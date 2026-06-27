function ConvertTo-NativeArgumentString {
    param([string[]]$Arguments)

    $parts = @()
    foreach ($argument in @($Arguments)) {
        $value = if ($null -eq $argument) { '' } else { [string]$argument }
        if ($value.Length -gt 0 -and $value -notmatch '[\s"]') {
            $parts += $value
            continue
        }

        $builder = New-Object System.Text.StringBuilder
        [void]$builder.Append('"')
        $backslashes = 0
        foreach ($character in $value.ToCharArray()) {
            if ($character -eq [char]92) {
                $backslashes++
                continue
            }
            if ($character -eq [char]34) {
                if ($backslashes -gt 0) {
                    [void]$builder.Append(('\' * ($backslashes * 2)))
                    $backslashes = 0
                }
                [void]$builder.Append('\"')
                continue
            }
            if ($backslashes -gt 0) {
                [void]$builder.Append(('\' * $backslashes))
                $backslashes = 0
            }
            [void]$builder.Append($character)
        }
        if ($backslashes -gt 0) {
            [void]$builder.Append(('\' * ($backslashes * 2)))
        }
        [void]$builder.Append('"')
        $parts += $builder.ToString()
    }

    return ($parts -join ' ')
}
