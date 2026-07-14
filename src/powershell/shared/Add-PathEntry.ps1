function Add-PathEntry {
    param(
        [string]$Entry,
        [ValidateSet('User','Process')] [string]$Scope = 'User'
    )
    $normalized = Get-NormalizedPathString -Path $Entry
    if ([string]::IsNullOrWhiteSpace($normalized)) { return $false }
    $entries = @(Get-PathEntries -Scope $Scope)
    foreach ($existing in $entries) {
        $existingNormalized = Get-NormalizedPathString -Path $existing
        if ($existingNormalized -and $existingNormalized.ToLowerInvariant() -eq $normalized.ToLowerInvariant()) {
            return $false
        }
    }
    Set-PathEntries -Scope $Scope -Entries (@($entries) + @($Entry)) -TokenKind 'pathEntryAdd' -ChangedEntry $Entry
    return $true
}
