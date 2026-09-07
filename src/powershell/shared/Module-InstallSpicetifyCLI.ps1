function Module-InstallSpicetifyCLI {
    $v3Conflict = Get-SpicetifyV3Conflict
    if ($v3Conflict.IsConflict) {
        throw $v3Conflict.Message
    }
    $integration = Get-SpicetifyIntegrationContext
    $ver = $global:PinnedReleases.SpicetifyCLI.Version
    Write-Log "Installing Spicetify CLI v$ver..." -Level 'STEP'

    $installParent = Split-Path -Path $integration.InstallDirectory -Parent
    if (-not (Test-Path -LiteralPath $installParent -PathType Container)) {
        New-Item -Path $installParent -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }
    $configDirectory = if ($integration.PSObject.Properties['ConfigDirectory']) {
        [string]$integration.ConfigDirectory
    } else {
        Split-Path -Path $integration.ThemesDirectory -Parent
    }
    $configPath = if ($integration.PSObject.Properties['ConfigPath']) {
        [string]$integration.ConfigPath
    } else {
        Join-Path $configDirectory 'config-xpui.ini'
    }
    if (-not (Test-Path -LiteralPath $configDirectory -PathType Container)) {
        New-Item -Path $configDirectory -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }
    $allowedRoots = @($installParent, $configDirectory)
    $transactionPath = Join-Path $configDirectory '.librespot-package-cli.transaction.json'
    Resolve-LibreSpotPackageTransaction -TransactionPath $transactionPath -AllowedRoots $allowedRoots | Out-Null

    $arch = switch ($env:PROCESSOR_ARCHITECTURE) { 'ARM64' {'arm64'} default {'x64'} }
    $zip = $global:URL_SPICETIFY_FMT -f $ver, $arch
    $zp = New-LibreSpotTempFile -Name 'spicetify.zip'
    $transactionId = [Guid]::NewGuid().ToString('N')
    $stagePath = Join-Path $installParent ('.librespot-package-' + $transactionId + '-cli-stage')
    try {
        $expectedHash = $global:PinnedReleases.SpicetifyCLI.SHA256[$arch]
        if (-not (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zp -Label "Spicetify CLI ($arch)")) {
            try {
                Download-FileSafe -Uri $zip -OutFile $zp
            } catch {
                if (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zp -Label "Spicetify CLI ($arch)") {
                    Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                } else { throw }
            }
            Confirm-FileHash -Path $zp -ExpectedHash $expectedHash -Label "Spicetify CLI ($arch)"
            $attestation = Test-SpicetifyCliAttestation -Path $zp -Attestation $global:PinnedReleases.SpicetifyCLI.Attestation
            switch ($attestation) {
                'Verified' { Write-Log 'Spicetify CLI build provenance verified via GitHub attestation.' }
                'Mismatch' { Write-Log "Spicetify CLI GitHub attestation did not verify against the pinned signer identity ($($global:PinnedReleases.SpicetifyCLI.Attestation.Repo)). The SHA256 hash matched the pin, so the install proceeds on the verified hash, but provenance could not be confirmed - re-verify the pin if this persists." -Level 'WARN' }
                default    { }
            }
            Save-ToAssetCache -SourcePath $zp -SHA256Hash $expectedHash -Label "Spicetify CLI ($arch)" -SourceUrl $zip
        }

        New-Item -Path $stagePath -ItemType Directory -Force -ErrorAction Stop | Out-Null
        Expand-ArchiveSafely -ZipPath $zp -DestinationPath $stagePath -Label 'Spicetify CLI'
        $stagedCliPath = Join-Path $stagePath 'spicetify.exe'
        if (-not (Test-Path -LiteralPath $stagedCliPath -PathType Leaf)) {
            throw 'spicetify.exe not found in the verified staging directory - ZIP may be corrupted.'
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
                    TargetPath = $integration.InstallDirectory
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
                Write-Log 'Generating config...'
                Invoke-SpicetifyCli -Arguments @('config', '--bypass-admin') -FailureMessage 'Could not generate the initial Spicetify config.'
            } | Out-Null

        $null = Add-PathEntry -Entry $integration.InstallDirectory -Scope 'Process'
        if (Add-PathEntry -Entry $integration.InstallDirectory -Scope 'User') {
            Write-Log 'Added Spicetify to user PATH.'
        }
        Write-Log "Spicetify CLI v$ver installed."
    } finally {
        Remove-Item -LiteralPath $zp -Force -ErrorAction SilentlyContinue
        if (-not (Test-Path -LiteralPath $transactionPath) -and (Test-Path -LiteralPath $stagePath)) {
            try { Remove-LibreSpotPackagePathSafely -Path $stagePath | Out-Null } catch { Write-Log "Could not clean the failed CLI staging directory: $($_.Exception.Message)" -Level 'WARN' }
        }
    }
}
