function Get-LibreSpotWatcherClearedHoldState {
    <#
        The watcher-state fields that retire a hold and the failure count.
        Written after a successful reapply so the next Spotify update starts
        from a clean count rather than inheriting the previous build's.
    #>
    [CmdletBinding()]
    param()

    return @{
        ReapplyFailureCount   = 0
        ReapplyFailureVersion = $null
        HoldSpotifyVersion    = $null
        HoldSince             = $null
        HoldReason            = $null
    }
}
