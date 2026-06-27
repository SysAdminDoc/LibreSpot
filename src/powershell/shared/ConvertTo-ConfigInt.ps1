function ConvertTo-ConfigInt {
    param(
        [object]$Value,
        [int]$Default = 0,
        [int]$Minimum = [int]::MinValue,
        [int]$Maximum = [int]::MaxValue
    )
    $parsed = 0
    if ($null -eq $Value -or -not [int]::TryParse([string]$Value, [ref]$parsed)) {
        $parsed = $Default
    }
    if ($parsed -lt $Minimum) { $parsed = $Minimum }
    if ($parsed -gt $Maximum) { $parsed = $Maximum }
    return $parsed
}
