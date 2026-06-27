function Compare-LibreSpotVersions {
    # Semver-ish compare that tolerates `-preview.N` / `-rc.N` suffixes and
    # string-compare them as a tie-breaker when the numeric prefixes match.
    # Returns $true iff $Latest is strictly newer than $Current.
    param([string]$Latest, [string]$Current)
    if ([string]::IsNullOrWhiteSpace($Latest)) { return $false }
    if ([string]::IsNullOrWhiteSpace($Current)) { return $true }
    $stripLatest  = ($Latest  -replace '-preview.*','' -replace '-rc.*','')
    $stripCurrent = ($Current -replace '-preview.*','' -replace '-rc.*','')
    try {
        $l = [Version]$stripLatest
        $c = [Version]$stripCurrent
        if ($l -gt $c) { return $true }
        if ($l -lt $c) { return $false }
        # Numeric prefixes equal: the one WITHOUT a pre-release suffix is newer.
        $latestIsStable  = ($Latest  -eq $stripLatest)
        $currentIsStable = ($Current -eq $stripCurrent)
        if ($latestIsStable -and -not $currentIsStable) { return $true }
        if (-not $latestIsStable -and $currentIsStable) { return $false }
        # Both stable or both pre-release with same numeric prefix: compare the
        # full suffix lexically. E.g. `-preview.5` > `-preview.4`. If the
        # suffixes are identical the versions are equal (not "newer").
        if ($Latest -eq $Current) { return $false }
        return ([string]::CompareOrdinal($Latest, $Current) -gt 0)
    } catch {
        # Non-parseable versions: lexical compare is better than claiming all
        # non-equal versions are "newer".
        if ($Latest -eq $Current) { return $false }
        return ([string]::CompareOrdinal($Latest, $Current) -gt 0)
    }
}
