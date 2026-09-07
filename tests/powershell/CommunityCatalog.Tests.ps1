Describe 'Community catalog cross-edition output' {
    BeforeAll {
        # Moves whenever schemas/community-assets.json or
        # schemas/theme-preview-manifest.json changes, because the page
        # publishes both manifests' hashes as trust evidence. Recompute with
        # tools\Build-CommunityCatalog.ps1 -GeneratedDate 2026-09-04, and
        # republish gh-pages in the same pass or -Validate stays red.
        $script:reviewedCatalogSha256 = '5e95459bdd21f52d2bd81961843e46dbe9c765066d2b985ca14664beeaf4149f'
        $script:catalogGenerator = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\tools\Build-CommunityCatalog.ps1')).Path
        $script:catalogTestRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("librespot-catalog-editions-{0}" -f ([guid]::NewGuid().ToString('N')))
        $script:windowsPowerShellOutput = Join-Path $script:catalogTestRoot 'windows-powershell'
        $script:powerShellOutput = Join-Path $script:catalogTestRoot 'powershell'
        New-Item -ItemType Directory -Path $script:catalogTestRoot -Force | Out-Null
    }

    AfterAll {
        Remove-Item -LiteralPath $script:catalogTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'writes identical files from the documented command under both engines' {
        $engines = @(
            [pscustomobject]@{
                Executable = (Get-Command powershell.exe -ErrorAction Stop).Source
                Arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass')
                Output = $script:windowsPowerShellOutput
            }
            [pscustomobject]@{
                Executable = (Get-Command pwsh.exe -ErrorAction Stop).Source
                Arguments = @('-NoProfile')
                Output = $script:powerShellOutput
            }
        )

        foreach ($engine in $engines) {
            $arguments = @($engine.Arguments) + @(
                '-File', $script:catalogGenerator,
                '-OutputDirectory', $engine.Output,
                '-GeneratedDate', '2026-09-04'
            )
            $commandOutput = & $engine.Executable @arguments 2>&1
            $LASTEXITCODE | Should -Be 0 -Because ($commandOutput -join [Environment]::NewLine)
        }

        foreach ($fileName in @('catalog.json', 'index.html', '404.html', 'README.md')) {
            $windowsPowerShellBytes = [System.IO.File]::ReadAllBytes((Join-Path $script:windowsPowerShellOutput $fileName))
            $powerShellBytes = [System.IO.File]::ReadAllBytes((Join-Path $script:powerShellOutput $fileName))
            [Convert]::ToBase64String($windowsPowerShellBytes) |
                Should -BeExactly ([Convert]::ToBase64String($powerShellBytes)) -Because "$fileName must be byte-identical"

            if ($fileName -eq 'catalog.json') {
                foreach ($bytes in @($windowsPowerShellBytes, $powerShellBytes)) {
                    $sha256 = [System.Security.Cryptography.SHA256]::Create()
                    try {
                        $actualSha256 = ([BitConverter]::ToString($sha256.ComputeHash($bytes)) -replace '-', '').ToLowerInvariant()
                    } finally {
                        $sha256.Dispose()
                    }
                    $actualSha256 | Should -BeExactly $script:reviewedCatalogSha256 -Because 'the canonical formatter must retain the reviewed PowerShell 7 bytes'
                }
            }
        }
    }
}
