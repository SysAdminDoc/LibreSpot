function Download-CommunityExtensions { param($Config)
    $exts = @($Config.Spicetify_Extensions)
    $extDir = (Get-SpicetifyIntegrationContext).ExtensionsDirectory
    if (-not (Test-Path $extDir)) { New-Item -Path $extDir -ItemType Directory -Force | Out-Null }
    $verifiedPaths = @()
    foreach ($ext in $exts) {
        if (-not $global:CommunityExtensions.Contains($ext)) { continue }
        $info = $global:CommunityExtensions[$ext]
        $destFile = Join-Path $extDir $ext
        try {
            Write-Log "Downloading community extension: $ext from $($info.Source)..."
            $extHash = $info.SHA256
            if (-not (Get-FromAssetCache -SHA256Hash $extHash -DestinationPath $destFile -Label "Community extension $ext")) {
                try {
                    Download-FileSafe -Uri $info.Url -OutFile $destFile
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $extHash -DestinationPath $destFile -Label "Community extension $ext") {
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
                # Sanity check: make sure we got JavaScript, not a 404 HTML page.
                # Read just the first 512 bytes to avoid loading a huge file.
                $head = Get-Content -LiteralPath $destFile -TotalCount 5 -ErrorAction SilentlyContinue
                $headStr = ($head -join "`n").TrimStart()
                if ($headStr -match '^<(!DOCTYPE|html)' -or $headStr -match '^404:') {
                    Remove-Item -LiteralPath $destFile -Force -ErrorAction SilentlyContinue
                    Write-Log "Community extension '$ext' downloaded but appears to be an HTML error page, not JavaScript. The URL may have changed. Skipping." -Level 'WARN'
                    continue
                }
                Confirm-FileHash -Path $destFile -ExpectedHash $extHash -Label "Community extension $ext"
                Save-ToAssetCache -SourcePath $destFile -SHA256Hash $extHash -Label "Community extension $ext" -SourceUrl $info.Url
            }
            Write-Log "Community extension '$ext' saved to $destFile"
            $verifiedPaths += $destFile
        } catch {
            Write-Log "Could not download community extension '$ext': $($_.Exception.Message). Skipping." -Level 'WARN'
        }
    }
    # A file LibreSpot just verified that has since vanished is the classic
    # antivirus-quarantine signal. Detect and guide; never auto-restore.
    $quarantineWarned = $false
    foreach ($vp in $verifiedPaths) {
        if (-not $quarantineWarned -and -not (Test-Path -LiteralPath $vp)) {
            $quarantineWarned = $true
            Write-Log (Get-QuarantineGuidance -What "The verified extension file '$(Split-Path -Leaf $vp)'") -Level 'WARN'
        }
    }
}
