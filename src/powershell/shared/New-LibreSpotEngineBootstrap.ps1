function New-LibreSpotEngineBootstrap {
    param(
        [Parameter(Mandatory)][hashtable]$Config,
        [Parameter(Mandatory)][string]$SourcePath,
        [Parameter(Mandatory)][string]$DestinationPath
    )

    if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
        throw "LibreSpot companion extension source is missing: $SourcePath"
    }

    $engineProfile = $null
    if ($Config.ContainsKey('LibreSpot_EngineProfileJson') -and -not [string]::IsNullOrWhiteSpace([string]$Config.LibreSpot_EngineProfileJson)) {
        try {
            $engineProfile = [string]$Config.LibreSpot_EngineProfileJson | ConvertFrom-Json
        } catch {
            throw "The saved LibreSpot engine profile is not valid JSON: $($_.Exception.Message)"
        }
    }

    $featureOverrides = [ordered]@{}
    if ($Config.ContainsKey('LibreSpot_FeatureOverridesJson') -and -not [string]::IsNullOrWhiteSpace([string]$Config.LibreSpot_FeatureOverridesJson)) {
        try {
            $parsedOverrides = [string]$Config.LibreSpot_FeatureOverridesJson | ConvertFrom-Json
            foreach ($property in @($parsedOverrides.PSObject.Properties)) {
                $featureOverrides[$property.Name] = $property.Value
            }
        } catch {
            throw "The saved LibreSpot feature overrides are not valid JSON: $($_.Exception.Message)"
        }
    }

    $spotXSwitches = [ordered]@{}
    foreach ($key in @($Config.Keys | Where-Object { [string]$_ -like 'SpotX_*' } | Sort-Object)) {
        if ([string]$key -like 'SpotX_CustomPatchesSource*') { continue }
        $spotXSwitches[[string]$key] = $Config[$key]
    }

    $payload = [ordered]@{
        schemaVersion    = 1
        profile          = $engineProfile
        enabledSnippets  = @($Config.LibreSpot_EnabledSnippets)
        featureOverrides = $featureOverrides
        spotxSwitches    = $spotXSwitches
    }
    $payloadJson = $payload | ConvertTo-Json -Depth 32 -Compress
    $encoding = New-Object System.Text.UTF8Encoding($false)
    $payloadBytes = $encoding.GetBytes($payloadJson)
    if ($payloadBytes.Length -gt 262144) {
        throw "The LibreSpot engine bootstrap is too large ($($payloadBytes.Length) bytes)."
    }

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $revision = ([BitConverter]::ToString($sha.ComputeHash($payloadBytes))).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
    $base64 = [Convert]::ToBase64String($payloadBytes)
    $source = [System.IO.File]::ReadAllText($SourcePath, [System.Text.Encoding]::UTF8)
    $prefix = "window.__libreSpotDesktopBootstrap={payloadBase64:'$base64',revision:'$revision'};`n"
    $destinationDirectory = Split-Path $DestinationPath -Parent
    New-Item -Path $destinationDirectory -ItemType Directory -Force | Out-Null
    [System.IO.File]::WriteAllText($DestinationPath, $prefix + $source, $encoding)
    return [pscustomobject]@{
        Revision = $revision
        Bytes    = $payloadBytes.Length
        Path     = $DestinationPath
    }
}
