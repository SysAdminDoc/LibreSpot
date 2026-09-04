function Get-LibreSpotWatcherFailureState {
    <#
        The watcher-state fields to write after a failed reapply. The hold
        fields appear once the same Spotify build has failed the threshold
        number of times in a row, which is what stops the thirty-minute retry
        loop against a build the pinned tuple cannot patch.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][AllowNull()][hashtable]$State,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$CurrentVersion,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Reason,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Timestamp
    )

    $decision = Get-LibreSpotWatcherHoldDecision -State $State -CurrentVersion $CurrentVersion
    $count = $decision.FailureCount + 1

    $result = @{
        ReapplyFailureCount   = $count
        ReapplyFailureVersion = $CurrentVersion
    }

    if ($count -ge $decision.Threshold) {
        $result['HoldSpotifyVersion'] = $CurrentVersion
        $result['HoldSince'] = $Timestamp
        $result['HoldReason'] = $Reason
    } else {
        # Below the threshold there is no hold. Writing the fields back as null
        # matters when Spotify has moved on: a hold left over from the previous
        # build would otherwise keep Maintenance reporting the old version and
        # the old reason while hiding the failure actually happening now.
        $result['HoldSpotifyVersion'] = $null
        $result['HoldSince'] = $null
        $result['HoldReason'] = $null
    }

    return $result
}
