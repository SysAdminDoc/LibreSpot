function Module-InstallCustomApps { param($Config)
    $requestedApps = @($Config.Spicetify_CustomApps | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $managedApps = @($global:CommunityCustomApps.Keys)
    $integration = Get-SpicetifyIntegrationContext
    $customAppsDirectory = $integration.CustomAppsDirectory

    if ($requestedApps.Count -eq 0) {
        Write-Log 'Custom apps: none selected. Removing LibreSpot-managed custom apps if present...' -Level 'STEP'
        foreach ($appId in $managedApps) {
            $null = Remove-PathSafely -Path (Join-Path $customAppsDirectory $appId) -Label "Custom app $appId"
        }
        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @() -ManagedItems $managedApps
        return
    }

    Write-Log "Custom apps: $($requestedApps -join ', ')..." -Level 'STEP'
    New-Item -Path $customAppsDirectory -ItemType Directory -Force | Out-Null
    $installedApps = [System.Collections.Generic.List[string]]::new()

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
            Write-Log "Downloading custom app '$($info.DisplayName)' from $($info.Source)..."
            $expectedHash = [string]$info.SHA256
            if (-not (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zipPath -Label "Custom app $appId archive")) {
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

            foreach ($requiredFile in @('manifest.json', 'extension.js')) {
                if (-not (Test-Path -LiteralPath (Join-Path $sourcePath $requiredFile) -PathType Leaf)) {
                    throw "Custom app '$appId' is missing required file '$requiredFile'."
                }
            }

            $null = Remove-PathSafely -Path $destinationPath -Label "Custom app $appId"
            New-Item -Path $destinationPath -ItemType Directory -Force | Out-Null
            Copy-Item -Path (Join-Path $sourcePath '*') -Destination $destinationPath -Recurse -Force
            $installedApps.Add($appId)
            Write-Log "Custom app '$($info.DisplayName)' installed to $destinationPath"
        } catch {
            Write-Log "Could not install custom app '$appId': $($_.Exception.Message). Skipping." -Level 'WARN'
        } finally {
            Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $unpackPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @($installedApps) -ManagedItems $managedApps
}
