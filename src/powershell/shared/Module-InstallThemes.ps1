function Module-InstallThemes { param($Config)
    $tn = $Config.Spicetify_Theme; if ($tn -eq '(None - Marketplace Only)') { Write-Log "No theme selected."; return }
    Write-Log "Installing theme: $tn..." -Level 'STEP'
    $td = (Get-SpicetifyIntegrationContext).ThemesDirectory
    if (-not (Test-Path $td)) { New-Item -Path $td -ItemType Directory -Force | Out-Null }

    $isBundled = ($null -ne $global:BundledThemes) -and $global:BundledThemes.Contains($tn)
    $isCommunity = $global:CommunityThemeRepos.ContainsKey($tn)

    if ($isBundled) {
        # Bundled theme — LibreSpot writes this one itself, so it ships inside the
        # package and never touches the network. Every file is pinned, so a
        # truncated or edited copy is rejected rather than half-installed.
        $bundle = $global:BundledThemes[$tn]
        try {
            # PS2EXE leaves $PSScriptRoot empty, and the install runs in a worker
            # runspace where a script-scoped root is not visible either, so the
            # monolith publishes $global:LibreSpotScriptRoot and exports it. The
            # backend host has neither and relies on LIBRESPOT_BUNDLED_ASSETS,
            # which the desktop and CLI hosts set.
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
                    # A locked file (antivirus, a parallel run) throws out of the
                    # hash helper; treat it like a mismatch and try the next root.
                    $actualHash = ''
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

            $dst = Join-Path $td $tn
            if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
            New-Item -Path $dst -ItemType Directory -Force | Out-Null
            # Copy only the pinned files so the installed theme is exactly what was verified.
            foreach ($fileName in @($bundle.Files.Keys)) {
                Copy-Item -LiteralPath (Join-Path $src $fileName) -Destination (Join-Path $dst $fileName) -Force
            }
            Write-Log "Bundled theme '$tn' copied to $dst"
        } catch {
            Add-LibreSpotAssetInstallFailure -Kind 'Theme' -Name $tn -Reason "The bundled copy could not be installed: $($_.Exception.Message)."
            return
        }
    } elseif ($isCommunity) {
        # Community theme — download commit-pinned archive and verify hash
        $repo = $global:CommunityThemeRepos[$tn]
        $archiveUrl = "https://github.com/$($repo.Owner)/$($repo.Repo)/archive/$($repo.CommitSha).zip"
        $safeName = ($tn -replace '[^a-zA-Z0-9_-]','_')
        $tz = New-LibreSpotTempFile -Name "community-theme-$safeName.zip"
        $tu = New-LibreSpotTempDirectory -Name "community-theme-$safeName-unpack"
        try {
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
            # Verify the archive actually contains Spicetify theme files
            $hasColorIni = Test-Path -LiteralPath (Join-Path $src 'color.ini')
            $hasUserCss  = Test-Path -LiteralPath (Join-Path $src 'user.css')
            if (-not ($hasColorIni -or $hasUserCss)) {
                throw "Community theme '$tn' archive does not contain color.ini or user.css - not a valid Spicetify theme."
            }
            $dst = Join-Path $td $tn
            if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
            # Copy only theme-relevant files, not repo metadata (.git, .github, etc.)
            New-Item -Path $dst -ItemType Directory -Force | Out-Null
            $themeFiles = @('color.ini','user.css','theme.js','theme.script.js','assets','README.md')
            foreach ($tf in $themeFiles) {
                $tfSrc = Join-Path $src $tf
                if (Test-Path -LiteralPath $tfSrc) {
                    Copy-Item $tfSrc -Destination (Join-Path $dst $tf) -Recurse -Force
                }
            }
            Write-Log "Community theme '$tn' copied to $dst"
        } catch {
            Add-LibreSpotAssetInstallFailure -Kind 'Theme' -Name $tn -Reason "The download could not be installed: $($_.Exception.Message)."
            return
        } finally {
            Remove-Item -LiteralPath $tz -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $tu -Recurse -Force -ErrorAction SilentlyContinue
        }
    } else {
        # Official theme — extract from the pinned spicetify-themes archive
        $tz = New-LibreSpotTempFile -Name 'themes.zip'
        $tu = New-LibreSpotTempDirectory -Name 'themes-unpack'
        try {
            $themesHash = $global:PinnedReleases.Themes.SHA256
            if (-not (Get-FromAssetCache -SHA256Hash $themesHash -DestinationPath $tz -Label 'Themes archive')) {
                try {
                    Download-FileSafe -Uri $global:URL_THEMES_REPO -OutFile $tz
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $themesHash -DestinationPath $tz -Label 'Themes archive') {
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
                Confirm-FileHash -Path $tz -ExpectedHash $themesHash -Label "Themes archive"
                Save-ToAssetCache -SourcePath $tz -SHA256Hash $themesHash -Label 'Themes archive' -SourceUrl $global:URL_THEMES_REPO
            }
            Expand-ArchiveSafely -ZipPath $tz -DestinationPath $tu -Label 'Themes archive'
            $root = Get-ChildItem -LiteralPath $tu -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $root) { throw "Theme archive did not contain an unpacked root folder." }
            $src = Join-Path $root.FullName $tn
            if (-not (Test-Path -LiteralPath $src -PathType Container)) {
                throw "Theme '$tn' was not found in the pinned theme archive."
            }
            $dst = Join-Path $td $tn
            if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
            Copy-Item $src -Destination $dst -Recurse -Force
            Write-Log "Theme copied to $dst"
        } finally {
            Remove-Item -LiteralPath $tz -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $tu -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    if (-not (Test-Path (Join-Path $td $tn))) {
        # The copy reported no error and still left nothing behind. Returning
        # quietly here is what let a run with no theme report success.
        Add-LibreSpotAssetInstallFailure -Kind 'Theme' -Name $tn -Reason 'Nothing was written to the themes directory.'
        return
    }
    $sc = $Config.Spicetify_Scheme; Write-Log "Setting theme=$tn, scheme=$sc"
    Invoke-SpicetifyCli -Arguments @('config', 'current_theme', $tn, '--bypass-admin') -FailureMessage "Could not set Spicetify theme '$tn'."
    if (-not [string]::IsNullOrWhiteSpace($sc)) {
        Invoke-SpicetifyCli -Arguments @('config', 'color_scheme', $sc, '--bypass-admin') -FailureMessage "Could not set color scheme '$sc'."
    }
    $needsThemeJs = $global:ThemesNeedingJS -contains $tn
    $jsVal = if ($needsThemeJs) { "1" } else { "0" }
    Invoke-SpicetifyCli -Arguments @('config', 'inject_css', '1', 'replace_colors', '1', 'overwrite_assets', '1', 'inject_theme_js', $jsVal, '--bypass-admin') -FailureMessage 'Could not enable the selected theme assets.'
}
