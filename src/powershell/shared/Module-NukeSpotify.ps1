function Module-NukeSpotify {
    Write-Log "=== LibreSpot Comprehensive Spotify Uninstaller ===" -Level 'STEP'
    $rc = 0

    # --- Phase 1: Kill all Spotify processes ---
    Write-Log "[Phase 1/8] Terminating Spotify processes..."
    Stop-SpotifyProcesses

    # --- Phase 2: Remove Spotify Store (UWP/AppX) ---
    Write-Log "[Phase 2/8] Checking for Microsoft Store Spotify..."
    try {
        if ($PSVersionTable.PSVersion.Major -ge 7) { Import-Module Appx -UseWindowsPowerShell -WarningAction SilentlyContinue }
        $storeApp = Get-AppxPackage -Name "SpotifyAB.SpotifyMusic" -EA SilentlyContinue
        if ($storeApp) {
            Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
            $savedPP = $ProgressPreference; $ProgressPreference = 'SilentlyContinue'
            try {
                Remove-AppxPackage -Package $storeApp.PackageFullName -EA Stop
                Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
            } catch {
                Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry removal or reinstall from the Microsoft Store.'
                throw
            } finally { $ProgressPreference = $savedPP }
            Write-Log "  Removed Spotify Store app."; $rc++
        } else { Write-Log "  No Store version found." }
    } catch { Write-Log "  Store removal failed: $($_.Exception.Message)" -Level 'WARN' }

    # --- Phase 3: Run Spotify native uninstaller (silent) ---
    Write-Log "[Phase 3/8] Running native uninstaller..."
    $spotifyExe = Join-Path $env:APPDATA "Spotify\Spotify.exe"
    if (Test-Path $spotifyExe) {
        try {
            Unlock-SpotifyUpdateFolder
            $null = Start-Process -FilePath $spotifyExe -ArgumentList @('/UNINSTALL', '/SILENT') -Wait:$false -PassThru -ErrorAction Stop
            $deadline = (Get-Date).AddSeconds(15)
            while ((Get-Process -Name "SpotifyUninstall" -EA SilentlyContinue) -and (Get-Date) -lt $deadline) {
                Start-Sleep -Milliseconds 500
            }
            Start-Sleep -Milliseconds 500
            Write-Log "  Native uninstaller completed."; $rc++
        } catch { Write-Log "  Native uninstaller error: $($_.Exception.Message)" -Level 'WARN' }
    } else { Write-Log "  No native Spotify.exe found, skipping." }
    Stop-SpotifyProcesses -MaxAttempts 3

    # --- Phase 4: Nuke file system ---
    Write-Log "[Phase 4/8] Removing Spotify files and folders..."
    $desktopPath = Get-DesktopPath
    $filesToNuke = @(
        @{ Path = (Join-Path $env:APPDATA "Spotify");        Label = "Spotify Roaming (%APPDATA%)" }
        @{ Path = (Join-Path $env:LOCALAPPDATA "Spotify");   Label = "Spotify Local (%LOCALAPPDATA%)" }
        @{ Path = (Join-Path $env:APPDATA "spicetify");      Label = "Spicetify Config (%APPDATA%)" }
        @{ Path = (Join-Path $env:LOCALAPPDATA "spicetify"); Label = "Spicetify CLI (%LOCALAPPDATA%)" }
        @{ Path = (Join-Path $env:TEMP "SpotifyUninstall.exe"); Label = "Spotify uninstaller (TEMP)" }
        @{ Path = (Join-Path $desktopPath "Spotify.lnk");    Label = "Desktop shortcut" }
        @{ Path = (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Spotify.lnk"); Label = "Start Menu shortcut" }
    )
    foreach ($f in $filesToNuke) { $rc += Remove-PathSafely -Path $f.Path -Label $f.Label }

    # Glob targets: SpotX temp folders, Spotify installers, spicetify temp
    @(
        @{ Pattern = (Join-Path $env:TEMP "SpotX_Temp*");  Label = "SpotX temp" }
        @{ Pattern = (Join-Path $env:TEMP "Spotify_*");    Label = "Spotify temp installer" }
        @{ Pattern = (Join-Path $env:TEMP "spicetify*");   Label = "Spicetify temp" }
    ) | ForEach-Object {
        $lbl = $_.Label
        Get-ChildItem -Path $_.Pattern -EA SilentlyContinue | ForEach-Object {
            $rc += Remove-PathSafely -Path $_.FullName -Label "${lbl}: $($_.Name)"
        }
    }

    # IE/Edge cached Spotify installers
    $ieCache = Join-Path $env:LOCALAPPDATA "Microsoft\Windows\INetCache"
    if (Test-Path $ieCache) {
        Get-ChildItem -Path $ieCache -Recurse -Force -Filter "SpotifyFullSetup*" -EA SilentlyContinue | ForEach-Object {
            $rc += Remove-PathSafely -Path $_.FullName -Label "Cached installer: $($_.Name)"
        }
    }

    # --- Phase 5: Registry cleanup ---
    Write-Log "[Phase 5/8] Cleaning registry..."
    $regKeys = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Spotify"
        "HKCU:\Software\Spotify"
        "HKCU:\Software\Classes\spotify"
        "HKCU:\Software\Classes\spotify-client"
        "HKCU:\Software\Microsoft\Internet Explorer\Low Rights\ElevationPolicy\{5C0D11B8-C5F6-4be3-AD2C-2B1A3EB94AB6}"
        "HKCU:\Software\Microsoft\Internet Explorer\Low Rights\DragDrop\{5C0D11B8-C5F6-4be3-AD2C-2B1A3EB94AB6}"
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\Spotify.exe"
    )
    foreach ($key in $regKeys) {
        if (Test-Path $key) {
            Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Registry key cannot be automatically restored.'
            try {
                Remove-Item -Path $key -Recurse -Force -EA Stop
                Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Registry key cannot be automatically restored.'
                Write-Log "  Removed: $key"; $rc++
            } catch {
                Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry registry removal manually.'
                Write-Log "  Failed: $key" -Level 'WARN'
            }
        }
    }
    $regValues = @(
        @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"; Name = "Spotify" }
        @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"; Name = "Spotify Web Helper" }
    )
    foreach ($rv in $regValues) {
        if (Get-ItemProperty -Path $rv.Path -Name $rv.Name -EA SilentlyContinue) {
            $regTarget = "$($rv.Path)\$($rv.Name)"
            Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Registry value cannot be automatically restored.'
            try {
                Remove-ItemProperty -Path $rv.Path -Name $rv.Name -Force -EA Stop
                Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Registry value cannot be automatically restored.'
                Write-Log "  Removed startup: $($rv.Name)"; $rc++
            } catch {
                Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry registry removal manually.'
            }
        }
    }

    # --- Phase 6: Scheduled tasks ---
    Write-Log "[Phase 6/8] Removing scheduled tasks..."
    try {
        $spotifyTaskNames = @('SpotifyMigrator', 'SpotifyUpdateTask', 'Spotify')
        Get-ScheduledTask -EA SilentlyContinue |
            Where-Object { $_.TaskName -in $spotifyTaskNames -or $_.TaskName -like 'Spotify-*' } |
            ForEach-Object {
                Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Re-register the scheduled task manually if needed.'
                try {
                    Unregister-ScheduledTask -TaskName $_.TaskName -Confirm:$false -EA Stop
                    Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Re-register the scheduled task manually if needed.'
                    Write-Log "  Removed task: $($_.TaskName)"; $rc++
                } catch {
                    Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry scheduled task removal manually.'
                }
            }
    } catch { Write-Log "  Task cleanup skipped." }

    # --- Phase 7: Firewall rules ---
    Write-Log "[Phase 7/8] Removing firewall rules..."
    try {
        Get-NetFirewallRule -EA SilentlyContinue | Where-Object { $_.DisplayName -match 'Spotify' } | ForEach-Object {
            try { Remove-NetFirewallRule -Name $_.Name -EA Stop; Write-Log "  Removed firewall: $($_.DisplayName)"; $rc++ }
            catch {}
        }
    } catch { Write-Log "  Firewall cleanup skipped." }

    # --- Phase 8: Verification sweep ---
    Write-Log "[Phase 8/8] Verification sweep..."
    $survivors = @()
    @((Join-Path $env:APPDATA "Spotify"), (Join-Path $env:LOCALAPPDATA "Spotify")) | ForEach-Object {
        if (Test-Path $_) {
            Start-Sleep -Milliseconds 1500
            if (Remove-PathSafely -Path $_ -Label "Spotify cleanup retry") {
                $rc++
            } else {
                $survivors += $_
            }
        }
    }
    if ($survivors.Count -gt 0) {
        Write-Log "  Could not fully remove $($survivors.Count) path(s) (may need reboot):" -Level 'WARN'
        $survivors | ForEach-Object { Write-Log "    - $_" -Level 'WARN' }
    }

    Write-Log "=== Nuke complete: $rc items removed ===" -Level 'STEP'
}
