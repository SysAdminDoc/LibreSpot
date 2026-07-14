function Assert-LibreSpotExternalScriptDefenderPolicy {
    param(
        [Parameter(Mandatory)][System.IO.Stream]$Stream,
        [string]$Arguments = '',
        [string]$Label = 'script'
    )

    if (-not $Stream.CanSeek) {
        throw "$Label cannot be inspected for Microsoft Defender mutations. Refusing to run."
    }

    $Stream.Position = 0
    $reader = New-Object System.IO.StreamReader($Stream, [System.Text.Encoding]::UTF8, $true, 4096, $true)
    try {
        $content = $reader.ReadToEnd()
    } finally {
        $reader.Dispose()
        $Stream.Position = 0
    }

    $containsDefenderMutation = $content -match '(?i)\b(?:Add|Set)-MpPreference\b|-(?:ExclusionPath|ExclusionProcess)\b'
    if (-not $containsDefenderMutation) { return }

    $isSpotX = $Label -like 'SpotX*'
    $declaresOptOut = $content -match '(?i)\bdefender_exclusions_off\b'
    $passesOptOut = $Arguments -match '(?i)(?:^|\s)-defender_exclusions_off(?:\s|$)'
    if (-not $isSpotX -or -not $declaresOptOut -or -not $passesOptOut) {
        throw "$Label contains Microsoft Defender preference or exclusion commands without a proven, passed -defender_exclusions_off adapter. Refusing to run."
    }
}
