function Install-MarketplacePlaceholderTheme {
    # The official Marketplace installer creates a placeholder theme at
    # Themes\marketplace\color.ini and points current_theme at it so the store
    # can write theme colors and CSS snippets into an active theme. Without an
    # active theme, spicetify leaves inject_css/replace_colors off and every
    # Marketplace theme or snippet install silently does nothing. Content is the
    # upstream placeholder verbatim (a single [Marketplace] section header).
    # Idempotent; returns the placeholder theme directory path.
    $integration = Get-SpicetifyIntegrationContext
    $themeDirectory = Join-Path $integration.ThemesDirectory 'marketplace'
    $colorIniPath = Join-Path $themeDirectory 'color.ini'
    $expectedContent = "[Marketplace]`n"

    New-Item -Path $themeDirectory -ItemType Directory -Force | Out-Null
    $needsWrite = $true
    if (Test-Path -LiteralPath $colorIniPath -PathType Leaf) {
        try {
            $existing = [System.IO.File]::ReadAllText($colorIniPath)
            if ($existing.Trim() -eq $expectedContent.Trim()) { $needsWrite = $false }
        } catch {}
    }
    if ($needsWrite) {
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($colorIniPath, $expectedContent, $utf8NoBom)
        Write-Log "Created Marketplace placeholder theme at $themeDirectory"
    }
    return $themeDirectory
}
