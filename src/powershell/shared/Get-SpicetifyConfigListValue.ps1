function Get-SpicetifyConfigListValue {
    param([string]$Key)
    $entries = Get-SpicetifyConfigEntries
    if (-not $entries.ContainsKey($Key)) { return @() }
    $raw = [string]$entries[$Key]
    if ([string]::IsNullOrWhiteSpace($raw)) { return @() }
    return @(
        $raw -split '\|' |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
    )
}
