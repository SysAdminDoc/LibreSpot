function Get-SpotXDownloadRetryPlan {
    # Maps a classified SpotX child-download failure (from
    # Get-SpotXChildFailureClassification) to a single automatic-retry plan.
    # Timeouts and Cloudflare-worker outages retry once through the SpotX
    # mirror; a mirror flagged as phishing retries once WITHOUT the mirror.
    # Returns $null when the failure is not download-retryable, or when the
    # useful mirror toggle was already the state of the failed attempt - this
    # guarantees at most one automatic retry and that the retry changes the
    # download path (a same-path retry would just fail the same way).
    param(
        [string]$Category,
        [bool]$MirrorAlreadyUsed
    )

    switch ($Category) {
        'SpotXChildDownloadTimeout' {
            if ($MirrorAlreadyUsed) { return $null }
            return [pscustomobject]@{
                UseMirror = $true
                Reason    = "SpotX's download timed out; retrying once through the SpotX mirror."
            }
        }
        'SpotXWorkerEndpointFailure' {
            if ($MirrorAlreadyUsed) { return $null }
            return [pscustomobject]@{
                UseMirror = $true
                Reason    = "SpotX's primary download endpoint failed; retrying once through the SpotX mirror."
            }
        }
        'SpotXMirrorBlockedPhishing' {
            if (-not $MirrorAlreadyUsed) { return $null }
            return [pscustomobject]@{
                UseMirror = $false
                Reason    = 'The SpotX mirror was blocked upstream; retrying once without the mirror.'
            }
        }
        default { return $null }
    }
}
