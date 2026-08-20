function Get-SpicetifyV3SupportContract {
    [CmdletBinding()]
    param(
        [string]$CliVersion,
        [string]$SpotifyVersion,
        [string[]]$SupportListPath
    )

    $major = Get-SpicetifyCliMajorVersion -Version $CliVersion
    if ($null -eq $major -or $major -le 2) {
        return [pscustomobject][ordered]@{
            FeatureActive = $false
            CliMajor = $major
            ListAvailable = $false
            Verdict = 'not-applicable'
            CanApply = $true
            CanAutoApply = $false
            NormalizedVersion = $null
            MapStatus = $null
            ClassmapKey = $null
            FallbackVersion = $null
            FallbackClassmapKey = $null
            SupportListPath = $null
            Reason = 'The v3 Spotify support contract is inactive for Spicetify 2.x.'
        }
    }

    $resolvedPath = @($SupportListPath | Where-Object {
        -not [string]::IsNullOrWhiteSpace([string]$_) -and
        (Test-Path -LiteralPath ([string]$_) -PathType Leaf)
    } | Select-Object -First 1)
    $resolvedPath = if ($resolvedPath.Count -gt 0) { [string]$resolvedPath[0] } else { $null }
    if ([string]::IsNullOrWhiteSpace($resolvedPath)) {
        return [pscustomobject][ordered]@{
            FeatureActive = $true
            CliMajor = $major
            ListAvailable = $false
            Verdict = 'unknown'
            CanApply = $false
            CanAutoApply = $false
            NormalizedVersion = $null
            MapStatus = $null
            ClassmapKey = $null
            FallbackVersion = $null
            FallbackClassmapKey = $null
            SupportListPath = $null
            Reason = "The v3 supported-versions document is unavailable, so LibreSpot refuses to mutate Spotify. Run 'spicetify restore' first, then reinstall the pinned Spicetify 2.x CLI."
        }
    }

    try {
        $document = Get-Content -Raw -LiteralPath $resolvedPath -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        if ([int]$document.schema_version -ne 2 -or [string]$document.policy -cne 'allowlist') {
            throw 'The v3 supported-versions document has an unsupported schema or policy.'
        }
        $defaultMapStatus = [string]$document.default_map_status
        if ($defaultMapStatus -notin @('classic', 'modular', 'none')) {
            throw 'The v3 supported-versions document has an invalid default map status.'
        }
    } catch {
        return [pscustomobject][ordered]@{
            FeatureActive = $true
            CliMajor = $major
            ListAvailable = $false
            Verdict = 'unknown'
            CanApply = $false
            CanAutoApply = $false
            NormalizedVersion = $null
            MapStatus = $null
            ClassmapKey = $null
            FallbackVersion = $null
            FallbackClassmapKey = $null
            SupportListPath = $resolvedPath
            Reason = "The v3 supported-versions document is malformed, so LibreSpot refuses to mutate Spotify. Run 'spicetify restore' first, then reinstall the pinned Spicetify 2.x CLI. $($_.Exception.Message)"
        }
    }

    $normalizeVersion = {
        param([string]$RawVersion)
        $match = [regex]::Match($RawVersion, '^\s*[vV]?(\d+)\.(\d+)\.(\d+)(?:[.\-+]|\s|$)')
        if (-not $match.Success) { return $null }
        return [Version]::new(
            [int]$match.Groups[1].Value,
            [int]$match.Groups[2].Value,
            [int]$match.Groups[3].Value)
    }

    $normalized = & $normalizeVersion $SpotifyVersion
    if ($null -eq $normalized) {
        return [pscustomobject][ordered]@{
            FeatureActive = $true
            CliMajor = $major
            ListAvailable = $true
            Verdict = 'unknown'
            CanApply = $true
            CanAutoApply = $false
            NormalizedVersion = $null
            MapStatus = $null
            ClassmapKey = $null
            FallbackVersion = $null
            FallbackClassmapKey = $null
            SupportListPath = $resolvedPath
            Reason = 'The installed Spotify version could not be normalized, so the v3 support check remains fail-open.'
        }
    }
    $normalizedText = $normalized.ToString(3)
    $allowlisted = @($document.versions | ForEach-Object { & $normalizeVersion ([string]$_) } | Where-Object {
        $null -ne $_ -and $_.CompareTo($normalized) -eq 0
    }).Count -gt 0
    if (-not $allowlisted) {
        foreach ($range in @($document.ranges)) {
            $minimum = & $normalizeVersion ([string]$range.min)
            $maximum = & $normalizeVersion ([string]$range.max)
            if ($null -ne $minimum -and $null -ne $maximum -and
                $minimum.CompareTo($normalized) -le 0 -and $maximum.CompareTo($normalized) -ge 0) {
                $allowlisted = $true
                break
            }
        }
    }

    $map = $null
    if ($document.maps -and $document.maps.PSObject.Properties[$normalizedText]) {
        $map = $document.maps.PSObject.Properties[$normalizedText].Value
    }
    if ($allowlisted) {
        return [pscustomobject][ordered]@{
            FeatureActive = $true
            CliMajor = $major
            ListAvailable = $true
            Verdict = 'allowlisted'
            CanApply = $true
            CanAutoApply = $true
            NormalizedVersion = $normalizedText
            MapStatus = if ($map.status) { [string]$map.status } else { $defaultMapStatus }
            ClassmapKey = if ($map.classmap_key) { [string]$map.classmap_key } else { $null }
            FallbackVersion = $null
            FallbackClassmapKey = $null
            SupportListPath = $resolvedPath
            Reason = 'The Spotify version is allowlisted by the v3 support contract.'
        }
    }

    $fallbacks = foreach ($mapProperty in @($document.maps.PSObject.Properties)) {
        $mapVersion = & $normalizeVersion ([string]$mapProperty.Name)
        if ($null -ne $mapVersion -and $mapProperty.Value.status -eq 'modular' -and
            $mapVersion.Major -eq $normalized.Major -and
            $mapVersion.Minor -eq $normalized.Minor -and
            $mapVersion.CompareTo($normalized) -lt 0) {
            [pscustomobject]@{
                Version = $mapVersion
                VersionText = $mapVersion.ToString(3)
                ClassmapKey = [string]$mapProperty.Value.classmap_key
            }
        }
    }
    $fallback = @($fallbacks | Sort-Object Version -Descending | Select-Object -First 1)
    if ($fallback.Count -gt 0) {
        return [pscustomobject][ordered]@{
            FeatureActive = $true
            CliMajor = $major
            ListAvailable = $true
            Verdict = 'degraded'
            CanApply = $true
            CanAutoApply = $true
            NormalizedVersion = $normalizedText
            MapStatus = 'modular'
            ClassmapKey = $fallback[0].ClassmapKey
            FallbackVersion = $fallback[0].VersionText
            FallbackClassmapKey = $fallback[0].ClassmapKey
            SupportListPath = $resolvedPath
            Reason = "The Spotify version is outside the allowlist, so the nearest lower modular classmap $($fallback[0].VersionText) is used."
        }
    }

    return [pscustomobject][ordered]@{
        FeatureActive = $true
        CliMajor = $major
        ListAvailable = $true
        Verdict = 'refused'
        CanApply = $false
        CanAutoApply = $false
        NormalizedVersion = $normalizedText
        MapStatus = $null
        ClassmapKey = $null
        FallbackVersion = $null
        FallbackClassmapKey = $null
        SupportListPath = $resolvedPath
        Reason = 'The Spotify version is outside the allowlist and has no same-minor lower modular classmap fallback.'
    }
}
