function Test-SpicetifyCustomAppRouteWiring {
    # Spicetify v2.44.0 wires custom apps into xpui-modules.js and the chunk map
    # into xpui-snapshot.js, but SpotX repoints index.html at the combined
    # /xpui.js bundle so its own patches take effect. With both tools installed,
    # the Marketplace route can end up only in files the page never loads, and
    # /marketplace renders a permanently blank page with no console errors
    # (React.lazy suspends forever on a chunk the live runtime cannot start).
    # Detection only - Repair-SpicetifyCustomAppWiring performs the fix.
    # States: Wired | NotWired | NotApplicable (snapshot layout is live) |
    # Unknown (no extracted bundle or unrecognized layout).
    param(
        [string]$AppName = 'marketplace',
        [string]$AppsDirectory
    )
    if ([string]::IsNullOrWhiteSpace($AppsDirectory)) {
        if ([string]::IsNullOrWhiteSpace([string]$global:SPOTIFY_EXE_PATH)) {
            return [pscustomobject]@{
                State              = 'Unknown'
                BundlePath         = $null
                RouteBundlePresent = $false
                Detail             = 'Spotify install path is not resolved yet.'
            }
        }
        $spotifyDir = Split-Path $global:SPOTIFY_EXE_PATH -Parent
        $AppsDirectory = Join-Path $spotifyDir 'Apps'
    }
    $xpuiDir = Join-Path $AppsDirectory 'xpui'
    $indexPath = Join-Path $xpuiDir 'index.html'
    $chunkName = "spicetify-routes-$AppName"
    $result = [pscustomobject]@{
        State              = 'Unknown'
        BundlePath         = $null
        RouteBundlePresent = $false
        Detail             = ''
    }
    if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
        $result.Detail = 'No extracted xpui bundle yet (Spicetify has not applied).'
        return $result
    }
    $result.RouteBundlePresent = Test-Path -LiteralPath (Join-Path $xpuiDir "$chunkName.js") -PathType Leaf
    $indexHtml = [System.IO.File]::ReadAllText($indexPath)
    if ($indexHtml -match 'src="/xpui-snapshot\.js"') {
        $result.State = 'NotApplicable'
        $result.Detail = 'index.html loads the xpui-snapshot layout that the Spicetify CLI patches directly.'
        return $result
    }
    $bundlePath = Join-Path $xpuiDir 'xpui.js'
    $result.BundlePath = $bundlePath
    if (($indexHtml -notmatch 'src="/xpui\.js"') -or -not (Test-Path -LiteralPath $bundlePath -PathType Leaf)) {
        $result.Detail = 'Could not identify the live xpui bundle from index.html.'
        return $result
    }
    if (([System.IO.File]::ReadAllText($bundlePath)).Contains($chunkName)) {
        $result.State = 'Wired'
        $result.Detail = "The live bundle already routes $chunkName."
    } else {
        $result.State = 'NotWired'
        $result.Detail = "The live bundle never references $chunkName, so the $AppName page renders blank."
    }
    return $result
}
