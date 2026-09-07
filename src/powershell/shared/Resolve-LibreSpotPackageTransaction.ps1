function Resolve-LibreSpotPackageTransaction {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$TransactionPath,

        [Parameter(Mandatory = $true)]
        [string[]]$AllowedRoots
    )

    $marker = [System.IO.Path]::GetFullPath($TransactionPath)
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
        return $false
    }

    $transaction = $null
    try {
        $null = Test-LibreSpotPackageTransactionPath -Path $marker -AllowedRoots $AllowedRoots -TransactionId ('0' * 32) -Role 'marker'
        $transaction = Get-Content -LiteralPath $marker -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw "The pending package transaction is unreadable: $($_.Exception.Message)"
    }

    $transactionId = [string]$transaction.TransactionId
    if ($transaction.SchemaVersion -ne 1 -or $transactionId -notmatch '\A[0-9a-f]{32}\z') {
        throw 'The pending package transaction has an unsupported schema or transaction id.'
    }
    $canonicalMarker = Test-LibreSpotPackageTransactionPath -Path $marker -AllowedRoots $AllowedRoots -TransactionId $transactionId -Role 'marker'
    if (-not [string]::Equals($canonicalMarker, [System.IO.Path]::GetFullPath($TransactionPath), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The pending package transaction path changed while it was being read.'
    }
    if ($null -ne $transaction.TransactionPath -and
        -not [string]::Equals([System.IO.Path]::GetFullPath([string]$transaction.TransactionPath), $canonicalMarker, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The pending package transaction points at a different marker path.'
    }

    $descriptors = @($transaction.Descriptors)
    if ($descriptors.Count -eq 0 -or $descriptors.Count -gt 128) {
        throw 'The pending package transaction has an invalid descriptor count.'
    }

    function Move-PackageTransactionPath {
        param(
            [Parameter(Mandatory = $true)][string]$SourcePath,
            [Parameter(Mandatory = $true)][string]$DestinationPath,
            [Parameter(Mandatory = $true)][string]$Kind
        )
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
    }

    $states = [System.Collections.Generic.List[object]]::new()
    $targets = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($descriptor in $descriptors) {
        $action = [string]$descriptor.Action
        $kind = [string]$descriptor.Kind
        if ($action -notin @('swap', 'remove', 'preserve') -or $kind -notin @('directory', 'file')) {
            throw 'The pending package transaction contains an invalid action or target kind.'
        }
        $targetPath = [string]$descriptor.TargetPath
        $canonicalTarget = Test-LibreSpotPackageTransactionPath -Path $targetPath -AllowedRoots $AllowedRoots -TransactionId $transactionId -Role 'target'
        if (-not $targets.Add($canonicalTarget)) { throw "The pending package transaction repeats target $canonicalTarget." }

        $backupPath = [string]$descriptor.BackupPath
        $canonicalBackup = Test-LibreSpotPackageTransactionPath -Path $backupPath -AllowedRoots $AllowedRoots -TransactionId $transactionId -Role 'backup'
        if ([string]::Equals($canonicalTarget, $canonicalBackup, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'The pending package transaction uses a target as its own backup.'
        }

        $stagePath = $null
        if ($action -eq 'swap') {
            if ([string]::IsNullOrWhiteSpace([string]$descriptor.StagePath)) { throw 'A package swap is missing its staging path.' }
            $stagePath = Test-LibreSpotPackageTransactionPath -Path ([string]$descriptor.StagePath) -AllowedRoots $AllowedRoots -TransactionId $transactionId -Role 'stage'
            if ([string]::Equals($canonicalTarget, $stagePath, [System.StringComparison]::OrdinalIgnoreCase) -or
                [string]::Equals($canonicalBackup, $stagePath, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw 'The pending package transaction reuses a target or backup as its staging path.'
            }
            $expected = [string]$descriptor.ExpectedFingerprint
            if ($expected -notmatch '\A[0-9a-f]{64}\z') { throw 'A package swap has an invalid expected fingerprint.' }
        }

        $oldExists = [bool]$descriptor.OldExists
        $oldFingerprint = [string]$descriptor.OldFingerprint
        if ($oldExists -and $oldFingerprint -notmatch '\A[0-9a-f]{64}\z') {
            throw 'A package transaction descriptor has an invalid original fingerprint.'
        }
        if (-not $oldExists -and -not [string]::IsNullOrWhiteSpace($oldFingerprint)) {
            throw 'A package transaction descriptor records a fingerprint for a missing original.'
        }

        $states.Add([pscustomobject]@{
            Action             = $action
            Kind               = $kind
            TargetPath         = $canonicalTarget
            StagePath          = $stagePath
            BackupPath         = $canonicalBackup
            OldExists          = $oldExists
            OldFingerprint     = $oldFingerprint
            ExpectedFingerprint = [string]$descriptor.ExpectedFingerprint
        })
    }

    # Validate every existing path before making any recovery change. This keeps
    # a tampered marker from partially walking through a junction tree.
    foreach ($state in $states) {
        if (Test-Path -LiteralPath $state.TargetPath) {
            $item = Get-Item -LiteralPath $state.TargetPath -Force -ErrorAction Stop
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Package transaction target is a reparse point: $($state.TargetPath)"
            }
            if (($state.Kind -eq 'directory' -and -not $item.PSIsContainer) -or ($state.Kind -eq 'file' -and $item.PSIsContainer)) {
                throw "Package transaction target kind changed: $($state.TargetPath)"
            }
        }
        if (Test-Path -LiteralPath $state.BackupPath) {
            $backupItem = Get-Item -LiteralPath $state.BackupPath -Force -ErrorAction Stop
            if (($backupItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Package transaction backup is a reparse point: $($state.BackupPath)"
            }
            if (($state.Kind -eq 'directory' -and -not $backupItem.PSIsContainer) -or ($state.Kind -eq 'file' -and $backupItem.PSIsContainer)) {
                throw "Package transaction backup kind changed: $($state.BackupPath)"
            }
            if ($state.OldExists -and (Get-LibreSpotPackageFingerprint -Path $state.BackupPath -AllowReparse) -ne $state.OldFingerprint) {
                throw "Package transaction backup fingerprint does not match the recorded original: $($state.BackupPath)"
            }
            if (-not $state.OldExists) { throw "Package transaction has an unexpected backup for a missing original: $($state.BackupPath)" }
        } elseif ($state.OldExists -and -not (Test-Path -LiteralPath $state.TargetPath)) {
            throw "Package transaction lost both the original target and its backup: $($state.TargetPath)"
        }
        if ($state.StagePath -and (Test-Path -LiteralPath $state.StagePath)) {
            if ((Get-LibreSpotPackageFingerprint -Path $state.StagePath) -ne $state.ExpectedFingerprint) {
                throw "Package transaction staging fingerprint changed: $($state.StagePath)"
            }
        }
    }

    if ([string]$transaction.Status -eq 'Committed') {
        foreach ($state in $states) {
            if ($state.Action -eq 'swap') {
                if (-not (Test-Path -LiteralPath $state.TargetPath) -or
                    (Get-LibreSpotPackageFingerprint -Path $state.TargetPath) -ne $state.ExpectedFingerprint) {
                    throw "A committed package transaction no longer has its verified replacement: $($state.TargetPath)"
                }
            } elseif ($state.Action -eq 'remove' -and (Test-Path -LiteralPath $state.TargetPath)) {
                throw "A committed package transaction has a target that should have been removed: $($state.TargetPath)"
            }
            if ($state.StagePath -and (Test-Path -LiteralPath $state.StagePath)) {
                Remove-LibreSpotPackagePathSafely -Path $state.StagePath | Out-Null
            }
            if (Test-Path -LiteralPath $state.BackupPath) {
                Remove-LibreSpotPackagePathSafely -Path $state.BackupPath | Out-Null
            }
        }
        Remove-LibreSpotPackagePathSafely -Path $marker | Out-Null
        return $true
    }

    foreach ($state in $states) {
        $targetExists = Test-Path -LiteralPath $state.TargetPath
        $backupExists = Test-Path -LiteralPath $state.BackupPath
        if ($state.Action -eq 'preserve') {
            if ($state.OldExists) {
                if (-not $backupExists) { throw "Package transaction config backup is missing: $($state.BackupPath)" }
                if ($targetExists) { Remove-LibreSpotPackagePathSafely -Path $state.TargetPath | Out-Null }
                [System.IO.File]::Copy($state.BackupPath, $state.TargetPath, $false)
                if ((Get-LibreSpotPackageFingerprint -Path $state.TargetPath -AllowReparse) -ne $state.OldFingerprint) {
                    throw "Package transaction could not restore configuration: $($state.TargetPath)"
                }
            } elseif ($targetExists) {
                Remove-LibreSpotPackagePathSafely -Path $state.TargetPath | Out-Null
            }
            if (Test-Path -LiteralPath $state.BackupPath) { Remove-LibreSpotPackagePathSafely -Path $state.BackupPath | Out-Null }
            continue
        }

        if ($state.OldExists) {
            if ($backupExists) {
                if ($targetExists) {
                    $currentFingerprint = Get-LibreSpotPackageFingerprint -Path $state.TargetPath -AllowReparse
                    if ($currentFingerprint -eq $state.OldFingerprint) {
                        Remove-LibreSpotPackagePathSafely -Path $state.BackupPath | Out-Null
                    } elseif ($state.Action -eq 'swap' -and $currentFingerprint -eq $state.ExpectedFingerprint) {
                        Remove-LibreSpotPackagePathSafely -Path $state.TargetPath | Out-Null
                        Move-PackageTransactionPath -SourcePath $state.BackupPath -DestinationPath $state.TargetPath -Kind $state.Kind
                    } else {
                        throw "Package transaction target has an unexpected fingerprint: $($state.TargetPath)"
                    }
                } else {
                    Move-PackageTransactionPath -SourcePath $state.BackupPath -DestinationPath $state.TargetPath -Kind $state.Kind
                }
            } elseif (-not (Test-Path -LiteralPath $state.TargetPath) -or
                (Get-LibreSpotPackageFingerprint -Path $state.TargetPath -AllowReparse) -ne $state.OldFingerprint) {
                throw "Package transaction cannot prove the original target is intact: $($state.TargetPath)"
            }
            if ((Get-LibreSpotPackageFingerprint -Path $state.TargetPath -AllowReparse) -ne $state.OldFingerprint) {
                throw "Package transaction restored the wrong original bytes: $($state.TargetPath)"
            }
        } elseif ($targetExists) {
            if ($state.Action -ne 'swap' -or (Get-LibreSpotPackageFingerprint -Path $state.TargetPath) -ne $state.ExpectedFingerprint) {
                throw "Package transaction found unexpected bytes for a previously missing target: $($state.TargetPath)"
            }
            Remove-LibreSpotPackagePathSafely -Path $state.TargetPath | Out-Null
        }

        if ($state.StagePath -and (Test-Path -LiteralPath $state.StagePath)) {
            Remove-LibreSpotPackagePathSafely -Path $state.StagePath | Out-Null
        }
        if (Test-Path -LiteralPath $state.BackupPath) { Remove-LibreSpotPackagePathSafely -Path $state.BackupPath | Out-Null }
    }

    Remove-LibreSpotPackagePathSafely -Path $marker | Out-Null
    return $true
}
