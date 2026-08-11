function Remove-PathEntry {
    param(
        [string]$Entry,
        [ValidateSet('User','Process')] [string]$Scope = 'User',
        [string]$EnvironmentKeyPath = 'Environment',
        [switch]$SkipEnvironmentBroadcast
    )
    $normalized = Get-NormalizedPathString -Path $Entry
    if ([string]::IsNullOrWhiteSpace($normalized)) { return $false }
    $entries = @(Get-PathEntries -Scope $Scope -EnvironmentKeyPath $EnvironmentKeyPath)
    $remaining = @()
    $removed = $false
    foreach ($existing in $entries) {
        $existingNormalized = Get-NormalizedPathString -Path $existing
        if ($existingNormalized -and $existingNormalized.ToLowerInvariant() -eq $normalized.ToLowerInvariant()) {
            $removed = $true
            continue
        }
        $remaining += $existing
    }
    if ($removed) {
        Set-PathEntries -Scope $Scope -Entries $remaining -TokenKind 'pathEntryRemove' -ChangedEntry $Entry -EnvironmentKeyPath $EnvironmentKeyPath -SkipEnvironmentBroadcast:$SkipEnvironmentBroadcast
    }
    return $removed
}
