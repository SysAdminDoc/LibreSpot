function Download-CommunityExtensions { param($Config)
    $exts = @($Config.Spicetify_Extensions)
    $extDir = (Get-SpicetifyIntegrationContext).ExtensionsDirectory
    if (-not (Test-Path $extDir)) { New-Item -Path $extDir -ItemType Directory -Force | Out-Null }
    $verifiedPaths = @()
    foreach ($ext in $exts) {
        if (-not $global:CommunityExtensions.Contains($ext)) { continue }
        $info = $global:CommunityExtensions[$ext]
        $destFile = Join-Path $extDir $ext
        $tempFile = Join-Path $extDir (".librespot-$ext.$PID.$([Guid]::NewGuid().ToString('N')).tmp")
        try {
            Write-Log "Downloading community extension: $ext from $($info.Source)..."
            $extHash = $info.SHA256
            $fromCache = Get-FromAssetCache -SHA256Hash $extHash -DestinationPath $tempFile -Label "Community extension $ext"
            if (-not $fromCache) {
                try {
                    Download-FileSafe -Uri $info.Url -OutFile $tempFile
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $extHash -DestinationPath $tempFile -Label "Community extension $ext") {
                        $fromCache = $true
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
            }
            # Sanity check: make sure we got JavaScript, not a 404 HTML page.
            # Read just the first 512 bytes to avoid loading a huge file.
            $head = Get-Content -LiteralPath $tempFile -TotalCount 5 -ErrorAction SilentlyContinue
            $headStr = ($head -join "`n").TrimStart()
            if ($headStr -match '^<(!DOCTYPE|html)' -or $headStr -match '^404:') {
                Add-LibreSpotAssetInstallFailure -Kind 'Extension' -Name $ext -Reason 'The download was an HTML error page, not JavaScript. The URL may have changed.'
                continue
            }
            Confirm-FileHash -Path $tempFile -ExpectedHash $extHash -Label "Community extension $ext"
            if (-not $fromCache) {
                Save-ToAssetCache -SourcePath $tempFile -SHA256Hash $extHash -Label "Community extension $ext" -SourceUrl $info.Url
            }
            Move-Item -LiteralPath $tempFile -Destination $destFile -Force
            Write-Log "Community extension '$ext' saved to $destFile"
            $verifiedPaths += $destFile
        } catch {
            Add-LibreSpotAssetInstallFailure -Kind 'Extension' -Name $ext -Reason "The download failed: $($_.Exception.Message)."
        } finally {
            Remove-Item -LiteralPath $tempFile -Force -ErrorAction SilentlyContinue
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
