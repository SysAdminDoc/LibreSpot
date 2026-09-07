function Module-InstallThemes { param($Config)
    $tn = [string]$Config.Spicetify_Theme
    if ($tn -eq '(None - Marketplace Only)') { Write-Log 'No theme selected.'; return }
    Write-Log "Installing theme: $tn..." -Level 'STEP'

    $integration = Get-SpicetifyIntegrationContext
    $td = $integration.ThemesDirectory
    $configDirectory = if ($integration.PSObject.Properties['ConfigDirectory']) { [string]$integration.ConfigDirectory } else { Split-Path -Path $td -Parent }
    $configPath = if ($integration.PSObject.Properties['ConfigPath']) { [string]$integration.ConfigPath } else { Join-Path $configDirectory 'config-xpui.ini' }
    if (-not (Test-Path -LiteralPath $configDirectory -PathType Container)) {
        New-Item -Path $configDirectory -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }
    if (-not (Test-Path -LiteralPath $td -PathType Container)) {
        New-Item -Path $td -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }
    $allowedRoots = @($td, $configDirectory)
    $transactionPath = Join-Path $configDirectory '.librespot-package-theme.transaction.json'
    Resolve-LibreSpotPackageTransaction -TransactionPath $transactionPath -AllowedRoots $allowedRoots | Out-Null

    $isBundled = ($null -ne $global:BundledThemes) -and $global:BundledThemes.Contains($tn)
    $isCommunity = ($null -ne $global:CommunityThemeRepos) -and $global:CommunityThemeRepos.ContainsKey($tn)
    $transactionId = [Guid]::NewGuid().ToString('N')
    $safeName = ($tn -replace '[^a-zA-Z0-9_-]', '_')
    $stagePath = Join-Path $td ('.librespot-package-' + $transactionId + '-theme-' + $safeName + '-stage')
    $tz = $null
    $tu = $null

    try {
        if ($isBundled) {
            # Bundled themes are copied into the target-volume staging directory
            # after every pinned source file has been checked.
            $bundle = $global:BundledThemes[$tn]
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
                $bundleRoots.Add([string](Join-Path $env:LIBRESPOT_BUNDLED_ASSETS 'themes'))
            }
            if (-not [string]::IsNullOrWhiteSpace($bundleScriptRoot)) {
                $bundleRoots.Add([string](Join-Path $bundleScriptRoot 'themes'))
                $bundleRoots.Add([string](Join-Path $bundleScriptRoot 'resources\themes'))
            }

            $src = ''
            foreach ($bundleRoot in $bundleRoots) {
                $candidate = Join-Path $bundleRoot ([string]$bundle.Folder)
                if (-not (Test-Path -LiteralPath $candidate -PathType Container)) { continue }
                $verified = $true
                foreach ($fileName in @($bundle.Files.Keys)) {
                    $filePath = Join-Path $candidate $fileName
                    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
                        Write-Log "  Bundled theme copy $candidate is missing $fileName. Ignoring it." -Level 'WARN'
                        $verified = $false
                        break
                    }
                    try { $actualHash = Get-FileSha256Lower -Path $filePath } catch {
                        Write-Log "  Bundled theme file $filePath could not be read: $($_.Exception.Message)." -Level 'WARN'
                        $verified = $false
                        break
                    }
                    if ($actualHash -ne ([string]$bundle.Files[$fileName]).ToLowerInvariant()) {
                        Write-Log "  Bundled theme file $filePath does not match the pinned hash. Ignoring it." -Level 'WARN'
                        $verified = $false
                        break
                    }
                }
                if ($verified) { $src = $candidate; break }
            }
            if ([string]::IsNullOrWhiteSpace($src)) {
                throw "No verified bundled copy of '$tn' was found. Looked in: $($bundleRoots -join '; ')."
            }

            New-Item -Path $stagePath -ItemType Directory -Force -ErrorAction Stop | Out-Null
            foreach ($fileName in @($bundle.Files.Keys)) {
                $stageFile = Join-Path $stagePath $fileName
                $stageParent = Split-Path -Path $stageFile -Parent
                if (-not (Test-Path -LiteralPath $stageParent -PathType Container)) {
                    New-Item -Path $stageParent -ItemType Directory -Force -ErrorAction Stop | Out-Null
                }
                Copy-Item -LiteralPath (Join-Path $src $fileName) -Destination $stageFile -Force -ErrorAction Stop
            }
            Write-Log "Bundled theme '$tn' copied to target-volume staging."
        } elseif ($isCommunity) {
            $repo = $global:CommunityThemeRepos[$tn]
            $archiveUrl = "https://github.com/$($repo.Owner)/$($repo.Repo)/archive/$($repo.CommitSha).zip"
            $tz = New-LibreSpotTempFile -Name "community-theme-$safeName.zip"
            $tu = New-LibreSpotTempDirectory -Name "community-theme-$safeName-unpack"
            Write-Log "Downloading community theme from $($repo.Owner)/$($repo.Repo) @ $($repo.CommitSha.Substring(0,10))..."
            $themeHash = $repo.SHA256
            if (-not (Get-FromAssetCache -SHA256Hash $themeHash -DestinationPath $tz -Label "Community theme '$tn'")) {
                try {
                    Download-FileSafe -Uri $archiveUrl -OutFile $tz
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $themeHash -DestinationPath $tz -Label "Community theme '$tn'") {
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
                Confirm-FileHash -Path $tz -ExpectedHash $themeHash -Label "Community theme '$tn'"
                Save-ToAssetCache -SourcePath $tz -SHA256Hash $themeHash -Label "Community theme '$tn'" -SourceUrl $archiveUrl
            }
            Expand-ArchiveSafely -ZipPath $tz -DestinationPath $tu -Label "Community theme '$tn'"
            $root = Get-ChildItem -LiteralPath $tu -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $root) { throw "Community theme archive for '$tn' did not contain a root folder." }
            $src = if ($repo.ThemeFolder -eq '.') { $root.FullName } else { Join-Path $root.FullName $repo.ThemeFolder }
            if (-not (Test-Path -LiteralPath $src -PathType Container)) {
                throw "Theme folder '$($repo.ThemeFolder)' was not found in the $($repo.Owner)/$($repo.Repo) archive."
            }
            if (-not (Test-Path -LiteralPath (Join-Path $src 'color.ini') -PathType Leaf) -and
                -not (Test-Path -LiteralPath (Join-Path $src 'user.css') -PathType Leaf)) {
                throw "Community theme '$tn' archive does not contain color.ini or user.css - not a valid Spicetify theme."
            }

            New-Item -Path $stagePath -ItemType Directory -Force -ErrorAction Stop | Out-Null
            foreach ($themeFile in @('color.ini', 'user.css', 'theme.js', 'theme.script.js', 'assets', 'README.md')) {
                $sourceFile = Join-Path $src $themeFile
                if (-not (Test-Path -LiteralPath $sourceFile)) { continue }
                $stageFile = Join-Path $stagePath $themeFile
                if ((Get-Item -LiteralPath $sourceFile -Force).PSIsContainer) {
                    New-Item -Path $stageFile -ItemType Directory -Force -ErrorAction Stop | Out-Null
                    Copy-Item -Path (Join-Path $sourceFile '*') -Destination $stageFile -Recurse -Force -ErrorAction Stop
                } else {
                    Copy-Item -LiteralPath $sourceFile -Destination $stageFile -Force -ErrorAction Stop
                }
            }
            Write-Log "Community theme '$tn' copied to target-volume staging."
        } else {
            $tz = New-LibreSpotTempFile -Name 'themes.zip'
            $tu = New-LibreSpotTempDirectory -Name 'themes-unpack'
            $themesHash = $global:PinnedReleases.Themes.SHA256
            if (-not (Get-FromAssetCache -SHA256Hash $themesHash -DestinationPath $tz -Label 'Themes archive')) {
                try {
                    Download-FileSafe -Uri $global:URL_THEMES_REPO -OutFile $tz
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $themesHash -DestinationPath $tz -Label 'Themes archive') {
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
                Confirm-FileHash -Path $tz -ExpectedHash $themesHash -Label 'Themes archive'
                Save-ToAssetCache -SourcePath $tz -SHA256Hash $themesHash -Label 'Themes archive' -SourceUrl $global:URL_THEMES_REPO
            }
            Expand-ArchiveSafely -ZipPath $tz -DestinationPath $tu -Label 'Themes archive'
            $root = Get-ChildItem -LiteralPath $tu -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $root) { throw 'Theme archive did not contain an unpacked root folder.' }
            $src = Join-Path $root.FullName $tn
            if (-not (Test-Path -LiteralPath $src -PathType Container)) {
                throw "Theme '$tn' was not found in the pinned theme archive."
            }
            New-Item -Path $stagePath -ItemType Directory -Force -ErrorAction Stop | Out-Null
            Copy-Item -Path (Join-Path $src '*') -Destination $stagePath -Recurse -Force -ErrorAction Stop
            Write-Log "Theme '$tn' copied to target-volume staging."
        }

        if (-not (Test-Path -LiteralPath $stagePath -PathType Container)) {
            throw 'Nothing was written to the theme staging directory.'
        }
        $stagedEntries = @(Get-ChildItem -LiteralPath $stagePath -Force -ErrorAction Stop)
        if ($stagedEntries.Count -eq 0) {
            throw 'The theme staging directory is empty.'
        }
        if ($isBundled) {
            foreach ($fileName in @($bundle.Files.Keys)) {
                $stagedFile = Join-Path $stagePath $fileName
                if (-not (Test-Path -LiteralPath $stagedFile -PathType Leaf) -or
                    (Get-FileSha256Lower -Path $stagedFile) -ne ([string]$bundle.Files[$fileName]).ToLowerInvariant()) {
                    throw "Bundled theme staging verification failed for '$fileName'."
                }
            }
        } elseif ($isCommunity -and
            -not (Test-Path -LiteralPath (Join-Path $stagePath 'color.ini') -PathType Leaf) -and
            -not (Test-Path -LiteralPath (Join-Path $stagePath 'user.css') -PathType Leaf)) {
            throw "Community theme '$tn' staging is missing color.ini and user.css."
        }
        $expectedFingerprint = Get-LibreSpotPackageFingerprint -Path $stagePath
        Invoke-LibreSpotPackageTransaction `
            -TransactionPath $transactionPath `
            -AllowedRoots $allowedRoots `
            -TransactionId $transactionId `
            -Packages @(
                [pscustomobject]@{
                    Action = 'swap'
                    Kind = 'directory'
                    TargetPath = Join-Path $td $tn
                    StagePath = $stagePath
                    ExpectedFingerprint = $expectedFingerprint
                },
                [pscustomobject]@{
                    Action = 'preserve'
                    Kind = 'file'
                    TargetPath = $configPath
                }
            ) `
            -Commit {
                $sc = $Config.Spicetify_Scheme
                Write-Log "Setting theme=$tn, scheme=$sc"
                Invoke-SpicetifyCli -Arguments @('config', 'current_theme', $tn, '--bypass-admin') -FailureMessage "Could not set Spicetify theme '$tn'."
                if (-not [string]::IsNullOrWhiteSpace($sc)) {
                    Invoke-SpicetifyCli -Arguments @('config', 'color_scheme', $sc, '--bypass-admin') -FailureMessage "Could not set color scheme '$sc'."
                }
                $needsThemeJs = $global:ThemesNeedingJS -contains $tn
                $jsVal = if ($needsThemeJs) { '1' } else { '0' }
                Invoke-SpicetifyCli -Arguments @('config', 'inject_css', '1', 'replace_colors', '1', 'overwrite_assets', '1', 'inject_theme_js', $jsVal, '--bypass-admin') -FailureMessage 'Could not enable the selected theme assets.'
            } | Out-Null
    } catch {
        Add-LibreSpotAssetInstallFailure -Kind 'Theme' -Name $tn -Reason "The theme could not be installed: $($_.Exception.Message)."
        return
    } finally {
        if ($tz) { $null = Remove-PathSafely -Path $tz -Label "Temporary theme archive '$tn'" }
        if ($tu) { $null = Remove-PathSafely -Path $tu -Label "Temporary theme extraction '$tn'" }
        if (-not (Test-Path -LiteralPath $transactionPath) -and (Test-Path -LiteralPath $stagePath)) {
            try { Remove-LibreSpotPackagePathSafely -Path $stagePath | Out-Null } catch { Write-Log "Could not clean the failed theme staging directory: $($_.Exception.Message)" -Level 'WARN' }
        }
    }
}
