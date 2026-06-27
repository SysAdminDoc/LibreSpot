function Get-SpotXPatchVerification {
    param([string]$SpotifyExePath = $global:SPOTIFY_EXE_PATH)

    $result = [ordered]@{
        Verified = $false
        Status   = 'Missing'   # Missing | Unverified | Verified
        Reason   = ''
        Signals  = @()
    }

    if ([string]::IsNullOrWhiteSpace($SpotifyExePath) -or -not (Test-Path -LiteralPath $SpotifyExePath)) {
        $result.Reason = 'Spotify.exe was not found, so SpotX could not have patched anything.'
        return [pscustomobject]$result
    }

    $spotifyDir = Split-Path -LiteralPath $SpotifyExePath -Parent
    $appsDir    = Join-Path $spotifyDir 'Apps'
    $signals    = New-Object System.Collections.Generic.List[string]

    $hasBackup = Test-Path -LiteralPath (Join-Path $appsDir 'xpui.spa.bak')
    $hasBundle = Test-Path -LiteralPath (Join-Path $appsDir 'xpui.spa')

    if ($hasBackup) { $signals.Add('xpui.spa.bak (SpotX backed up the original bundle before patching)') }
    if ($hasBundle) { $signals.Add('xpui.spa (Spotify app bundle present)') }
    $result.Signals = @($signals)

    if ($hasBackup -and $hasBundle) {
        $result.Verified = $true
        $result.Status   = 'Verified'
        $result.Reason   = 'SpotX left a patched xpui.spa and a backup of the original, so the patch was applied.'
    }
    elseif ($hasBundle) {
        $result.Status = 'Unverified'
        $result.Reason = 'Spotify is present but no SpotX backup (xpui.spa.bak) was found, so the patch may not have been applied. Signature protection on newer Spotify builds can let SpotX exit cleanly without patching.'
    }
    else {
        $result.Status = 'Unverified'
        $result.Reason = 'The Spotify app bundle (Apps\xpui.spa) is missing, so SpotX patching could not be confirmed.'
    }

    return [pscustomobject]$result
}
