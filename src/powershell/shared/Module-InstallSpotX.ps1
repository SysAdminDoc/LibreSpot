function Module-InstallSpotX { param($Config,$SyncHash)
    Write-Log "Installing SpotX v$($global:PinnedReleases.SpotX.Version)..." -Level 'STEP'
    $dest = New-LibreSpotTempFile -Name 'spotx_run.ps1'
    $customPatchesPath = ''
    try {
        $spotxHash = $global:PinnedReleases.SpotX.SHA256
        if (-not (Get-FromAssetCache -SHA256Hash $spotxHash -DestinationPath $dest -Label 'SpotX run.ps1')) {
            try {
                Download-FileSafe -Uri $global:URL_SPOTX -OutFile $dest
            } catch {
                if (Get-FromAssetCache -SHA256Hash $spotxHash -DestinationPath $dest -Label 'SpotX run.ps1') {
                    Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                } else { throw }
            }
            Confirm-FileHash -Path $dest -ExpectedHash $spotxHash -Label "SpotX run.ps1"
            Save-ToAssetCache -SourcePath $dest -SHA256Hash $spotxHash -Label 'SpotX run.ps1' -SourceUrl $global:URL_SPOTX
        }
        $baseParams = Build-SpotXParams -Config $Config
        $customPatchesPath = New-SpotXCustomPatchesFile -Config $Config
        $patchSuffix = ''
        if (-not [string]::IsNullOrWhiteSpace($customPatchesPath)) {
            $patchSuffix = " -CustomPatchesPath `"$customPatchesPath`""
            Write-Log "Custom SpotX patches staged at $customPatchesPath"
        }
        if (Test-Path $global:SPOTIFY_EXE_PATH) {
            $ver = (Get-Item $global:SPOTIFY_EXE_PATH).VersionInfo.FileVersion
            Write-Log "Spotify $ver detected - SpotX will verify version compatibility"
        } else {
            Write-Log "Spotify not installed - SpotX will download recommended version"
        }
        Write-Log "Params: $($baseParams + $patchSuffix)"
        if ($SyncHash) { $SyncHash.AllowSpotify = $true }
        try {
            # SpotX can fail inside its own downloader after LibreSpot already
            # hash-verified run.ps1 (timeout, Cloudflare-worker outage, or a
            # mirror flagged as phishing). Invoke-ExternalScriptIsolated tags
            # those with a [SpotX...] category. On a classified download
            # failure, retry exactly once through the SpotX mirror (or, for a
            # phishing-blocked mirror, without it) before surfacing the error.
            $spotxMirrorInUse = [bool]$Config.SpotX_Mirror
            $spotxAttempt = 0
            while ($true) {
                $spotxAttempt++
                try {
                    Invoke-ExternalScriptIsolated -FilePath $dest -Arguments ($baseParams + $patchSuffix) -ExpectedHash $spotxHash -Label 'SpotX run.ps1'
                    break
                } catch {
                    $spotxCategory = if ($_.Exception.Message -match '\[(SpotX\w+)\]') { $Matches[1] } else { $null }
                    $spotxRetry = if ($spotxCategory -and $spotxAttempt -eq 1) {
                        Get-SpotXDownloadRetryPlan -Category $spotxCategory -MirrorAlreadyUsed $spotxMirrorInUse
                    } else { $null }
                    if (-not $spotxRetry) { throw }
                    Write-Log $spotxRetry.Reason -Level 'WARN'
                    $hasMirror = $baseParams -match '(^|\s)-mirror(\s|$)'
                    if ($spotxRetry.UseMirror -and -not $hasMirror) {
                        $baseParams = ($baseParams.Trim() + ' -mirror').Trim()
                    } elseif ((-not $spotxRetry.UseMirror) -and $hasMirror) {
                        $baseParams = ($baseParams -replace '(^|\s)-mirror(\s|$)', ' ').Trim()
                    }
                    $spotxMirrorInUse = $spotxRetry.UseMirror
                }
            }
            # Verify SpotX patching succeeded
            if (-not (Test-Path $global:SPOTIFY_EXE_PATH)) {
                throw "SpotX failed - Spotify.exe not found at $global:SPOTIFY_EXE_PATH. Check the log above for errors."
            }
            $elfDll = Join-Path (Split-Path $global:SPOTIFY_EXE_PATH) "chrome_elf.dll"
            if (-not (Test-Path $elfDll)) {
                throw "Spotify installation is incomplete - chrome_elf.dll is missing. This usually means the Spotify download failed or was corrupted."
            }
            $patchedVer = (Get-Item $global:SPOTIFY_EXE_PATH).VersionInfo.FileVersion
            $verify = Get-SpotXPatchVerification -SpotifyExePath $global:SPOTIFY_EXE_PATH
            if ($verify.Verified) {
                Write-Log "Spotify $patchedVer patched and verified ($($verify.Signals -join '; '))." -Level 'SUCCESS'
            } else {
                Write-Log "Spotify ${patchedVer}: SpotX ran but the patch could not be verified. $($verify.Reason)" -Level 'WARN'
                Write-Log "If ads still play or the UI is blank, this Spotify build may resist SpotX patching (SpotX issue #760). Try Maintenance > Reapply, or Maintenance > Full Reset to start clean. As a fallback, enable 'Ad-block (Spicetify fallback)' in Custom Install to keep ad-blocking working at the Spicetify layer." -Level 'WARN'
            }
            Write-Log "Launching Spotify (hidden) to generate config files..."
            if (Test-Path $global:SPOTIFY_EXE_PATH) {
                Start-Process -FilePath 'explorer.exe' -ArgumentList "`"$global:SPOTIFY_EXE_PATH`""
                Start-Sleep -Milliseconds 800
                Hide-SpotifyWindows
            }
            $prefsPath = Join-Path $env:APPDATA "Spotify\prefs"
            $waited = 0; $maxWait = 45
            while ($waited -lt $maxWait) {
                if ((Test-Path $prefsPath) -and ((Get-Item $prefsPath).Length -gt 10)) {
                    Write-Log "Config files detected after ${waited}s."; break
                }
                Hide-SpotifyWindows
                Start-Sleep -Seconds 2; $waited += 2
            }
            if ($waited -ge $maxWait) { Write-Log "Timed out waiting for config (${maxWait}s). Continuing..." -Level 'WARN' }
            Start-Sleep -Seconds 3; Stop-SpotifyProcesses -maxAttempts 3
        } finally {
            if ($SyncHash) { $SyncHash.AllowSpotify = $false }
        }
    } finally {
        if (-not [string]::IsNullOrWhiteSpace($customPatchesPath)) {
            Remove-Item -LiteralPath $customPatchesPath -Force -ErrorAction SilentlyContinue
        }
        Remove-Item -LiteralPath $dest -Force -ErrorAction SilentlyContinue
    }
}
