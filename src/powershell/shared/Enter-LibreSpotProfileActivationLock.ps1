function Enter-LibreSpotProfileActivationLock {
    if (-not (Test-Path -LiteralPath $global:CONFIG_DIR -PathType Container)) {
        New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ($true) {
        try {
            return (New-Object System.IO.FileStream(
                $global:PROFILE_ACTIVATION_LOCK_PATH,
                [System.IO.FileMode]::OpenOrCreate,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::None,
                1,
                [System.IO.FileOptions]::None))
        } catch [System.IO.IOException] {
            if ([DateTime]::UtcNow -ge $deadline) {
                throw 'Timed out waiting for another LibreSpot profile activation to finish.'
            }
            Start-Sleep -Milliseconds 50
        }
    }
}
