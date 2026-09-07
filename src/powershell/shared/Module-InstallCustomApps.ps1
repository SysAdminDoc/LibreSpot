function Module-InstallCustomApps { param($Config)
    $requestedApps = @($Config.Spicetify_CustomApps | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $managedApps = @($global:CommunityCustomApps.Keys)
    $managedCompanionExtensions = @($global:CommunityCustomApps.Values | ForEach-Object { [string]$_.CompanionExtension } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $integration = Get-SpicetifyIntegrationContext
    $customAppsDirectory = [string]$integration.CustomAppsDirectory
    $extensionsDirectory = [string]$integration.ExtensionsDirectory
    $configDirectory = if ($integration.PSObject.Properties['ConfigDirectory']) { [string]$integration.ConfigDirectory } else { Split-Path -Path $customAppsDirectory -Parent }
    $configPath = if ($integration.PSObject.Properties['ConfigPath']) { [string]$integration.ConfigPath } else { Join-Path $configDirectory 'config-xpui.ini' }

    foreach ($directory in @($configDirectory, $customAppsDirectory, $extensionsDirectory)) {
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
            New-Item -Path $directory -ItemType Directory -Force -ErrorAction Stop | Out-Null
        }
    }
    $allowedRoots = @($customAppsDirectory, $extensionsDirectory, $configDirectory)
    $transactionPath = Join-Path $configDirectory '.librespot-package-custom-apps.transaction.json'
    Resolve-LibreSpotPackageTransaction -TransactionPath $transactionPath -AllowedRoots $allowedRoots | Out-Null

    if ($requestedApps.Count -eq 0) {
        Write-Log 'Custom apps: none selected. Removing LibreSpot-managed custom apps if present...' -Level 'STEP'
    } else {
        Write-Log "Custom apps: $($requestedApps -join ', ')..." -Level 'STEP'
    }

    $transactionId = [Guid]::NewGuid().ToString('N')
    $installedApps = [System.Collections.Generic.List[string]]::new()
    $installedCompanionExtensions = [System.Collections.Generic.List[string]]::new()
    $stagedApps = @{}
    $stagedCompanions = @{}
    $stagingPaths = [System.Collections.Generic.List[string]]::new()
    $failedRequestedApps = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $zipPaths = [System.Collections.Generic.List[string]]::new()
    $unpackPaths = [System.Collections.Generic.List[string]]::new()

    try {
        foreach ($appId in $requestedApps) {
            if (-not $global:CommunityCustomApps.Contains($appId)) {
                Add-LibreSpotAssetInstallFailure -Kind 'Custom app' -Name $appId -Reason 'LibreSpot does not know this custom app.'
                $null = $failedRequestedApps.Add($appId)
                continue
            }

            $info = $global:CommunityCustomApps[$appId]
            $safeName = ($appId -replace '[^a-zA-Z0-9_-]', '_')
            $zipPath = New-LibreSpotTempFile -Name "custom-app-$safeName.zip"
            $unpackPath = New-LibreSpotTempDirectory -Name "custom-app-$safeName-unpack"
            $zipPaths.Add($zipPath)
            $unpackPaths.Add($unpackPath)
            $destinationPath = Join-Path $customAppsDirectory $appId
            $stagePath = Join-Path $customAppsDirectory ('.librespot-package-' + $transactionId + '-app-' + $safeName + '-stage')

            try {
                Write-Log "Installing custom app '$($info.DisplayName)' from $($info.Source)..."
                $expectedHash = [string]$info.SHA256
                $resolvedFromBundle = $false
                $bundledFileName = [string]$info.BundledFileName

                if ([bool]$info.Bundled -and -not [string]::IsNullOrWhiteSpace($bundledFileName)) {
                    $bundleScriptRoot = if (-not [string]::IsNullOrWhiteSpace($global:LibreSpotScriptRoot)) {
                        [string]$global:LibreSpotScriptRoot
                    } elseif (-not [string]::IsNullOrWhiteSpace($script:ScriptRoot)) {
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
                        try {
                            $bundlePath = Join-Path $bundleRoot $bundledFileName
                            if (-not (Test-Path -LiteralPath $bundlePath -PathType Leaf)) { continue }
                            $bundleHash = Get-FileSha256Lower -Path $bundlePath
                            if ($bundleHash -ne $expectedHash.ToLowerInvariant()) {
                                Write-Log "  Bundled archive $bundlePath does not match the pinned hash for '$appId'. Ignoring it." -Level 'WARN'
                                continue
                            }
                            Copy-Item -LiteralPath $bundlePath -Destination $zipPath -Force -ErrorAction Stop
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
                        } | Select-Object -First 1
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

                New-Item -Path $stagePath -ItemType Directory -Force -ErrorAction Stop | Out-Null
                $stagingPaths.Add($stagePath)
                Copy-Item -Path (Join-Path $sourcePath '*') -Destination $stagePath -Recurse -Force -ErrorAction Stop
                $expectedFingerprint = Get-LibreSpotPackageFingerprint -Path $stagePath

                $companionExtension = [string]$info.CompanionExtension
                $companionStagePath = $null
                $companionFingerprint = $null
                if (-not [string]::IsNullOrWhiteSpace($companionExtension)) {
                    $safeCompanion = ($companionExtension -replace '[^a-zA-Z0-9_.-]', '_')
                    $companionStagePath = Join-Path $extensionsDirectory ('.librespot-package-' + $transactionId + '-companion-' + $safeName + '-' + $safeCompanion + '-stage')
                    $stagingPaths.Add($companionStagePath)
                    $sourceCompanion = Join-Path $stagePath $companionExtension
                    $bootstrap = New-LibreSpotEngineBootstrap -Config $Config -SourcePath $sourceCompanion -DestinationPath $companionStagePath
                    if (-not (Test-Path -LiteralPath $companionStagePath -PathType Leaf)) { throw "Companion extension '$companionExtension' was not written to staging." }
                    $companionFingerprint = Get-LibreSpotPackageFingerprint -Path $companionStagePath
                    $stagedCompanions[$companionExtension] = [pscustomobject]@{ Path = $companionStagePath; Fingerprint = $companionFingerprint }
                    $installedCompanionExtensions.Add($companionExtension)
                    Write-Log "Companion extension '$companionExtension' staged with desktop profile $($bootstrap.Revision.Substring(0, 12))."
                }

                $stagedApps[$appId] = [pscustomobject]@{ Path = $stagePath; Fingerprint = $expectedFingerprint; Companion = $companionExtension }
                $installedApps.Add($appId)
                Write-Log "Custom app '$($info.DisplayName)' is verified in target-volume staging."
            } catch {
                $null = $failedRequestedApps.Add($appId)
                Add-LibreSpotAssetInstallFailure -Kind 'Custom app' -Name $appId -Reason $_.Exception.Message
            }
        }

        $descriptors = [System.Collections.Generic.List[object]]::new()
        foreach ($appId in $managedApps) {
            $target = Join-Path $customAppsDirectory $appId
            if ($stagedApps.ContainsKey($appId)) {
                $staged = $stagedApps[$appId]
                $descriptors.Add([pscustomobject]@{ Action = 'swap'; Kind = 'directory'; TargetPath = $target; StagePath = $staged.Path; ExpectedFingerprint = $staged.Fingerprint })
            } elseif ($requestedApps -notcontains $appId) {
                $descriptors.Add([pscustomobject]@{ Action = 'remove'; Kind = 'directory'; TargetPath = $target })
            }
        }
        foreach ($extensionName in $managedCompanionExtensions) {
            $target = Join-Path $extensionsDirectory $extensionName
            $owners = @($global:CommunityCustomApps.GetEnumerator() | Where-Object { [string]$_.Value.CompanionExtension -eq $extensionName } | ForEach-Object { [string]$_.Key })
            $ownerFailed = @($owners | Where-Object { $failedRequestedApps.Contains($_) }).Count -gt 0
            $ownerRequested = @($owners | Where-Object { $requestedApps -contains $_ }).Count -gt 0
            if ($stagedCompanions.ContainsKey($extensionName)) {
                $staged = $stagedCompanions[$extensionName]
                $descriptors.Add([pscustomobject]@{ Action = 'swap'; Kind = 'file'; TargetPath = $target; StagePath = $staged.Path; ExpectedFingerprint = $staged.Fingerprint })
            } elseif (-not $ownerRequested -and -not $ownerFailed) {
                $descriptors.Add([pscustomobject]@{ Action = 'remove'; Kind = 'file'; TargetPath = $target })
            }
        }
        $descriptors.Add([pscustomobject]@{ Action = 'preserve'; Kind = 'file'; TargetPath = $configPath })

        Invoke-LibreSpotPackageTransaction `
            -TransactionPath $transactionPath `
            -AllowedRoots $allowedRoots `
            -TransactionId $transactionId `
            -Packages @($descriptors) `
            -Commit {
                Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @($installedApps) -ManagedItems $managedApps
                Sync-SpicetifyListSetting -Key 'extensions' -DesiredItems @($installedCompanionExtensions) -ManagedItems $managedCompanionExtensions
            } | Out-Null
    } finally {
        foreach ($zipPath in @($zipPaths)) { Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue }
        foreach ($unpackPath in @($unpackPaths)) {
            if (Test-Path -LiteralPath $unpackPath) {
                try { Remove-LibreSpotPackagePathSafely -Path $unpackPath | Out-Null } catch { Write-Log "Could not clean custom-app extraction $unpackPath`: $($_.Exception.Message)" -Level 'WARN' }
            }
        }
        if (-not (Test-Path -LiteralPath $transactionPath)) {
            foreach ($stagingPath in @($stagingPaths)) {
                if (Test-Path -LiteralPath $stagingPath) {
                    try { Remove-LibreSpotPackagePathSafely -Path $stagingPath | Out-Null } catch { Write-Log "Could not clean custom-app staging $stagingPath`: $($_.Exception.Message)" -Level 'WARN' }
                }
            }
        }
    }
}
