function ConvertTo-LibreSpotProfileId {
    param([string]$Name)
    $text = if ([string]::IsNullOrWhiteSpace($Name)) { 'profile' } else { $Name.Trim().ToLowerInvariant() }
    $chars = foreach ($ch in $text.ToCharArray()) {
        if ([char]::IsLetterOrDigit($ch)) { $ch } else { '-' }
    }
    $slug = (($chars -join '') -split '-' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join '-'
    if ([string]::IsNullOrWhiteSpace($slug)) { return 'profile' }
    return $slug
}
