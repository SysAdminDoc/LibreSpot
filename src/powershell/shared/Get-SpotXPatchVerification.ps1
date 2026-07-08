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

    $spotifyDir = [System.IO.Path]::GetDirectoryName($SpotifyExePath)
    $appsDir    = Join-Path $spotifyDir 'Apps'
    $signals    = New-Object System.Collections.Generic.List[string]

    # SpotX backs up the original app bundle before patching. Current SpotX names
    # that backup Apps\xpui.bak; older SpotX builds used Apps\xpui.spa.bak. Either
    # one proves SpotX rewrote the bundle. (Checking only xpui.spa.bak produced a
    # false "patch could not be verified" warning on every successful install.)
    $hasXpuiBak    = Test-Path -LiteralPath (Join-Path $appsDir 'xpui.bak')
    $hasXpuiSpaBak = Test-Path -LiteralPath (Join-Path $appsDir 'xpui.spa.bak')
    $hasBackup     = $hasXpuiBak -or $hasXpuiSpaBak

    # The bundle is a packed xpui.spa, or an extracted Apps\xpui directory once
    # Spicetify has applied on top of the SpotX-patched client.
    $hasSpaBundle = Test-Path -LiteralPath (Join-Path $appsDir 'xpui.spa') -PathType Leaf
    $hasDirBundle = Test-Path -LiteralPath (Join-Path $appsDir 'xpui') -PathType Container
    $hasBundle    = $hasSpaBundle -or $hasDirBundle

    # SpotX also patches the native binaries and leaves durable .bak copies next to
    # Spotify.exe. Spicetify's later apply consumes/renames the xpui backup, but the
    # binary backups persist, so they corroborate a SpotX run after the fact.
    $hasBinBackup = (Test-Path -LiteralPath (Join-Path $spotifyDir 'Spotify.bak')) -or `
                    (Test-Path -LiteralPath (Join-Path $spotifyDir 'chrome_elf.dll.bak'))

    if ($hasXpuiBak)    { $signals.Add('xpui.bak (SpotX backed up the original bundle before patching)') }
    if ($hasXpuiSpaBak) { $signals.Add('xpui.spa.bak (legacy SpotX bundle backup)') }
    if ($hasBinBackup)  { $signals.Add('Spotify.bak/chrome_elf.dll.bak (SpotX patched the native binaries)') }
    if ($hasSpaBundle)  { $signals.Add('xpui.spa (Spotify app bundle present)') }
    elseif ($hasDirBundle) { $signals.Add('Apps\xpui (bundle extracted by Spicetify)') }
    $result.Signals = @($signals)

    if (($hasBackup -or $hasBinBackup) -and $hasBundle) {
        $result.Verified = $true
        $result.Status   = 'Verified'
        $result.Reason   = 'SpotX left a patched app bundle and a backup of the original, so the patch was applied.'
    }
    elseif ($hasBundle) {
        $result.Status = 'Unverified'
        $result.Reason = 'Spotify is present but no SpotX backup (Apps\xpui.bak or a patched-binary backup) was found, so the patch may not have been applied. Signature protection on newer Spotify builds can let SpotX exit cleanly without patching.'
    }
    else {
        $result.Status = 'Unverified'
        $result.Reason = 'The Spotify app bundle (Apps\xpui.spa) is missing, so SpotX patching could not be confirmed.'
    }

    return [pscustomobject]$result
}
