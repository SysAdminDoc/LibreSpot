function Module-InstallCustomApps { param($Config)
    $requestedApps = @($Config.Spicetify_CustomApps | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $managedApps = @($global:CommunityCustomApps.Keys)
    $managedCompanionExtensions = @($global:CommunityCustomApps.Values | ForEach-Object { [string]$_.CompanionExtension } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $integration = Get-SpicetifyIntegrationContext
    $customAppsDirectory = $integration.CustomAppsDirectory

    if ($requestedApps.Count -eq 0) {
        Write-Log 'Custom apps: none selected. Removing LibreSpot-managed custom apps if present...' -Level 'STEP'
        foreach ($appId in $managedApps) {
            $null = Remove-PathSafely -Path (Join-Path $customAppsDirectory $appId) -Label "Custom app $appId"
        }
        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @() -ManagedItems $managedApps
        foreach ($extensionName in $managedCompanionExtensions) {
            $null = Remove-PathSafely -Path (Join-Path $integration.ExtensionsDirectory $extensionName) -Label "Companion extension $extensionName"
        }
        Sync-SpicetifyListSetting -Key 'extensions' -DesiredItems @() -ManagedItems $managedCompanionExtensions
        return
    }

    Write-Log "Custom apps: $($requestedApps -join ', ')..." -Level 'STEP'
    New-Item -Path $customAppsDirectory -ItemType Directory -Force | Out-Null
    $installedApps = [System.Collections.Generic.List[string]]::new()
    $installedCompanionExtensions = [System.Collections.Generic.List[string]]::new()

    foreach ($appId in $requestedApps) {
        if (-not $global:CommunityCustomApps.Contains($appId)) {
            Write-Log "Unknown custom app '$appId'. Skipping." -Level 'WARN'
            continue
        }

        $info = $global:CommunityCustomApps[$appId]
        $safeName = ($appId -replace '[^a-zA-Z0-9_-]', '_')
        $zipPath = New-LibreSpotTempFile -Name "custom-app-$safeName.zip"
        $unpackPath = New-LibreSpotTempDirectory -Name "custom-app-$safeName-unpack"
        $destinationPath = Join-Path $customAppsDirectory $appId

        try {
            Write-Log "Installing custom app '$($info.DisplayName)' from $($info.Source)..."
            $expectedHash = [string]$info.SHA256
            $resolvedFromBundle = $false
            $bundledFileName = [string]$info.BundledFileName

            # A bundled app ships with LibreSpot itself, so prefer the local copy over
            # any download. The desktop and CLI hosts extract it and point
            # LIBRESPOT_BUNDLED_ASSETS at the folder; the script lane looks beside
            # itself and in a source checkout.
            if ([bool]$info.Bundled -and -not [string]::IsNullOrWhiteSpace($bundledFileName)) {
                # PS2EXE leaves $PSScriptRoot empty, which is why the monolith computes
                # $script:ScriptRoot. Prefer it so the compiled LibreSpot.exe finds an
                # archive sitting beside it; the backend host has no such variable and
                # relies on LIBRESPOT_BUNDLED_ASSETS instead.
                $bundleScriptRoot = if (-not [string]::IsNullOrWhiteSpace($script:ScriptRoot)) {
                    [string]$script:ScriptRoot
                } elseif (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
                    [string]$PSScriptRoot
                } elseif (-not [string]::IsNullOrWhiteSpace($PSCommandPath)) {
                    Split-Path -Parent $PSCommandPath
                } else {
                    try { Split-Path -Parent ([System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName) } catch { '' }
                }

                $bundleRoots = [System.Collections.Generic.List[string]]::new()
                if (-not [string]::IsNullOrWhiteSpace($env:LIBRESPOT_BUNDLED_ASSETS)) {
                    $bundleRoots.Add([string]$env:LIBRESPOT_BUNDLED_ASSETS)
                }
                if (-not [string]::IsNullOrWhiteSpace($bundleScriptRoot)) {
                    $bundleRoots.Add($bundleScriptRoot)
                    $bundleRoots.Add([string](Join-Path $bundleScriptRoot 'resources\custom-apps'))
                }

                foreach ($bundleRoot in $bundleRoots) {
                    # A bundled copy is an optimisation, never a requirement: any failure
                    # reading or copying it must fall through to the cache and download
                    # rather than abandon the app. A locked file (antivirus, a parallel
                    # run) throws out of Get-FileSha256Lower.
                    try {
                        $bundlePath = Join-Path $bundleRoot $bundledFileName
                        if (-not (Test-Path -LiteralPath $bundlePath -PathType Leaf)) { continue }
                        $bundleHash = Get-FileSha256Lower -Path $bundlePath
                        if ($bundleHash -ne $expectedHash.ToLowerInvariant()) {
                            Write-Log "  Bundled archive $bundlePath does not match the pinned hash for '$appId'. Ignoring it." -Level 'WARN'
                            continue
                        }
                        Copy-Item -LiteralPath $bundlePath -Destination $zipPath -Force
                    } catch {
                        Write-Log "  Bundled archive $bundlePath could not be read: $($_.Exception.Message). Falling back to the cache and download." -Level 'WARN'
                        continue
                    }
                    Save-ToAssetCache -SourcePath $zipPath -SHA256Hash $expectedHash -Label "Custom app $appId archive" -SourceUrl $bundlePath
                    Write-Log "  Using the copy bundled with LibreSpot ($bundledFileName)."
                    $resolvedFromBundle = $true
                    break
                }
            }

            if (-not $resolvedFromBundle -and -not (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zipPath -Label "Custom app $appId archive")) {
                try {
                    Download-FileSafe -Uri $info.Url -OutFile $zipPath
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zipPath -Label "Custom app $appId archive") {
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
                Confirm-FileHash -Path $zipPath -ExpectedHash $expectedHash -Label "Custom app $appId"
                Save-ToAssetCache -SourcePath $zipPath -SHA256Hash $expectedHash -Label "Custom app $appId archive" -SourceUrl $info.Url
            }

            Expand-ArchiveSafely -ZipPath $zipPath -DestinationPath $unpackPath -Label "Custom app $appId" -MaxExpandedBytes 250MB
            $sourcePath = Join-Path $unpackPath ([string]$info.AssetPath)
            if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
                $candidate = Get-ChildItem -LiteralPath $unpackPath -Directory -ErrorAction SilentlyContinue |
                    Where-Object {
                        (Test-Path -LiteralPath (Join-Path $_.FullName 'manifest.json') -PathType Leaf) -and
                        (Test-Path -LiteralPath (Join-Path $_.FullName 'extension.js') -PathType Leaf)
                    } |
                    Select-Object -First 1
                if ($candidate) { $sourcePath = $candidate.FullName }
            }

            if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
                throw "Custom app archive did not contain expected folder '$($info.AssetPath)'."
            }

            $requiredFiles = if ($info.RequiredFiles) { @($info.RequiredFiles) } else { @('manifest.json', 'extension.js') }
            foreach ($requiredFile in $requiredFiles) {
                if (-not (Test-Path -LiteralPath (Join-Path $sourcePath $requiredFile) -PathType Leaf)) {
                    throw "Custom app '$appId' is missing required file '$requiredFile'."
                }
            }

            $null = Remove-PathSafely -Path $destinationPath -Label "Custom app $appId"
            New-Item -Path $destinationPath -ItemType Directory -Force | Out-Null
            Copy-Item -Path (Join-Path $sourcePath '*') -Destination $destinationPath -Recurse -Force
            $companionExtension = [string]$info.CompanionExtension
            if (-not [string]::IsNullOrWhiteSpace($companionExtension)) {
                $bootstrap = New-LibreSpotEngineBootstrap `
                    -Config $Config `
                    -SourcePath (Join-Path $destinationPath $companionExtension) `
                    -DestinationPath (Join-Path $integration.ExtensionsDirectory $companionExtension)
                $installedCompanionExtensions.Add($companionExtension)
                Write-Log "Companion extension '$companionExtension' staged with desktop profile $($bootstrap.Revision.Substring(0, 12))."
            }
            $installedApps.Add($appId)
            Write-Log "Custom app '$($info.DisplayName)' installed to $destinationPath"
        } catch {
            Write-Log "Could not install custom app '$appId': $($_.Exception.Message). Skipping." -Level 'WARN'
        } finally {
            Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $unpackPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    foreach ($extensionName in $managedCompanionExtensions) {
        if ($installedCompanionExtensions.Contains($extensionName)) { continue }
        $null = Remove-PathSafely -Path (Join-Path $integration.ExtensionsDirectory $extensionName) -Label "Companion extension $extensionName"
    }
    Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @($installedApps) -ManagedItems $managedApps
    Sync-SpicetifyListSetting -Key 'extensions' -DesiredItems @($installedCompanionExtensions) -ManagedItems $managedCompanionExtensions
}
