#requires -Version 5.1

BeforeAll {
    $script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    . (Join-Path $script:RepoRoot 'src\powershell\shared\Get-FileSha256Lower.ps1')
    . (Join-Path $script:RepoRoot 'src\powershell\shared\Enter-LibreSpotAssetCacheLease.ps1')
    . (Join-Path $script:RepoRoot 'src\powershell\shared\Exit-LibreSpotAssetCacheLease.ps1')
    . (Join-Path $script:RepoRoot 'src\powershell\shared\Write-LibreSpotAssetCacheFileAtomically.ps1')
    . (Join-Path $script:RepoRoot 'src\powershell\shared\Update-AssetCacheIndexEntry.ps1')
    . (Join-Path $script:RepoRoot 'src\powershell\shared\Save-ToAssetCache.ps1')
    . (Join-Path $script:RepoRoot 'src\powershell\shared\Export-LibreSpotAssetCacheBundle.ps1')
    . (Join-Path $script:RepoRoot 'src\powershell\shared\Import-LibreSpotAssetCacheBundle.ps1')

    function Write-Log { param([string]$Message, [string]$Level = 'INFO') }

    function Write-TestCache {
        param(
            [Parameter(Mandatory = $true)][string]$CachePath,
            [Parameter(Mandatory = $true)][string]$Label,
            [Parameter(Mandatory = $true)][string]$Content
        )

        New-Item -Path $CachePath -ItemType Directory -Force | Out-Null
        $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($Content)
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hash = (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join '')
        } finally {
            $sha.Dispose()
        }
        [System.IO.File]::WriteAllBytes((Join-Path $CachePath $hash), $bytes)
        $now = (Get-Date).ToUniversalTime().ToString('o')
        $index = [ordered]@{
            schemaVersion = 1
            generatedAtUtc = $now
            entries = @(
                [ordered]@{
                    sha256 = $hash
                    label = $Label
                    sourceUrl = "https://example.invalid/$Label"
                    byteSize = $bytes.Length
                    firstSeenAtUtc = $now
                    lastUsedAtUtc = $now
                    lastVerifiedAtUtc = $now
                    status = 'present'
                    quarantinedPath = $null
                }
            )
        }
        [System.IO.File]::WriteAllText(
            (Join-Path $CachePath 'asset-cache-index.json'),
            ($index | ConvertTo-Json -Depth 8),
            [System.Text.UTF8Encoding]::new($false))
        return $hash
    }

    function Get-TestCacheSnapshot {
        param([Parameter(Mandatory = $true)][string]$CachePath)

        return @(
            Get-ChildItem -LiteralPath $CachePath -File -Recurse -Force |
                Sort-Object FullName |
                ForEach-Object {
                    $relative = $_.FullName.Substring($CachePath.TrimEnd('\').Length + 1)
                    "$relative=$((Get-FileSha256Lower -Path $_.FullName))"
                }
        )
    }

    function Set-TestBundleEntryStatus {
        param(
            [Parameter(Mandatory = $true)][string]$BundlePath,
            [Parameter(Mandatory = $true)][string]$Status
        )

        Add-Type -AssemblyName System.IO.Compression -ErrorAction Stop
        $file = [System.IO.File]::Open($BundlePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $archive = [System.IO.Compression.ZipArchive]::new($file, [System.IO.Compression.ZipArchiveMode]::Update, $true, [System.Text.Encoding]::UTF8)
        try {
            $entry = $archive.GetEntry('manifest.json')
            $reader = [System.IO.StreamReader]::new($entry.Open(), [System.Text.Encoding]::UTF8)
            try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
            $manifest.entries[0].status = $Status
            $entry.Delete()
            $replacement = $archive.CreateEntry('manifest.json')
            $writer = [System.IO.StreamWriter]::new($replacement.Open(), [System.Text.UTF8Encoding]::new($false))
            try { $writer.Write(($manifest | ConvertTo-Json -Depth 8)) } finally { $writer.Dispose() }
        } finally {
            $archive.Dispose()
            $file.Dispose()
        }
    }
}

Describe 'PowerShell asset-cache bundle import transaction' {
    BeforeEach {
        $script:TestRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('LibreSpot-AssetBundle-' + [guid]::NewGuid().ToString('N'))
        $script:SourceCache = Join-Path $script:TestRoot 'source\cache'
        $script:TargetConfig = Join-Path $script:TestRoot 'target'
        $script:TargetCache = Join-Path $script:TargetConfig 'cache'
        $script:BundlePath = Join-Path $script:TestRoot 'cache.zip'
        $script:ImportHash = Write-TestCache -CachePath $script:SourceCache -Label 'imported' -Content 'imported bytes'
        $null = Write-TestCache -CachePath $script:TargetCache -Label 'existing' -Content 'existing bytes'
        [System.IO.File]::WriteAllText((Join-Path $script:TargetCache 'unindexed-note.txt'), 'preserve me')

        $global:CONFIG_DIR = Split-Path -Path $script:SourceCache -Parent
        $global:CACHE_DIR = $script:SourceCache
        $null = Export-LibreSpotAssetCacheBundle -OutputPath $script:BundlePath -ProductVersion 'test'
        $global:CONFIG_DIR = $script:TargetConfig
        $global:CACHE_DIR = $script:TargetCache
    }

    AfterEach {
        if (Test-Path -LiteralPath $script:TestRoot) {
            Remove-Item -LiteralPath $script:TestRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'restores every original cache byte when commit is interrupted after the backup move' {
        $before = @(Get-TestCacheSnapshot -CachePath $script:TargetCache)

        { Import-LibreSpotAssetCacheBundle -BundlePath $script:BundlePath -AfterBackupMove { throw 'Simulated commit interruption.' } } |
            Should -Throw -ExpectedMessage '*Simulated commit interruption*'

        @(Get-TestCacheSnapshot -CachePath $script:TargetCache) | Should -Be $before
        (Test-Path -LiteralPath (Join-Path $script:TargetCache $script:ImportHash) -PathType Leaf) | Should -BeFalse
        @(Get-ChildItem -LiteralPath $script:TargetConfig -Directory -Filter '.asset-cache-rollback-*').Count | Should -Be 0
    }

    It 'rejects a non-present manifest entry before changing the target cache' {
        Set-TestBundleEntryStatus -BundlePath $script:BundlePath -Status 'stale'
        $before = @(Get-TestCacheSnapshot -CachePath $script:TargetCache)

        { Import-LibreSpotAssetCacheBundle -BundlePath $script:BundlePath } |
            Should -Throw -ExpectedMessage '*not a verified present entry*'

        @(Get-TestCacheSnapshot -CachePath $script:TargetCache) | Should -Be $before
        (Test-Path -LiteralPath (Join-Path $script:TargetCache $script:ImportHash) -PathType Leaf) | Should -BeFalse
    }

    It 'retains a parseable prior index when an atomic write is interrupted' {
        $indexPath = Join-Path $script:TargetCache 'asset-cache-index.json'
        $before = [System.IO.File]::ReadAllBytes($indexPath)

        {
            Write-LibreSpotAssetCacheFileAtomically -DestinationPath $indexPath -Writer {
                param($stream)
                $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes('{"schemaVersion":1,"entries":')
                $stream.Write($bytes, 0, $bytes.Length)
                throw 'Simulated interrupted index write.'
            }
        } | Should -Throw -ExpectedMessage '*Simulated interrupted index write*'

        { [System.IO.File]::ReadAllText($indexPath) | ConvertFrom-Json -ErrorAction Stop } | Should -Not -Throw
        [System.Convert]::ToBase64String([System.IO.File]::ReadAllBytes($indexPath)) |
            Should -Be ([System.Convert]::ToBase64String($before))
    }

    It 'retains and reports a corrupt index instead of replacing it with an empty inventory' {
        $indexPath = Join-Path $script:TargetCache 'asset-cache-index.json'
        [System.IO.File]::WriteAllText($indexPath, '{"schemaVersion":1,"entries":', [System.Text.UTF8Encoding]::new($false))
        $before = [System.IO.File]::ReadAllText($indexPath)

        Update-AssetCacheIndexEntry -SHA256Hash $script:ImportHash -Label 'should not replace corrupt input'

        [System.IO.File]::ReadAllText($indexPath) | Should -Be $before
    }

    It 'serializes distinct concurrent saves while retaining both verified objects and entries' {
        $concurrentRoot = Join-Path $script:TestRoot 'concurrent'
        $concurrentCache = Join-Path $concurrentRoot 'cache'
        New-Item -Path $concurrentCache -ItemType Directory -Force | Out-Null
        $sourceA = Join-Path $concurrentRoot 'a.bin'
        $sourceB = Join-Path $concurrentRoot 'b.bin'
        [System.IO.File]::WriteAllText($sourceA, 'concurrent alpha', [System.Text.UTF8Encoding]::new($false))
        [System.IO.File]::WriteAllText($sourceB, 'concurrent beta', [System.Text.UTF8Encoding]::new($false))
        $hashA = Get-FileSha256Lower -Path $sourceA
        $hashB = Get-FileSha256Lower -Path $sourceB
        $worker = Join-Path $concurrentRoot 'save-worker.ps1'
        $workerText = @'
param([string]$CachePath, [string]$SourcePath, [string]$Hash, [string]$ResultPath, [string]$RepoRoot)
$global:CACHE_DIR = $CachePath
function Write-Log { param([string]$Message, [string]$Level = 'INFO') }
. (Join-Path $RepoRoot 'src\powershell\shared\Enter-LibreSpotAssetCacheLease.ps1')
. (Join-Path $RepoRoot 'src\powershell\shared\Exit-LibreSpotAssetCacheLease.ps1')
. (Join-Path $RepoRoot 'src\powershell\shared\Write-LibreSpotAssetCacheFileAtomically.ps1')
. (Join-Path $RepoRoot 'src\powershell\shared\Get-FileSha256Lower.ps1')
. (Join-Path $RepoRoot 'src\powershell\shared\Update-AssetCacheIndexEntry.ps1')
. (Join-Path $RepoRoot 'src\powershell\shared\Save-ToAssetCache.ps1')
$lease = $null
try {
    $lease = Enter-LibreSpotAssetCacheLease -CacheDirectory $CachePath -Label 'concurrent fixture'
    Start-Sleep -Milliseconds 250
    Save-ToAssetCache -SourcePath $SourcePath -SHA256Hash $Hash -Label $Hash -SourceUrl "https://example.invalid/$Hash"
    [System.IO.File]::WriteAllText($ResultPath, 'ok')
} catch {
    [System.IO.File]::WriteAllText($ResultPath, $_.Exception.Message)
    exit 1
} finally {
    if ($lease) { Exit-LibreSpotAssetCacheLease -Lease $lease }
}
'@
        [System.IO.File]::WriteAllText($worker, $workerText, [System.Text.UTF8Encoding]::new($false))
        $resultA = Join-Path $concurrentRoot 'a.result'
        $resultB = Join-Path $concurrentRoot 'b.result'
        $powershellPath = (Get-Command powershell.exe -ErrorAction Stop).Source
        $processA = Start-Process -FilePath $powershellPath -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-File', $worker, $concurrentCache, $sourceA, $hashA, $resultA, $script:RepoRoot) -WindowStyle Hidden -PassThru
        $processB = Start-Process -FilePath $powershellPath -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-File', $worker, $concurrentCache, $sourceB, $hashB, $resultB, $script:RepoRoot) -WindowStyle Hidden -PassThru
        try {
            $processA.WaitForExit(10000) | Should -BeTrue
            $processB.WaitForExit(10000) | Should -BeTrue
            $processA.ExitCode | Should -Be 0
            $processB.ExitCode | Should -Be 0
        } finally {
            $processA.Dispose()
            $processB.Dispose()
        }

        [System.IO.File]::ReadAllText($resultA) | Should -Be 'ok'
        [System.IO.File]::ReadAllText($resultB) | Should -Be 'ok'
        (Get-FileSha256Lower -Path (Join-Path $concurrentCache $hashA)) | Should -Be $hashA
        (Get-FileSha256Lower -Path (Join-Path $concurrentCache $hashB)) | Should -Be $hashB
        $index = Get-Content -LiteralPath (Join-Path $concurrentCache 'asset-cache-index.json') -Raw | ConvertFrom-Json
        @($index.entries).Count | Should -Be 2
        @($index.entries | ForEach-Object { [string]$_.sha256 }) | Should -Contain $hashA
        @($index.entries | ForEach-Object { [string]$_.sha256 }) | Should -Contain $hashB
    }
}
