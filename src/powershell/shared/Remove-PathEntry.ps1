function Remove-PathEntry {
    param(
        [string]$Entry,
        [ValidateSet('User','Process')] [string]$Scope = 'User'
    )
    $normalized = Get-NormalizedPathString -Path $Entry
    if ([string]::IsNullOrWhiteSpace($normalized)) { return $false }
    $entries = @(Get-PathEntries -Scope $Scope)
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
        Set-PathEntries -Scope $Scope -Entries $remaining
    }
    return $removed
}
