function Get-SpicetifyCliMajorVersion {
    # Pure: parse the leading integer major from a Spicetify version string such
    # as '2.44.0', 'v3.0.0', or '3.1.2-dev'. Returns $null for empty or
    # non-numeric input (e.g. 'Dev'), where the version is treated as unknown.
    param([string]$Version)
    if ([string]::IsNullOrWhiteSpace($Version)) { return $null }
    $trimmed = $Version.Trim().TrimStart('v', 'V')
    $match = [regex]::Match($trimmed, '^\d+')
    if (-not $match.Success) { return $null }
    return [int]$match.Value
}
