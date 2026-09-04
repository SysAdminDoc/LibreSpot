function Get-LibreSpotWatcherHoldDecision {
    <#
        Decides whether the auto-reapply watcher should attempt another reapply
        for the Spotify build in front of it.

        Before this existed the watcher kept LastKnownVersion on failure so it
        would "retry next tick", which meant a build the pinned tuple cannot
        patch was stopped and re-applied every thirty minutes for as long as the
        machine stayed on. The user saw a growing watcher log and nothing that
        named the build or the step that failed.

        Three consecutive failures against the same build put the watcher on
        hold for that build. A different build, or a successful apply, clears
        it. The decision is pure so both lanes share one rule and Pester can
        exercise every branch without Task Scheduler.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][AllowNull()][hashtable]$State,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$CurrentVersion
    )

    $threshold = 3
    $held = $false
    $failureCount = 0
    $failureVersion = $null

    if ($null -ne $State) {
        $failureVersion = [string]$State['ReapplyFailureVersion']
        # ConvertFrom-Json hands back a number or a string depending on how the
        # file was written, and an absent key is $null; normalize before math.
        $rawCount = $State['ReapplyFailureCount']
        if ($null -ne $rawCount) {
            $parsed = 0
            if ([int]::TryParse([string]$rawCount, [ref]$parsed)) { $failureCount = $parsed }
        }
        $holdVersion = [string]$State['HoldSpotifyVersion']
        if ($holdVersion -and $holdVersion -eq $CurrentVersion) { $held = $true }
    }

    if ($failureVersion -ne $CurrentVersion) {
        # The counters belong to a build that is no longer installed.
        $failureCount = 0
    }

    [pscustomobject]@{
        ShouldAttempt  = -not $held
        IsHeld         = $held
        FailureCount   = $failureCount
        FailureVersion = $failureVersion
        Threshold      = $threshold
    }
}
