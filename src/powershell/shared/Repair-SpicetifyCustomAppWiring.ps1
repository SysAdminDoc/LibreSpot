function Repair-SpicetifyCustomAppWiring {
    # Ports the Spicetify CLI's own custom-app injection (src/apply/apply.go,
    # insertCustomApp + insertCustomAppChunkMap) onto the bundle index.html
    # actually loads. Needed because SpotX serves the combined /xpui.js while
    # Spicetify v2.44.0 only patches xpui-modules.js / xpui-snapshot.js, leaving
    # the store route in files the page never executes (blank Marketplace).
    # Idempotent: returns without writing unless the live bundle is NotWired and
    # every required anchor matched. A pre-patch backup is kept alongside.
    param(
        [string]$AppName = 'marketplace',
        [string]$AppsDirectory
    )

    $wiring = Test-SpicetifyCustomAppRouteWiring -AppName $AppName -AppsDirectory $AppsDirectory
    if ($wiring.State -ne 'NotWired') {
        return [pscustomobject]@{ Status = $wiring.State; BundlePath = $wiring.BundlePath; Detail = $wiring.Detail }
    }
    if (-not $wiring.RouteBundlePresent) {
        return [pscustomobject]@{
            Status     = 'RouteBundleMissing'
            BundlePath = $wiring.BundlePath
            Detail     = "spicetify-routes-$AppName.js is missing from the bundle folder; run a full Spicetify apply first."
        }
    }

    $bundlePath = $wiring.BundlePath
    $text = [System.IO.File]::ReadAllText($bundlePath)
    $chunkName = "spicetify-routes-$AppName"

    # React.lazy chunk-loader anchor (CLI: customAppReactPatterns, same order).
    $reactPatterns = @(
        '([\w_\$][\w_\$\d]*(?:\(\))?)\.lazy\(\((?:\(\)=>|function\(\)\{return )(\w+)\.(\w+)\(["'']?[\w-]+["'']?\)\.then\(\w+\.bind\(\w+,["'']?[\w-]+["'']?\)\)\}?\)\)',
        '([\w_\$][\w_\$\d]*)\.lazy\(async\(\)=>\{(?:[^{}]|\{[^{}]*\})*await\s+(\w+)\.(\w+)\(["'']?[\w-]+["'']?\)\.then\(\w+\.bind\(\w+,["'']?[\w-]+["'']?\)\)',
        '([\w_\$][\w_\$\d]*(?:\(\))?)\.lazy\(async\(\)=>await\s+Promise\.all\(\[(\w+)\.(\w+)\(["'']?[\w-]+["'']?\)'
    )
    $reactMatch = $null
    foreach ($pattern in $reactPatterns) {
        $candidate = [regex]::Match($text, $pattern)
        if ($candidate.Success) { $reactMatch = $candidate; break }
    }

    # Route-table anchor: the /settings route (CLI: customAppElementPatterns).
    $elementMatch = [regex]::Match($text, '(\([\w$\.,]+\))\(([\w\.]+),\{path:"/settings(?:/[\w\*]+)?",?(element|children)?')

    if ((-not $reactMatch) -or (-not $elementMatch.Success)) {
        return [pscustomobject]@{
            Status     = 'AnchorsMissing'
            BundlePath = $bundlePath
            Detail     = 'The Spicetify injection anchors were not found in this Spotify build; the bundle was left untouched.'
        }
    }

    # Extend the lazy match to its balanced closing parenthesis, mirroring the
    # CLI's SeekToCloseParen (the async patterns match a prefix of the call).
    $lazyEnd = $reactMatch.Index + $reactMatch.Length
    $depth = 0
    for ($i = $reactMatch.Index; $i -lt $lazyEnd; $i++) {
        $ch = $text[$i]
        if ($ch -eq '(') { $depth++ } elseif ($ch -eq ')') { $depth-- }
    }
    while (($depth -gt 0) -and ($lazyEnd -lt $text.Length)) {
        $ch = $text[$lazyEnd]
        if ($ch -eq '(') { $depth++ } elseif ($ch -eq ')') { $depth-- }
        $lazyEnd++
    }

    $react = $reactMatch.Groups[1].Value
    $loader = $reactMatch.Groups[2].Value
    $loadFn = $reactMatch.Groups[3].Value
    $jsx = $elementMatch.Groups[1].Value
    $routeComp = $elementMatch.Groups[2].Value
    $prop = $elementMatch.Groups[3].Value
    $wildcard = ''
    if ([string]::IsNullOrEmpty($prop)) { $prop = 'children' }
    elseif ($prop -eq 'element') { $wildcard = '*' }

    $lazyDef = ',spicetifyApp0=' + $react + '.lazy((()=>' + $loader + '.' + $loadFn + '("' + $chunkName + '").then(' + $loader + '.bind(' + $loader + ',"' + $chunkName + '"))))'
    $routeElement = $jsx + '(' + $routeComp + ',{path:"/' + $AppName + '/' + $wildcard + '",pathV6:"/' + $AppName + '/*",' + $prop + ':' + $jsx + '(spicetifyApp0,{})}),'

    $inserts = New-Object System.Collections.Generic.List[object]
    $inserts.Add(@{ Index = $lazyEnd; Text = $lazyDef })
    $inserts.Add(@{ Index = $elementMatch.Index; Text = $routeElement })

    # Chunk-name -> URL maps (CLI: insertCustomAppChunkMap). Both runtimes fall
    # back to the raw chunk id for the URL, but the miniCss gate has no
    # fallback, so without its entry the store loads with no stylesheet.
    $mapEntry = '"' + $chunkName + '":"' + $chunkName + '",'
    foreach ($mapPattern in @('\.u=\w+=>""\+\(\(\{', '\.miniCssF=\w+=>""\+\(\(\{')) {
        $mapMatch = [regex]::Match($text, $mapPattern)
        if ($mapMatch.Success) { $inserts.Add(@{ Index = $mapMatch.Index + $mapMatch.Length; Text = $mapEntry }) }
    }
    $cssGate = [regex]::Match($text, '\.f\.miniCss=function\(\w+,\w+\).*?\(\{[0-9:,]+(?=\}\)\[\w+\])')
    if ($cssGate.Success) {
        $inserts.Add(@{ Index = $cssGate.Index + $cssGate.Length; Text = (',"' + $chunkName + '":1') })
    } else {
        Write-Log 'Could not find the miniCss chunk gate; the store may load without its stylesheet.' -Level 'WARN'
    }

    foreach ($insert in ($inserts | Sort-Object -Property @{ Expression = { [int]$_.Index } } -Descending)) {
        $text = $text.Insert([int]$insert.Index, [string]$insert.Text)
    }

    Copy-Item -LiteralPath $bundlePath -Destination "$bundlePath.librespot.bak" -Force
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($bundlePath, $text, $utf8NoBom)
    Write-Log "Wired the $AppName route into $(Split-Path $bundlePath -Leaf) (SpotX serves this bundle instead of the xpui-snapshot layout Spicetify patches)."
    return [pscustomobject]@{
        Status     = 'Patched'
        BundlePath = $bundlePath
        Detail     = "Injected the $chunkName route, lazy loader, and chunk-map entries into the live bundle."
    }
}
