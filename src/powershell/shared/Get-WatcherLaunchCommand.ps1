function Get-WatcherLaunchCommand {
    # Returns a [string[]]{ FileName, ArgumentList... } suitable for schtasks.exe's
    # /TR value. Prefers the compiled LibreSpot.exe when the user launched from it;
    # falls back to powershell.exe + -File when launched from the raw .ps1. Returns
    # $null when neither path is usable (e.g. `irm | iex`) so the caller can surface
    # a helpful error instead of registering a broken task.
    $entry = [string]$script:EntryCommandPath
    if ([string]::IsNullOrWhiteSpace($entry)) { return $null }
    if (-not (Test-Path -LiteralPath $entry)) { return $null }

    $ext = [System.IO.Path]::GetExtension($entry).ToLowerInvariant()
    if ($ext -eq '.exe') {
        return @{ Command = "`"$entry`" -Watch"; Entry = $entry }
    }
    if ($ext -eq '.ps1') {
        $ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
        if (-not (Test-Path -LiteralPath $ps)) { $ps = 'powershell.exe' }
        return @{ Command = "`"$ps`" -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$entry`" -Watch"; Entry = $entry }
    }
    return $null
}
