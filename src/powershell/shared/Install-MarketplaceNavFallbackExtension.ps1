function Install-MarketplaceNavFallbackExtension {
    # Spotify's global-nav redesigns periodically break the Spicetify CLI's
    # injected Marketplace nav link SILENTLY (regex mismatch in insertNavLink,
    # or the patched component variant never mounts - spicetify/marketplace
    # #1133/#1185/#1194). The store route still works, but users have no button.
    # This LibreSpot-managed extension waits for the app to settle and registers
    # a Spicetify.Topbar button ONLY when no Marketplace entry rendered, so the
    # store always stays reachable from the UI. Local-only; no network calls.
    # Idempotent; returns the extension file name.
    $integration = Get-SpicetifyIntegrationContext
    $extensionsDirectory = $integration.ExtensionsDirectory
    $fileName = 'librespot-marketplace-button.js'
    $filePath = Join-Path $extensionsDirectory $fileName

    $lines = @(
        '// LibreSpot Marketplace access button (fallback).',
        '// Registers a Topbar button only when the Spicetify-injected Marketplace',
        '// nav link failed to render (a recurring silent break across Spotify',
        '// global-nav redesigns). Managed by LibreSpot; removed when Marketplace',
        '// is disabled.',
        '(function libreSpotMarketplaceButton() {',
        '    if (window.__libreSpotMarketplaceButton) { return; }',
        '    window.__libreSpotMarketplaceButton = true;',
        '    var ICON = ''<svg role="img" height="16" width="16" viewBox="0 0 76.465 68.262" fill="currentColor"><path d="M151.909 72.923v6.5h10.097l8.663 44.567h48.968v-6.5h-43.61l-1.2-6.172h42.974l10.35-33.91h-59.915l-.872-4.485H151.91zm17.59 10.984h49.867l-6.393 20.91h-39.409l-4.064-20.91zm5.626 44.11a6.5 6.5 0 0 0-6.5 6.5 6.5 6.5 0 0 0 6.5 6.501 6.5 6.5 0 0 0 6.5-6.5 6.5 6.5 0 0 0-6.5-6.5zm38.274 0a6.5 6.5 0 0 0-6.5 6.5 6.5 6.5 0 0 0 6.5 6.501 6.5 6.5 0 0 0 6.5-6.5 6.5 6.5 0 0 0-6.5-6.5z" transform="translate(-151.909 -72.923)"/></svg>'';',
        '    function apiReady() {',
        '        return !!(window.Spicetify && Spicetify.Platform && Spicetify.Platform.History &&',
        '            Spicetify.Topbar && Spicetify.Topbar.Button);',
        '    }',
        '    function navEntryPresent() {',
        '        // The wrapper-rendered nav link, a sidebar item, or an earlier instance',
        '        // of this button. Covers the localized names Marketplace ships.',
        '        return !!document.querySelector(',
        '            ''a[href="/marketplace"], [aria-label="Marketplace"], [title="Marketplace"],'' +',
        '            '' [aria-label="Маркетплейс"], [title="Маркетплейс"]'');',
        '    }',
        '    function registerFallback() {',
        '        if (navEntryPresent()) { return; }',
        '        try {',
        '            new Spicetify.Topbar.Button("Marketplace", ICON, function () {',
        '                Spicetify.Platform.History.push("/marketplace");',
        '            });',
        '        } catch (error) {',
        '            // Topbar API drift: leave the UI untouched rather than break startup.',
        '        }',
        '    }',
        '    (function waitForApi(attempts) {',
        '        if (apiReady()) {',
        '            // Grace period so the native nav link (when it works) wins.',
        '            setTimeout(registerFallback, 4000);',
        '            return;',
        '        }',
        '        if (attempts > 0) { setTimeout(function () { waitForApi(attempts - 1); }, 300); }',
        '    })(200);',
        '})();'
    )
    $content = ($lines -join "`n") + "`n"

    New-Item -Path $extensionsDirectory -ItemType Directory -Force | Out-Null
    $needsWrite = $true
    if (Test-Path -LiteralPath $filePath -PathType Leaf) {
        try {
            if ([System.IO.File]::ReadAllText($filePath) -eq $content) { $needsWrite = $false }
        } catch {}
    }
    if ($needsWrite) {
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($filePath, $content, $utf8NoBom)
        Write-Log "Installed the Marketplace access-button fallback extension at $filePath"
    }
    return $fileName
}
