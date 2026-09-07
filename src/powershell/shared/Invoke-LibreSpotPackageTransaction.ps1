function Invoke-LibreSpotPackageTransaction {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$TransactionPath,

        [Parameter(Mandatory = $true)]
        [string[]]$AllowedRoots,

        [Parameter(Mandatory = $true)]
        [object[]]$Packages,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Commit,

        [string]$TransactionId = ([Guid]::NewGuid().ToString('N')),

        [scriptblock]$BeforeRename,

        [scriptblock]$AfterRename
    )

    if ($TransactionId -notmatch '\A[0-9a-f]{32}\z') { throw "Package transaction id is invalid: $TransactionId" }
    $marker = [System.IO.Path]::GetFullPath($TransactionPath)
    $null = Test-LibreSpotPackageTransactionPath -Path $marker -AllowedRoots $AllowedRoots -TransactionId $TransactionId -Role 'marker'
    if (Test-Path -LiteralPath $marker) { throw "A package transaction is already pending at $marker." }

    function Write-PackageTransactionMarker {
        param([Parameter(Mandatory = $true)][object]$Document)
        $parent = Split-Path -Path $marker -Parent
        $temporary = Join-Path $parent ('.librespot-package-' + $TransactionId + '-marker.tmp')
        if (Test-Path -LiteralPath $temporary) { Remove-LibreSpotPackagePathSafely -Path $temporary | Out-Null }
        $json = $Document | ConvertTo-Json -Depth 16
        [System.IO.File]::WriteAllText($temporary, $json, [System.Text.UTF8Encoding]::new($false))
        if (-not (Test-Path -LiteralPath $marker)) {
            [System.IO.File]::Move($temporary, $marker)
        } else {
            try {
                [System.IO.File]::Replace($temporary, $marker, $null, $true)
            } catch {
                [System.IO.File]::Copy($temporary, $marker, $true)
                Remove-LibreSpotPackagePathSafely -Path $temporary | Out-Null
            }
        }
    }

    function Move-PackageTransactionPath {
        param(
            [Parameter(Mandatory = $true)][string]$SourcePath,
            [Parameter(Mandatory = $true)][string]$DestinationPath,
            [Parameter(Mandatory = $true)][string]$Kind,
            [Parameter(Mandatory = $true)][object]$Descriptor,
            [Parameter(Mandatory = $true)][string]$Phase
        )
        if ($BeforeRename) { & $BeforeRename $Descriptor $Phase }
        if (-not (Test-Path -LiteralPath $SourcePath)) { throw "Package transaction source is missing: $SourcePath" }
        if (Test-Path -LiteralPath $DestinationPath) { throw "Package transaction destination is occupied: $DestinationPath" }
        $sourceItem = Get-Item -LiteralPath $SourcePath -Force -ErrorAction Stop
        if (($sourceItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Package transaction source is a reparse point: $SourcePath"
        }
        if ($Kind -eq 'directory' -and -not $sourceItem.PSIsContainer) { throw "Package transaction expected a directory: $SourcePath" }
        if ($Kind -eq 'file' -and $sourceItem.PSIsContainer) { throw "Package transaction expected a file: $SourcePath" }
        if ($Kind -eq 'directory') {
            [System.IO.Directory]::Move($SourcePath, $DestinationPath)
        } else {
            [System.IO.File]::Move($SourcePath, $DestinationPath)
        }
        if ($AfterRename) { & $AfterRename $Descriptor $Phase }
    }

    $normalized = [System.Collections.Generic.List[object]]::new()
    $targets = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $index = 0
    $committed = $false
    try {
        foreach ($package in @($Packages)) {
            $action = [string]$package.Action
            $kind = [string]$package.Kind
            if ($action -notin @('swap', 'remove', 'preserve') -or $kind -notin @('directory', 'file')) {
                throw 'Package transaction contains an invalid action or target kind.'
            }
            $targetPath = Test-LibreSpotPackageTransactionPath -Path ([string]$package.TargetPath) -AllowedRoots $AllowedRoots -TransactionId $TransactionId -Role 'target'
            if (-not $targets.Add($targetPath)) { throw "Package transaction repeats target $targetPath." }
            $targetExists = Test-Path -LiteralPath $targetPath
            $targetItem = $null
            $oldFingerprint = ''
            if ($targetExists) {
                $targetItem = Get-Item -LiteralPath $targetPath -Force -ErrorAction Stop
                if (($targetItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Package transaction target is a reparse point: $targetPath" }
                if (($kind -eq 'directory' -and -not $targetItem.PSIsContainer) -or ($kind -eq 'file' -and $targetItem.PSIsContainer)) {
                    throw "Package transaction target kind changed: $targetPath"
                }
                $oldFingerprint = Get-LibreSpotPackageFingerprint -Path $targetPath -AllowReparse
            }

            $stagePath = $null
            $expectedFingerprint = [string]$package.ExpectedFingerprint
            if ($action -eq 'swap') {
                if ([string]::IsNullOrWhiteSpace([string]$package.StagePath)) { throw 'A package swap is missing its staging path.' }
                $stagePath = Test-LibreSpotPackageTransactionPath -Path ([string]$package.StagePath) -AllowedRoots $AllowedRoots -TransactionId $TransactionId -Role 'stage'
                if ([string]::Equals($stagePath, $targetPath, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Package transaction staging path equals its target.' }
                if ($expectedFingerprint -notmatch '\A[0-9a-f]{64}\z') { throw "Package staging fingerprint is invalid for $stagePath." }
                if (-not (Test-Path -LiteralPath $stagePath)) { throw "Package staging path is missing: $stagePath" }
                $stageItem = Get-Item -LiteralPath $stagePath -Force -ErrorAction Stop
                if (($stageItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Package staging path is a reparse point: $stagePath" }
                if (($kind -eq 'directory' -and -not $stageItem.PSIsContainer) -or ($kind -eq 'file' -and $stageItem.PSIsContainer)) {
                    throw "Package staging path kind changed: $stagePath"
                }
                if ((Get-LibreSpotPackageFingerprint -Path $stagePath) -ne $expectedFingerprint) {
                    throw "Package staging fingerprint does not match the verified package: $stagePath"
                }
            }

            $backupName = '.librespot-package-' + $TransactionId + '-' + $index.ToString('000') + '-backup'
            $backupPath = Test-LibreSpotPackageTransactionPath -Path (Join-Path (Split-Path -Path $targetPath -Parent) $backupName) -AllowedRoots $AllowedRoots -TransactionId $TransactionId -Role 'backup'
            if (Test-Path -LiteralPath $backupPath) { throw "Package transaction backup path is already occupied: $backupPath" }
            if ($action -eq 'preserve' -and $kind -ne 'file') { throw 'Only files can be preserved as package transaction configuration.' }

            $normalized.Add([pscustomobject]@{
                Action              = $action
                Kind                = $kind
                TargetPath          = $targetPath
                StagePath           = $stagePath
                BackupPath          = $backupPath
                OldExists           = [bool]$targetExists
                OldFingerprint      = $oldFingerprint
                RecoveryFingerprint = $oldFingerprint
                RecoveryOwned       = $false
                ExpectedFingerprint = $expectedFingerprint
                Status              = 'Prepared'
            })
            $index++
        }
        if ($normalized.Count -eq 0) { throw 'Package transaction has no descriptors.' }

        $transaction = [pscustomobject]@{
            SchemaVersion = 1
            TransactionId = $TransactionId
            TransactionPath = $marker
            AllowedRoots  = @($AllowedRoots | ForEach-Object { [System.IO.Path]::GetFullPath([string]$_).TrimEnd('\', '/') })
            StartedAt     = (Get-Date).ToUniversalTime().ToString('o')
            Status        = 'Prepared'
            Descriptors   = @($normalized)
        }

        # Preserve configuration bytes before any package rename. A preserved
        # configuration is copied, so the commit callback can update it and a
        # recovery can restore the exact original bytes.
        foreach ($descriptor in $normalized) {
            if ($descriptor.Action -ne 'preserve' -or -not $descriptor.OldExists) { continue }
            [System.IO.File]::Copy($descriptor.TargetPath, $descriptor.BackupPath, $false)
            if ((Get-LibreSpotPackageFingerprint -Path $descriptor.BackupPath -AllowReparse) -ne $descriptor.OldFingerprint) {
                throw "Package transaction configuration backup failed verification: $($descriptor.BackupPath)"
            }
        }

        Write-PackageTransactionMarker -Document $transaction
        $markerWritten = $true

        foreach ($descriptor in $normalized) {
            if ($descriptor.Action -eq 'preserve') { continue }
            if ($descriptor.OldExists) {
                Move-PackageTransactionPath -SourcePath $descriptor.TargetPath -DestinationPath $descriptor.BackupPath -Kind $descriptor.Kind -Descriptor $descriptor -Phase 'target-to-backup'
                $descriptor.Status = 'OriginalMoved'
                Write-PackageTransactionMarker -Document $transaction
            }
            if ($descriptor.Action -eq 'swap') {
                Move-PackageTransactionPath -SourcePath $descriptor.StagePath -DestinationPath $descriptor.TargetPath -Kind $descriptor.Kind -Descriptor $descriptor -Phase 'stage-to-target'
                $descriptor.Status = 'ReplacementMoved'
                Write-PackageTransactionMarker -Document $transaction
            }
        }

        $transaction.Status = 'Committing'
        Write-PackageTransactionMarker -Document $transaction
        try {
            & $Commit
        } catch {
            # A commit callback can write configuration and then fail. Record
            # the bytes observed at that point so recovery can restore them
            # only when this transaction still owns the target.
            foreach ($descriptor in $normalized) {
                if ($descriptor.Action -ne 'preserve') { continue }
                if (Test-Path -LiteralPath $descriptor.TargetPath -PathType Leaf) {
                    try {
                        $descriptor.RecoveryFingerprint = Get-LibreSpotPackageFingerprint -Path $descriptor.TargetPath -AllowReparse
                        $descriptor.RecoveryOwned = $true
                    } catch {
                        $descriptor.RecoveryFingerprint = ''
                        $descriptor.RecoveryOwned = $false
                    }
                }
            }
            try { Write-PackageTransactionMarker -Document $transaction } catch {}
            throw
        }

        foreach ($descriptor in $normalized) {
            if ($descriptor.Action -eq 'swap') {
                if (-not (Test-Path -LiteralPath $descriptor.TargetPath) -or
                    (Get-LibreSpotPackageFingerprint -Path $descriptor.TargetPath) -ne $descriptor.ExpectedFingerprint) {
                    throw "Package transaction replacement failed post-commit verification: $($descriptor.TargetPath)"
                }
            } elseif ($descriptor.Action -eq 'remove' -and (Test-Path -LiteralPath $descriptor.TargetPath)) {
                throw "Package transaction removal failed post-commit verification: $($descriptor.TargetPath)"
            }
        }

        # Publish the commit point before deleting rollback material. If the
        # process ends during cleanup, the next operation can finish cleanup
        # while keeping the verified replacement in place.
        $transaction.Status = 'Committed'
        Write-PackageTransactionMarker -Document $transaction
        $committed = $true

        foreach ($descriptor in $normalized) {
            if ($descriptor.StagePath -and (Test-Path -LiteralPath $descriptor.StagePath)) {
                Remove-LibreSpotPackagePathSafely -Path $descriptor.StagePath | Out-Null
            }
            if (Test-Path -LiteralPath $descriptor.BackupPath) {
                Remove-LibreSpotPackagePathSafely -Path $descriptor.BackupPath | Out-Null
            }
        }
        Remove-LibreSpotPackagePathSafely -Path $marker | Out-Null
        return $true
    } catch {
        $failure = $_
        if (Test-Path -LiteralPath $marker) {
            try {
                Resolve-LibreSpotPackageTransaction -TransactionPath $marker -AllowedRoots $AllowedRoots | Out-Null
                if ($committed) { return $true }
            } catch {
                throw "Package transaction failed and automatic recovery also failed. The pending transaction was retained at $marker. Commit error: $($failure.Exception.Message) Recovery error: $($_.Exception.Message)"
            }
        } else {
            foreach ($descriptor in @($normalized)) {
                if (Test-Path -LiteralPath $descriptor.BackupPath) {
                    try { Remove-LibreSpotPackagePathSafely -Path $descriptor.BackupPath | Out-Null } catch {}
                }
            }
        }
        throw $failure
    }
}
