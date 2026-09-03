function Get-LibreSpotAssetInstallFailureSummary {
    # One line naming everything the run was asked to install and did not. Empty
    # when nothing failed, so callers can test it to decide the run's outcome.
    $failures = @($global:LibreSpotAssetInstallFailures)
    if ($failures.Count -eq 0) { return '' }

    $parts = foreach ($failure in $failures) {
        $reason = [string]$failure.Reason
        if ([string]::IsNullOrWhiteSpace($reason)) {
            "$($failure.Kind) '$($failure.Name)'"
        } else {
            "$($failure.Kind) '$($failure.Name)' ($reason)"
        }
    }

    $noun = if ($failures.Count -eq 1) { 'asset was' } else { 'assets were' }
    return "The run finished but $($failures.Count) selected $noun not installed: $($parts -join '; ')"
}
