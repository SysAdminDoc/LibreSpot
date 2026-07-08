function Get-WatcherState {
    if (-not (Test-Path -LiteralPath $global:WATCHER_STATE_PATH)) {
        return @{ LastKnownVersion = $null; LastRunAt = $null; LastOutcome = $null }
    }
    try {
        $raw = Get-Content -LiteralPath $global:WATCHER_STATE_PATH -Raw -ErrorAction Stop | ConvertFrom-Json
        return @{
            LastKnownVersion = [string]$raw.LastKnownVersion
            LastRunAt        = [string]$raw.LastRunAt
            LastOutcome      = [string]$raw.LastOutcome
        }
    } catch {
        return @{ LastKnownVersion = $null; LastRunAt = $null; LastOutcome = $null }
    }
}
