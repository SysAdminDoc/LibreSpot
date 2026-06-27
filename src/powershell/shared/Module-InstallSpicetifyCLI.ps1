function Module-InstallSpicetifyCLI {
    $ver = $global:PinnedReleases.SpicetifyCLI.Version
    Write-Log "Installing Spicetify CLI v$ver..." -Level 'STEP'
    New-Item -Path $global:SPICETIFY_DIR -ItemType Directory -Force | Out-Null
    $arch = switch ($env:PROCESSOR_ARCHITECTURE) { 'ARM64' {'arm64'} default {'x64'} }
    $zip = $global:URL_SPICETIFY_FMT -f $ver, $arch
    $zp = New-LibreSpotTempFile -Name 'spicetify.zip'
    try {
        $expectedHash = $global:PinnedReleases.SpicetifyCLI.SHA256[$arch]
        if (-not (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zp -Label "Spicetify CLI ($arch)")) {
            try {
                Download-FileSafe -Uri $zip -OutFile $zp
            } catch {
                if (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zp -Label "Spicetify CLI ($arch)") {
                    Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                } else { throw }
            }
            Confirm-FileHash -Path $zp -ExpectedHash $expectedHash -Label "Spicetify CLI ($arch)"
            Save-ToAssetCache -SourcePath $zp -SHA256Hash $expectedHash
        }
        if (Test-Path -LiteralPath $global:SPICETIFY_DIR) {
            $null = Clear-DirectoryContentsSafely -Path $global:SPICETIFY_DIR -Label 'Spicetify CLI'
        }
        Expand-ArchiveSafely -ZipPath $zp -DestinationPath $global:SPICETIFY_DIR -Label 'Spicetify CLI'
        $sExe = Join-Path $global:SPICETIFY_DIR "spicetify.exe"
        if (-not (Test-Path $sExe)) { throw "spicetify.exe not found after extraction - ZIP may be corrupted" }
        $null = Add-PathEntry -Entry $global:SPICETIFY_DIR -Scope 'Process'
        if (Add-PathEntry -Entry $global:SPICETIFY_DIR -Scope 'User') {
            Write-Log "Added Spicetify to user PATH."
        }
        Write-Log "Generating config..."
        Invoke-SpicetifyCli -Arguments @('config', '--bypass-admin') -FailureMessage 'Could not generate the initial Spicetify config.'
        Write-Log "Spicetify CLI v$ver installed."
    } finally {
        Remove-Item -LiteralPath $zp -Force -ErrorAction SilentlyContinue
    }
}
