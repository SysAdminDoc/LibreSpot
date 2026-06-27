function Remove-ConsoleEscapeSequences {
    param([string]$Text)

    if ($null -eq $Text) { return '' }
    $escapePattern = [regex]::Escape([string][char]27) + '\[[0-?]*[ -/]*[@-~]'
    return [regex]::Replace([string]$Text, $escapePattern, '')
}
