function New-SpotXCustomPatchesFile {
    param([hashtable]$Config)

    if (-not $Config -or -not $Config.ContainsKey('SpotX_CustomPatchesEnabled')) { return '' }
    if (-not [bool]$Config.SpotX_CustomPatchesEnabled) { return '' }

    $patchJson = if ($Config.ContainsKey('SpotX_CustomPatchesJson')) { [string]$Config.SpotX_CustomPatchesJson } else { '' }
    if ([string]::IsNullOrWhiteSpace($patchJson)) {
        throw 'Custom SpotX patches are enabled, but SpotX_CustomPatchesJson is empty.'
    }

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $byteCount = $utf8.GetByteCount($patchJson)
    if ($byteCount -gt 65536) {
        throw "Custom SpotX patches are $byteCount bytes; the maximum is 65536 bytes."
    }

    try {
        $null = $patchJson | ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw "Custom SpotX patches JSON is invalid: $($_.Exception.Message)"
    }

    $patchPath = New-LibreSpotTempFile -Name 'spotx-custom-patches.json'
    $patchDir = Split-Path -Path $patchPath -Parent
    if (-not (Test-Path -LiteralPath $patchDir)) {
        New-Item -ItemType Directory -Path $patchDir -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($patchPath, $patchJson, $utf8)
    return $patchPath
}
