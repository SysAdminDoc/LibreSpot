function Recover-LibreSpotAssetCacheTransaction {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$CacheDirectory,
        [ValidateSet('Recover', 'Begin', 'SetState', 'Complete', 'Abort')][string]$Operation = 'Recover',
        [string]$StagingDirectory,
        [string]$ReplacementDirectory,
        [string]$RollbackDirectory,
        [object[]]$ImportedEntries,
        [object]$Transaction,
        [ValidateSet('prepared', 'existing-moved', 'committed')][string]$State
    )

    $maxEntryCount = 2048
    $maxAssetBytes = 1GB
    $maxFingerprintBytes = 4GB
    $maxFingerprintFiles = 65536
    $markerName = '.asset-cache-transaction.json'

    function Test-Regular {
        param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][string]$Label)
        if (([System.IO.File]::GetAttributes($Path) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "The $Label is a reparse point; the asset-cache transaction marker was retained."
        }
    }

    function Test-Tree {
        param([Parameter(Mandatory = $true)][string]$Root)

        Test-Regular -Path $Root -Label 'asset-cache transaction path'
        $pending = [System.Collections.Generic.Stack[string]]::new()
        $pending.Push($Root)
        $files = [System.Collections.Generic.List[string]]::new()
        while ($pending.Count -gt 0) {
            $current = $pending.Pop()
            foreach ($child in @(Get-ChildItem -LiteralPath $current -Force -ErrorAction Stop)) {
                if (($child.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw "The asset-cache transaction tree contains a reparse point: $($child.FullName)"
                }
                if ($child.PSIsContainer) {
                    $pending.Push($child.FullName)
                } else {
                    $files.Add($child.FullName)
                }
            }
        }

        if ($files.Count -gt $maxFingerprintFiles) {
            throw 'The asset-cache transaction tree contains too many files; its marker was retained.'
        }
        [int64]$totalBytes = 0
        foreach ($path in $files) {
            $length = [int64](Get-Item -LiteralPath $path -Force -ErrorAction Stop).Length
            if ($length -lt 0 -or $totalBytes -gt ($maxFingerprintBytes - $length)) {
                throw 'The asset-cache transaction tree exceeds the fingerprint safety limit; its marker was retained.'
            }
            $totalBytes += $length
        }
        return @($files)
    }

    function Get-TreeHash {
        param([Parameter(Mandatory = $true)][string]$Root)

        $files = @(Test-Tree -Root $Root)
        $items = [System.Collections.Generic.List[object]]::new()
        $prefix = $Root.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        foreach ($path in $files) {
            $items.Add([pscustomobject]@{
                Path = $path
                Relative = $path.Substring($prefix.Length).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
            })
        }
        $items.Sort([System.Comparison[object]]{
            param($left, $right)
            return [System.StringComparer]::Ordinal.Compare([string]$left.Relative, [string]$right.Relative)
        })

        $utf8 = [System.Text.UTF8Encoding]::new($false)
        $hasher = [System.Security.Cryptography.IncrementalHash]::CreateHash([System.Security.Cryptography.HashAlgorithmName]::SHA256)
        try {
            [int64]$totalBytes = 0
            foreach ($item in $items) {
                $hasher.AppendData($utf8.GetBytes([string]$item.Relative))
                $hasher.AppendData([byte[]](0))
                $stream = [System.IO.File]::Open(
                    [string]$item.Path,
                    [System.IO.FileMode]::Open,
                    [System.IO.FileAccess]::Read,
                    [System.IO.FileShare]::Read)
                try {
                    $buffer = New-Object byte[] 81920
                    while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                        $hasher.AppendData($buffer, 0, $read)
                        $totalBytes += $read
                        if ($totalBytes -gt $maxFingerprintBytes) {
                            throw 'The asset-cache transaction tree exceeds the fingerprint safety limit; its marker was retained.'
                        }
                    }
                } finally {
                    $stream.Dispose()
                }
                $hasher.AppendData([byte[]](0))
            }
            return (($hasher.GetHashAndReset() | ForEach-Object { $_.ToString('x2') }) -join '')
        } finally {
            $hasher.Dispose()
        }
    }

    function Test-OwnedSibling {
        param(
            [Parameter(Mandatory = $true)][string]$Path,
            [Parameter(Mandatory = $true)][string]$ConfigDirectory,
            [Parameter(Mandatory = $true)][string]$Prefix
        )

        $resolved = [System.IO.Path]::GetFullPath($Path)
        $parent = [System.IO.Path]::GetDirectoryName($resolved)
        $name = [System.IO.Path]::GetFileName($resolved)
        if (-not $parent.Equals($ConfigDirectory, [System.StringComparison]::OrdinalIgnoreCase) -or
            -not $name.StartsWith($Prefix, [System.StringComparison]::Ordinal) -or
            $name.Substring($Prefix.Length) -notmatch '\A[0-9a-fA-F]{32}\z') {
            throw "The asset-cache transaction contains an unowned sibling path '$Path'; it was retained."
        }

        if (Test-Path -LiteralPath $resolved) {
            Test-Regular -Path $resolved -Label 'asset-cache transaction path'
            if (-not (Test-Path -LiteralPath $resolved -PathType Container)) {
                throw "The asset-cache transaction path '$resolved' is a file; its marker was retained."
            }
        }
        return $resolved
    }

    function Test-Entries {
        param([Parameter(Mandatory = $true)][object[]]$Entries)

        if ($Entries.Count -gt $maxEntryCount) {
            throw 'The asset-cache transaction contains too many entries; its marker was retained.'
        }
        $known = @{}
        foreach ($entry in $Entries) {
            $hash = [string]$entry.sha256
            [int64]$size = $entry.byteSize
            if ($hash -notmatch '\A[0-9a-f]{64}\z' -or $known.ContainsKey($hash) -or
                $size -lt 0 -or $size -gt $maxAssetBytes) {
                throw 'The asset-cache transaction contains an invalid entry; its marker was retained.'
            }
            $known[$hash] = $true
        }
    }

    function Write-Marker {
        param([Parameter(Mandatory = $true)][string]$MarkerPath, [Parameter(Mandatory = $true)][object]$Document)

        $json = $Document | ConvertTo-Json -Depth 8
        $utf8 = [System.Text.UTF8Encoding]::new($false)
        $bytes = $utf8.GetBytes($json)
        if ($bytes.Length -le 0 -or $bytes.Length -gt 1MB) {
            throw 'The asset-cache transaction marker exceeds the bounded recovery record limit.'
        }
        Write-LibreSpotAssetCacheFileAtomically -DestinationPath $MarkerPath -Writer {
            param($stream)
            $stream.Write($bytes, 0, $bytes.Length)
        }
    }

    function Remove-OwnedDirectory {
        param([Parameter(Mandatory = $true)][string]$Path)
        if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return }
        $null = Test-Tree -Root $Path
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
    }

    function Complete-Internal {
        param([Parameter(Mandatory = $true)][object]$Current)
        Remove-OwnedDirectory -Path ([string]$Current.StagingDirectory)
        Remove-OwnedDirectory -Path ([string]$Current.ReplacementDirectory)
        Remove-OwnedDirectory -Path ([string]$Current.RollbackDirectory)
        Test-Regular -Path ([string]$Current.MarkerPath) -Label 'asset-cache transaction marker'
        Remove-Item -LiteralPath ([string]$Current.MarkerPath) -Force -ErrorAction Stop
    }

    function Restore-Original {
        param([Parameter(Mandatory = $true)][string]$CacheRoot, [Parameter(Mandatory = $true)][object]$Current)
        if (-not (Test-Path -LiteralPath $Current.RollbackDirectory -PathType Container)) {
            throw 'The asset-cache rollback directory is missing; the transaction marker was retained.'
        }
        if (Test-Path -LiteralPath $CacheRoot) {
            throw 'The asset-cache target is occupied while its rollback directory is present; the transaction marker was retained.'
        }
        $rollbackHash = Get-TreeHash -Root ([string]$Current.RollbackDirectory)
        if ([bool]$Current.OriginalCacheExists -and $rollbackHash -cne [string]$Current.OriginalTreeSha256) {
            throw 'The asset-cache rollback directory failed its recorded fingerprint; the transaction marker was retained.'
        }
        [System.IO.Directory]::Move([string]$Current.RollbackDirectory, $CacheRoot)
    }

    function Test-Committed {
        param([Parameter(Mandatory = $true)][string]$CacheRoot, [Parameter(Mandatory = $true)][object]$Current)
        if (-not (Test-Path -LiteralPath $CacheRoot -PathType Container)) { return $false }
        if ((Get-TreeHash -Root $CacheRoot) -cne [string]$Current.ReplacementTreeSha256) { return $false }
        foreach ($entry in @($Current.ImportedEntries)) {
            $path = Join-Path $CacheRoot ([string]$entry.sha256)
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $false }
            Test-Regular -Path $path -Label 'asset-cache imported object'
            $info = Get-Item -LiteralPath $path -Force
            if ([int64]$info.Length -ne [int64]$entry.byteSize -or (Get-FileSha256Lower -Path $path) -cne [string]$entry.sha256) { return $false }
        }
        return $true
    }

    function New-Internal {
        param(
            [Parameter(Mandatory = $true)][string]$CacheRoot,
            [Parameter(Mandatory = $true)][string]$StagingRoot,
            [Parameter(Mandatory = $true)][string]$ReplacementRoot,
            [Parameter(Mandatory = $true)][string]$RollbackRoot,
            [Parameter(Mandatory = $true)][object[]]$Entries
        )

        $configRoot = [System.IO.Path]::GetDirectoryName($CacheRoot)
        if ([string]::IsNullOrWhiteSpace($configRoot)) { throw 'The asset-cache directory has no parent configuration directory.' }
        $markerPath = Join-Path $configRoot $markerName
        if (Test-Path -LiteralPath $markerPath) { throw 'An asset-cache transaction marker already exists. Recovery did not complete, so the new import was not started.' }
        if (-not ([System.IO.Path]::GetDirectoryName($CacheRoot)).Equals($configRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'The asset-cache transaction target is not a direct child of its configuration directory.' }
        Test-Regular -Path $configRoot -Label 'asset-cache configuration directory'
        $staging = Test-OwnedSibling -Path $StagingRoot -ConfigDirectory $configRoot -Prefix '.asset-cache-import-'
        $replacement = Test-OwnedSibling -Path $ReplacementRoot -ConfigDirectory $configRoot -Prefix '.asset-cache-ready-'
        $rollback = Test-OwnedSibling -Path $RollbackRoot -ConfigDirectory $configRoot -Prefix '.asset-cache-rollback-'
        Test-Entries -Entries $Entries

        $originalExists = Test-Path -LiteralPath $CacheRoot -PathType Container
        if (Test-Path -LiteralPath $CacheRoot) {
            Test-Regular -Path $CacheRoot -Label 'asset-cache directory'
            if (-not $originalExists) { throw 'The asset-cache transaction target is a file.' }
        }
        if ($originalExists) { $null = Test-Tree -Root $CacheRoot }
        $null = Test-Tree -Root $replacement
        $document = [ordered]@{
            schemaVersion         = 1
            state                 = 'prepared'
            cacheDirectory        = $CacheRoot
            stagingDirectory      = $staging
            replacementDirectory  = $replacement
            rollbackDirectory     = $rollback
            originalCacheExists   = $originalExists
            originalTreeSha256    = if ($originalExists) { Get-TreeHash -Root $CacheRoot } else { $null }
            replacementTreeSha256 = Get-TreeHash -Root $replacement
            importedEntries       = @($Entries | ForEach-Object { [pscustomobject][ordered]@{ sha256 = [string]$_.sha256; byteSize = [int64]$_.byteSize } })
        }
        Write-Marker -MarkerPath $markerPath -Document ([pscustomobject]$document)
        return [pscustomobject][ordered]@{
            MarkerPath            = $markerPath
            State                 = 'prepared'
            CacheDirectory        = $CacheRoot
            StagingDirectory      = $staging
            ReplacementDirectory  = $replacement
            RollbackDirectory     = $rollback
            OriginalCacheExists   = $originalExists
            OriginalTreeSha256    = $document.originalTreeSha256
            ReplacementTreeSha256 = $document.replacementTreeSha256
            ImportedEntries       = @($document.importedEntries)
        }
    }

    function Set-StateInternal {
        param([Parameter(Mandatory = $true)][object]$Current, [Parameter(Mandatory = $true)][string]$NextState)
        $Current.State = $NextState
        $document = [ordered]@{
            schemaVersion         = 1
            state                 = $NextState
            cacheDirectory        = [string]$Current.CacheDirectory
            stagingDirectory      = [string]$Current.StagingDirectory
            replacementDirectory  = [string]$Current.ReplacementDirectory
            rollbackDirectory     = [string]$Current.RollbackDirectory
            originalCacheExists   = [bool]$Current.OriginalCacheExists
            originalTreeSha256    = $Current.OriginalTreeSha256
            replacementTreeSha256 = [string]$Current.ReplacementTreeSha256
            importedEntries       = @($Current.ImportedEntries)
        }
        Write-Marker -MarkerPath ([string]$Current.MarkerPath) -Document ([pscustomobject]$document)
    }

    function Recover-Internal {
        param([Parameter(Mandatory = $true)][string]$CacheRoot)

        $configRoot = [System.IO.Path]::GetDirectoryName($CacheRoot)
        if ([string]::IsNullOrWhiteSpace($configRoot)) { throw 'The asset-cache directory has no parent configuration directory.' }
        $markerPath = Join-Path $configRoot $markerName
        if (-not (Test-Path -LiteralPath $markerPath)) { return }
        Test-Regular -Path $markerPath -Label 'asset-cache transaction marker'
        if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) { throw 'The asset-cache transaction marker is not a file; it was retained.' }
        $markerInfo = Get-Item -LiteralPath $markerPath -Force
        if ($markerInfo.Length -le 0 -or $markerInfo.Length -gt 1MB) { throw 'The asset-cache transaction marker is too large or empty; it was retained for inspection.' }
        try {
            $document = Get-Content -LiteralPath $markerPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
        } catch {
            throw "The asset-cache transaction marker is malformed and was retained: $($_.Exception.Message)"
        }
        if ([int]$document.schemaVersion -ne 1 -or [string]$document.state -notin @('prepared', 'existing-moved', 'committed')) { throw 'The asset-cache transaction marker is invalid; it was retained.' }
        if (-not ([System.IO.Path]::GetFullPath([string]$document.cacheDirectory)).Equals($CacheRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'The asset-cache transaction marker targets a different cache; it was retained.' }
        if (-not (Test-Path -LiteralPath $configRoot -PathType Container)) { throw 'The asset-cache configuration directory is missing; the transaction marker was retained.' }
        Test-Regular -Path $configRoot -Label 'asset-cache configuration directory'
        $staging = Test-OwnedSibling -Path ([string]$document.stagingDirectory) -ConfigDirectory $configRoot -Prefix '.asset-cache-import-'
        $replacement = Test-OwnedSibling -Path ([string]$document.replacementDirectory) -ConfigDirectory $configRoot -Prefix '.asset-cache-ready-'
        $rollback = Test-OwnedSibling -Path ([string]$document.rollbackDirectory) -ConfigDirectory $configRoot -Prefix '.asset-cache-rollback-'
        if ([bool]$document.originalCacheExists) {
            if ([string]$document.originalTreeSha256 -notmatch '\A[0-9a-f]{64}\z') { throw 'The asset-cache transaction has an invalid original fingerprint; it was retained.' }
        } elseif ($null -ne $document.originalTreeSha256) { throw 'The asset-cache transaction has an unexpected original fingerprint; it was retained.' }
        if ([string]$document.replacementTreeSha256 -notmatch '\A[0-9a-f]{64}\z') { throw 'The asset-cache transaction has an invalid replacement fingerprint; it was retained.' }
        $entries = @($document.importedEntries)
        Test-Entries -Entries $entries
        $current = [pscustomobject][ordered]@{
            MarkerPath            = $markerPath
            State                 = [string]$document.state
            CacheDirectory        = $CacheRoot
            StagingDirectory      = $staging
            ReplacementDirectory  = $replacement
            RollbackDirectory     = $rollback
            OriginalCacheExists   = [bool]$document.originalCacheExists
            OriginalTreeSha256    = [string]$document.originalTreeSha256
            ReplacementTreeSha256 = [string]$document.replacementTreeSha256
            ImportedEntries       = $entries
        }

        $cacheExists = Test-Path -LiteralPath $CacheRoot -PathType Container
        $stagingExists = Test-Path -LiteralPath $staging -PathType Container
        $replacementExists = Test-Path -LiteralPath $replacement -PathType Container
        $rollbackExists = Test-Path -LiteralPath $rollback -PathType Container
        foreach ($path in @($CacheRoot, $staging, $replacement, $rollback)) {
            if (Test-Path -LiteralPath $path) {
                Test-Regular -Path $path -Label 'asset-cache transaction path'
                if ($path -ne $CacheRoot -and -not (Test-Path -LiteralPath $path -PathType Container)) { throw 'The asset-cache transaction path is a file; its marker was retained.' }
                if ($path -eq $CacheRoot -and -not $cacheExists) { throw 'The asset-cache target is a file; the transaction marker was retained.' }
            }
        }
        if ($cacheExists) { $null = Test-Tree -Root $CacheRoot }
        if ($stagingExists) { $null = Test-Tree -Root $staging }
        if ($replacementExists) { $null = Test-Tree -Root $replacement }
        if ($rollbackExists) { $null = Test-Tree -Root $rollback }

        if ($current.State -eq 'committed') {
            if ($cacheExists -and (Test-Committed -CacheRoot $CacheRoot -Current $current)) { Complete-Internal -Current $current; return }
            if (-not $cacheExists -and $current.OriginalCacheExists -and $rollbackExists) {
                Restore-Original -CacheRoot $CacheRoot -Current $current
                Remove-OwnedDirectory -Path $staging
                Remove-OwnedDirectory -Path $replacement
                Test-Regular -Path $markerPath -Label 'asset-cache transaction marker'
                Remove-Item -LiteralPath $markerPath -Force -ErrorAction Stop
                return
            }
            if ($cacheExists -and $rollbackExists -and -not $replacementExists) {
                [System.IO.Directory]::Move($CacheRoot, $replacement)
                try { Restore-Original -CacheRoot $CacheRoot -Current $current }
                catch {
                    if (-not (Test-Path -LiteralPath $CacheRoot) -and (Test-Path -LiteralPath $replacement -PathType Container)) { [System.IO.Directory]::Move($replacement, $CacheRoot) }
                    throw
                }
                Remove-OwnedDirectory -Path $staging
                Remove-OwnedDirectory -Path $replacement
                Test-Regular -Path $markerPath -Label 'asset-cache transaction marker'
                Remove-Item -LiteralPath $markerPath -Force -ErrorAction Stop
                return
            }
            throw 'The committed asset-cache transaction could not be verified; its marker and owned recovery paths were retained.'
        }

        if ($cacheExists -and $rollbackExists -and -not $replacementExists) {
            if (Test-Committed -CacheRoot $CacheRoot -Current $current) { Complete-Internal -Current $current; return }
            [System.IO.Directory]::Move($CacheRoot, $replacement)
            try { Restore-Original -CacheRoot $CacheRoot -Current $current }
            catch {
                if (-not (Test-Path -LiteralPath $CacheRoot) -and (Test-Path -LiteralPath $replacement -PathType Container)) { [System.IO.Directory]::Move($replacement, $CacheRoot) }
                throw
            }
            Remove-OwnedDirectory -Path $staging
            Remove-OwnedDirectory -Path $replacement
            Test-Regular -Path $markerPath -Label 'asset-cache transaction marker'
            Remove-Item -LiteralPath $markerPath -Force -ErrorAction Stop
            return
        }

        if (-not $cacheExists -and $current.OriginalCacheExists -and $rollbackExists) {
            Restore-Original -CacheRoot $CacheRoot -Current $current
            Remove-OwnedDirectory -Path $staging
            Remove-OwnedDirectory -Path $replacement
            Test-Regular -Path $markerPath -Label 'asset-cache transaction marker'
            Remove-Item -LiteralPath $markerPath -Force -ErrorAction Stop
            return
        }

        if (-not $cacheExists -and -not $rollbackExists -and -not $current.OriginalCacheExists -and $replacementExists) {
            Remove-OwnedDirectory -Path $replacement
            Remove-OwnedDirectory -Path $staging
            Test-Regular -Path $markerPath -Label 'asset-cache transaction marker'
            Remove-Item -LiteralPath $markerPath -Force -ErrorAction Stop
            return
        }

        if ($cacheExists -and -not $rollbackExists -and -not $current.OriginalCacheExists -and (Test-Committed -CacheRoot $CacheRoot -Current $current)) { Complete-Internal -Current $current; return }
        if ($cacheExists -and $replacementExists -and -not $rollbackExists) {
            Remove-OwnedDirectory -Path $replacement
            Remove-OwnedDirectory -Path $staging
            Test-Regular -Path $markerPath -Label 'asset-cache transaction marker'
            Remove-Item -LiteralPath $markerPath -Force -ErrorAction Stop
            return
        }
        throw 'The asset-cache transaction marker describes an unsafe filesystem state; it and its owned paths were retained.'
    }

    switch ($Operation) {
        'Begin' {
            return (New-Internal -CacheRoot ([System.IO.Path]::GetFullPath($CacheDirectory)) -StagingRoot ([System.IO.Path]::GetFullPath($StagingDirectory)) -ReplacementRoot ([System.IO.Path]::GetFullPath($ReplacementDirectory)) -RollbackRoot ([System.IO.Path]::GetFullPath($RollbackDirectory)) -Entries $ImportedEntries)
        }
        'SetState' { Set-StateInternal -Current $Transaction -NextState $State; return }
        'Complete' { Complete-Internal -Current $Transaction; return }
        'Abort' { try { Complete-Internal -Current $Transaction } catch {}; return }
        default { Recover-Internal -CacheRoot ([System.IO.Path]::GetFullPath($CacheDirectory)); return }
    }
}
