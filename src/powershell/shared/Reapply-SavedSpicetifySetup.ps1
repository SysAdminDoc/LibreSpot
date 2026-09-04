function Reapply-SavedSpicetifySetup {
    [CmdletBinding(DefaultParameterSetName = 'Reapply')]
    param(
        $Config,
        [Parameter(ParameterSetName = 'SafeMode')][switch]$SafeMode,
        [Parameter(ParameterSetName = 'RestoreSafeMode')][switch]$RestoreSafeMode
    )

    $integration = Get-SpicetifyIntegrationContext
    $safeModeRoot = Join-Path $global:BACKUP_ROOT 'SafeMode'
    $statePath = Join-Path $global:CONFIG_DIR 'safe-mode-session.json'
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)

    $writeSafeModeMarker = {
        param(
            [Parameter(Mandatory = $true)][string]$Status,
            [Parameter(Mandatory = $true)][string]$SnapshotPath,
            [Parameter(Mandatory = $true)][string]$ManifestSha256
        )

        $marker = [ordered]@{
            schemaVersion = 2
            status = $Status
            snapshotPath = $SnapshotPath
            manifestSha256 = $ManifestSha256
        }
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR -PathType Container)) {
            New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force -ErrorAction Stop | Out-Null
        }
        $markerTempPath = "$statePath.tmp-$PID-$([Guid]::NewGuid().ToString('N'))"
        [System.IO.File]::WriteAllText($markerTempPath, ($marker | ConvertTo-Json -Depth 3), $utf8NoBom)
        Move-Item -LiteralPath $markerTempPath -Destination $statePath -Force -ErrorAction Stop
    }

    if ($RestoreSafeMode) {
        if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
            throw 'No recoverable LibreSpot safe-mode session was found.'
        }
        $stateFile = Get-Item -LiteralPath $statePath -Force -ErrorAction Stop
        if ($stateFile.Length -gt 4194304) {
            throw 'The safe-mode recovery marker is unexpectedly large. LibreSpot left it untouched for manual review.'
        }
        try {
            $markerJson = Get-Content -LiteralPath $statePath -Raw -ErrorAction Stop
            $marker = $markerJson | ConvertFrom-Json -ErrorAction Stop
        } catch {
            throw "The safe-mode recovery marker is unreadable. LibreSpot left it untouched for manual review. $($_.Exception.Message)"
        }
        $markerSchemaVersion = [int]$marker.schemaVersion
        if ($markerSchemaVersion -notin @(1, 2)) {
            throw "Unsupported safe-mode recovery schema version '$($marker.schemaVersion)'."
        }
        if ($markerSchemaVersion -eq 2) {
            $allowedMarkerProperties = @('schemaVersion', 'status', 'snapshotPath', 'manifestSha256')
            $unexpectedMarkerProperties = @($marker.PSObject.Properties.Name | Where-Object { $allowedMarkerProperties -notcontains $_ })
            if ($unexpectedMarkerProperties.Count -ne 0 -or
                ([string]$marker.status) -notin @('ReadyToEnter', 'Active') -or
                [string]::IsNullOrWhiteSpace([string]$marker.snapshotPath) -or
                [string]$marker.manifestSha256 -notmatch '^[0-9a-fA-F]{64}$') {
                throw 'The safe-mode recovery marker has unexpected or invalid fields. Recovery was refused before mutation.'
            }
        } elseif (([string]$marker.status) -notin @('ReadyToEnter', 'Active') -or
            [string]::IsNullOrWhiteSpace([string]$marker.snapshotPath)) {
            throw 'The legacy safe-mode recovery marker has invalid fields. Recovery was refused before mutation.'
        }

        $expectedRoot = [System.IO.Path]::GetFullPath($safeModeRoot).TrimEnd('\') + '\'
        $snapshotPath = [System.IO.Path]::GetFullPath([string]$marker.snapshotPath).TrimEnd('\')
        if (-not $snapshotPath.StartsWith($expectedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'The safe-mode snapshot path is outside LibreSpot backups. Recovery was refused before mutation.'
        }
        $manifestPath = Join-Path $snapshotPath 'safe-mode-manifest.json'
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
            throw 'The safe-mode snapshot manifest is missing. Recovery was refused before mutation.'
        }
        $manifestFile = Get-Item -LiteralPath $manifestPath -Force -ErrorAction Stop
        if ($manifestFile.Length -le 0 -or $manifestFile.Length -gt 4194304) {
            throw 'The safe-mode snapshot manifest has an invalid size. Recovery was refused before mutation.'
        }
        $actualManifestSha256 = Get-FileSha256Lower -Path $manifestPath
        $expectedManifestSha256 = if ($markerSchemaVersion -eq 2) {
            ([string]$marker.manifestSha256).ToLowerInvariant()
        } else {
            Get-FileSha256Lower -Path $statePath
        }
        if ($actualManifestSha256 -ne $expectedManifestSha256) {
            throw 'The safe-mode snapshot manifest failed SHA256 verification. Recovery was refused before mutation.'
        }
        try {
            $state = Get-Content -LiteralPath $manifestPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        } catch {
            throw "The safe-mode snapshot manifest is unreadable. Recovery was refused before mutation. $($_.Exception.Message)"
        }
        $allowedManifestProperties = @(
            'schemaVersion', 'operationId', 'createdAtUtc', 'snapshotPath', 'configSha256',
            'customAppsExisted', 'customAppsFileCount', 'customAppsBytes', 'customAppsFiles'
        )
        if ($markerSchemaVersion -eq 1) {
            $allowedManifestProperties += @('status', 'activatedAtUtc')
        }
        $unexpectedManifestProperties = @($state.PSObject.Properties.Name | Where-Object { $allowedManifestProperties -notcontains $_ })
        if ([int]$state.schemaVersion -ne $markerSchemaVersion -or $unexpectedManifestProperties.Count -ne 0 -or
            [System.IO.Path]::GetFullPath([string]$state.snapshotPath).TrimEnd('\') -ne $snapshotPath -or
            [string]$state.configSha256 -notmatch '^[0-9a-fA-F]{64}$' -or
            ($markerSchemaVersion -eq 1 -and ([string]$state.status) -ne ([string]$marker.status))) {
            throw 'The safe-mode snapshot manifest has unexpected or invalid fields. Recovery was refused before mutation.'
        }
        $configBackupPath = Join-Path $snapshotPath 'config-xpui.ini'
        $customAppsBackupPath = Join-Path $snapshotPath 'CustomApps'
        if (-not (Test-Path -LiteralPath $configBackupPath -PathType Leaf)) {
            throw 'The safe-mode config snapshot is missing. Recovery was refused before mutation.'
        }
        $expectedConfigHash = ([string]$state.configSha256).ToLowerInvariant()
        $actualConfigHash = Get-FileSha256Lower -Path $configBackupPath
        if ([string]::IsNullOrWhiteSpace($expectedConfigHash) -or $actualConfigHash -ne $expectedConfigHash) {
            throw 'The safe-mode config snapshot failed SHA256 verification. Recovery was refused before mutation.'
        }
        $snapshotFiles = @($state.customAppsFiles)
        if ([int]$state.customAppsFileCount -ne $snapshotFiles.Count -or
            ([long]$state.customAppsBytes -lt 0) -or
            (-not [bool]$state.customAppsExisted -and $snapshotFiles.Count -ne 0) -or
            ([bool]$state.customAppsExisted -and -not (Test-Path -LiteralPath $customAppsBackupPath -PathType Container))) {
            throw 'The safe-mode CustomApps manifest is inconsistent. Recovery was refused before mutation.'
        }
        $seenRelativePaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        [long]$verifiedCustomAppsBytes = 0
        foreach ($file in $snapshotFiles) {
            $relativePath = [string]$file.path
            if ([string]::IsNullOrWhiteSpace($relativePath) -or [System.IO.Path]::IsPathRooted($relativePath) -or
                $relativePath -match '(^|[\\/])\.\.([\\/]|$)' -or -not $seenRelativePaths.Add($relativePath) -or
                [string]$file.sha256 -notmatch '^[0-9a-fA-F]{64}$' -or [long]$file.bytes -lt 0) {
                throw 'The safe-mode CustomApps manifest contains an unsafe relative path. Recovery was refused before mutation.'
            }
            $sourceFile = [System.IO.Path]::GetFullPath((Join-Path $customAppsBackupPath $relativePath))
            $expectedCustomAppsRoot = [System.IO.Path]::GetFullPath($customAppsBackupPath).TrimEnd('\') + '\'
            if (-not $sourceFile.StartsWith($expectedCustomAppsRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
                -not (Test-Path -LiteralPath $sourceFile -PathType Leaf) -or
                (Get-Item -LiteralPath $sourceFile -Force -ErrorAction Stop).Length -ne [long]$file.bytes -or
                (Get-FileSha256Lower -Path $sourceFile) -ne ([string]$file.sha256).ToLowerInvariant()) {
                throw "The safe-mode CustomApps snapshot failed verification for '$relativePath'. Recovery was refused before mutation."
            }
            $verifiedCustomAppsBytes += [long]$file.bytes
        }
        $actualSnapshotFileCount = if (Test-Path -LiteralPath $customAppsBackupPath -PathType Container) {
            @(Get-ChildItem -LiteralPath $customAppsBackupPath -File -Recurse -Force -ErrorAction Stop).Count
        } else { 0 }
        if ($actualSnapshotFileCount -ne $snapshotFiles.Count -or $verifiedCustomAppsBytes -ne [long]$state.customAppsBytes) {
            throw 'The safe-mode CustomApps snapshot contains unverified content. Recovery was refused before mutation.'
        }

        if (-not (Test-SpicetifyCliInstalled)) {
            Write-Log 'Spicetify CLI is missing, so LibreSpot will reinstall it before restoring the safe-mode snapshot.' -Level 'WARN'
            Module-InstallSpicetifyCLI
        }

        Stop-SpotifyProcesses -MaxAttempts 3
        $configDirectory = Split-Path -Path $integration.ConfigPath -Parent
        New-Item -Path $configDirectory -ItemType Directory -Force -ErrorAction Stop | Out-Null
        Copy-Item -LiteralPath $configBackupPath -Destination $integration.ConfigPath -Force -ErrorAction Stop

        if (Test-Path -LiteralPath $integration.CustomAppsDirectory -PathType Container) {
            $null = Clear-DirectoryContentsSafely -Path $integration.CustomAppsDirectory -Label 'safe-mode CustomApps replacement'
            if (@(Get-ChildItem -LiteralPath $integration.CustomAppsDirectory -Force -ErrorAction SilentlyContinue).Count -gt 0) {
                throw 'LibreSpot could not clear the current CustomApps directory, so exact safe-mode recovery stopped before copying the snapshot.'
            }
        }
        if ([bool]$state.customAppsExisted) {
            New-Item -Path $integration.CustomAppsDirectory -ItemType Directory -Force -ErrorAction Stop | Out-Null
            $copyResult = Copy-DirectorySnapshotSafely -SourcePath $customAppsBackupPath -DestinationPath $integration.CustomAppsDirectory
            if ([int]$copyResult.SkippedReparsePoints -ne 0 -or [int]$copyResult.FileCount -ne $snapshotFiles.Count) {
                throw 'LibreSpot could not restore the complete CustomApps snapshot. The recovery marker was retained.'
            }
        } elseif (Test-Path -LiteralPath $integration.CustomAppsDirectory -PathType Container) {
            Remove-Item -LiteralPath $integration.CustomAppsDirectory -Force -ErrorAction Stop
        }

        if ((Get-FileSha256Lower -Path $integration.ConfigPath) -ne $expectedConfigHash) {
            throw 'The restored Spicetify config failed SHA256 verification. The recovery marker was retained.'
        }
        foreach ($file in $snapshotFiles) {
            $restoredFile = Join-Path $integration.CustomAppsDirectory ([string]$file.path)
            if (-not (Test-Path -LiteralPath $restoredFile -PathType Leaf) -or
                (Get-FileSha256Lower -Path $restoredFile) -ne ([string]$file.sha256).ToLowerInvariant()) {
                throw "The restored CustomApps file failed verification for '$($file.path)'. The recovery marker was retained."
            }
        }

        Module-ApplySpicetify -Config $Config -EvidenceSource 'RestoreSafeMode' | Out-Null
        Write-OperationJournalEntry -Phase 'safe-mode' -Target $snapshotPath -SafetyDecision 'Allowed' -Result 'Restored' -WouldChange $true -Reversible $false -RollbackHint 'Safe mode is no longer active; run it again to create a new recovery snapshot.' -TokenKind 'safeModeSession' -PreviousStateRef $snapshotPath -NewState 'restored' -UndoAction 'Start a new safe-mode session if another isolated launch is needed.' -Risk 'low' -Data @{
            restoredConfig = $true
            restoredCustomAppFiles = $snapshotFiles.Count
        }
        Remove-Item -LiteralPath $statePath -Force -ErrorAction Stop
        Write-Log "Safe mode ended. The exact config and $($snapshotFiles.Count) CustomApps file(s) were restored and reapplied." -Level 'SUCCESS'
        return [pscustomobject]@{ Status = 'Restored'; SnapshotPath = $snapshotPath; ConfigSha256 = $expectedConfigHash; CustomAppsFileCount = $snapshotFiles.Count }
    }

    $v3Conflict = Get-SpicetifyV3Conflict
    if ($v3Conflict.IsConflict) {
        throw $v3Conflict.Message
    }
    if (-not (Test-SpicetifyCliInstalled)) {
        Write-Log 'Spicetify CLI is missing, so LibreSpot will reinstall it before restoring your saved setup.' -Level 'WARN'
        Module-InstallSpicetifyCLI
    }

    if ($SafeMode) {
        if (Test-Path -LiteralPath $statePath -PathType Leaf) {
            throw 'A recoverable safe-mode session is already active. Restore that setup before starting another safe-mode launch.'
        }
        if (-not (Test-Path -LiteralPath $integration.ConfigPath -PathType Leaf)) {
            throw 'Spicetify config-xpui.ini was not found, so LibreSpot could not create a reversible safe-mode session.'
        }

        $operationId = if ($global:CURRENT_OPERATION_ID) { [string]$global:CURRENT_OPERATION_ID } else { [Guid]::NewGuid().ToString('N') }
        $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmssfff')
        $snapshotPath = Join-Path $safeModeRoot ("$stamp-" + $operationId.Substring(0, [Math]::Min(8, $operationId.Length)))
        $configBackupPath = Join-Path $snapshotPath 'config-xpui.ini'
        $customAppsBackupPath = Join-Path $snapshotPath 'CustomApps'
        $customAppsExisted = Test-Path -LiteralPath $integration.CustomAppsDirectory -PathType Container
        $mutationStarted = $false

        try {
            New-Item -Path $snapshotPath -ItemType Directory -Force -ErrorAction Stop | Out-Null
            Copy-Item -LiteralPath $integration.ConfigPath -Destination $configBackupPath -Force -ErrorAction Stop
            $copyResult = Copy-DirectorySnapshotSafely -SourcePath $integration.CustomAppsDirectory -DestinationPath $customAppsBackupPath
            if ([int]$copyResult.SkippedReparsePoints -ne 0) {
                throw 'CustomApps contains a reparse point, so LibreSpot could not guarantee an exact safe-mode restore.'
            }

            $customAppsFiles = @()
            if ($customAppsExisted) {
                $customAppsRootPrefix = [System.IO.Path]::GetFullPath($customAppsBackupPath).TrimEnd('\') + '\'
                $customAppsFiles = @(Get-ChildItem -LiteralPath $customAppsBackupPath -File -Recurse -Force -ErrorAction Stop | Sort-Object FullName | ForEach-Object {
                    [ordered]@{
                        path = $_.FullName.Substring($customAppsRootPrefix.Length)
                        bytes = [long]$_.Length
                        sha256 = Get-FileSha256Lower -Path $_.FullName
                    }
                })
            }

            $state = [ordered]@{
                schemaVersion = 2
                operationId = $operationId
                createdAtUtc = (Get-Date).ToUniversalTime().ToString('o')
                snapshotPath = $snapshotPath
                configSha256 = Get-FileSha256Lower -Path $configBackupPath
                customAppsExisted = $customAppsExisted
                customAppsFileCount = $customAppsFiles.Count
                customAppsBytes = [long]$copyResult.Bytes
                customAppsFiles = $customAppsFiles
            }
            $stateJson = $state | ConvertTo-Json -Depth 7
            $manifestPath = Join-Path $snapshotPath 'safe-mode-manifest.json'
            $manifestTempPath = "$manifestPath.tmp-$PID-$([Guid]::NewGuid().ToString('N'))"
            [System.IO.File]::WriteAllText($manifestTempPath, $stateJson, $utf8NoBom)
            Move-Item -LiteralPath $manifestTempPath -Destination $manifestPath -Force -ErrorAction Stop
            $manifestSha256 = Get-FileSha256Lower -Path $manifestPath
            & $writeSafeModeMarker -Status 'ReadyToEnter' -SnapshotPath $snapshotPath -ManifestSha256 $manifestSha256
            $mutationStarted = $true

            Stop-SpotifyProcesses -MaxAttempts 3
            $configText = [System.IO.File]::ReadAllText($integration.ConfigPath)
            foreach ($key in @('extensions', 'custom_apps')) {
                $pattern = "(?m)^(?<indent>[ \t]*)$([Regex]::Escape($key))[ \t]*=[^\r\n]*(?<ending>\r?)$"
                if (-not [Regex]::IsMatch($configText, $pattern)) {
                    throw "Spicetify config-xpui.ini does not contain '$key', so LibreSpot stopped before applying safe mode."
                }
                $configText = [Regex]::Replace($configText, $pattern, "`${indent}$key =`${ending}")
            }
            [System.IO.File]::WriteAllText($integration.ConfigPath, $configText, $utf8NoBom)
            if (@(Get-SpicetifyConfigListValue -Key 'extensions').Count -ne 0 -or
                @(Get-SpicetifyConfigListValue -Key 'custom_apps').Count -ne 0) {
                throw 'LibreSpot could not verify that extensions and custom apps were disabled before safe-mode apply.'
            }

            $applyPlan = Get-SpicetifyApplyPlan
            Invoke-SpicetifyCli -Arguments @($applyPlan.Arguments) -FailureMessage 'Could not apply the temporary safe-mode Spicetify configuration.'
            & $writeSafeModeMarker -Status 'Active' -SnapshotPath $snapshotPath -ManifestSha256 $manifestSha256

            Write-OperationJournalEntry -Phase 'safe-mode' -Target $snapshotPath -SafetyDecision 'Allowed' -Result 'Active' -WouldChange $true -Reversible $true -RollbackHint 'Use Restore my setup in Maintenance to restore and reapply this exact snapshot.' -TokenKind 'safeModeSession' -PreviousStateRef $snapshotPath -NewState 'extensions=; custom_apps=' -UndoAction 'Restore config-xpui.ini and CustomApps from the verified safe-mode snapshot, then reapply Spicetify.' -Risk 'low' -Data @{
                configSha256 = [string]$state.configSha256
                customAppsFileCount = $customAppsFiles.Count
                customAppsBytes = [long]$copyResult.Bytes
            }
            Write-Log "Safe mode is ready. Extensions and custom apps are disabled for this Spotify launch; recovery is saved at $snapshotPath" -Level 'SUCCESS'

            foreach ($oldSnapshot in @(Get-ChildItem -LiteralPath $safeModeRoot -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -Skip 5)) {
                $null = Remove-PathSafely -Path $oldSnapshot.FullName -Label 'expired safe-mode snapshot'
            }
            return [pscustomobject]@{ Status = 'Active'; SnapshotPath = $snapshotPath; ConfigSha256 = [string]$state.configSha256; CustomAppsFileCount = $customAppsFiles.Count }
        } catch {
            $safeModeError = $_
            if ($mutationStarted -and (Test-Path -LiteralPath $statePath -PathType Leaf)) {
                try {
                    Reapply-SavedSpicetifySetup -Config $Config -RestoreSafeMode | Out-Null
                    throw "Safe-mode preparation failed, and LibreSpot restored the original setup. $($safeModeError.Exception.Message)"
                } catch {
                    if ($_.Exception.Message -like 'Safe-mode preparation failed,*') { throw }
                    throw "Safe-mode preparation failed. Automatic recovery also failed, but the snapshot and recovery marker were retained. Entry error: $($safeModeError.Exception.Message) Recovery error: $($_.Exception.Message)"
                }
            }
            try { $null = Remove-PathSafely -Path $snapshotPath -Label 'incomplete safe-mode snapshot' } catch {}
            throw $safeModeError
        }
    }

    Invoke-WithSpicetifyStatePreservation -Action 'Reapply' -Operation {
        Module-InstallThemes -Config $Config
        Module-InstallExtensions -Config $Config
        Module-InstallMarketplace -Config $Config
        Module-InstallCustomApps -Config $Config
        Module-ApplySpicetify -Config $Config -EvidenceSource 'Reapply' | Out-Null
    } | Out-Null
}
