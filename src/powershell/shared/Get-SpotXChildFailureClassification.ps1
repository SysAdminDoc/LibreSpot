function Get-SpotXChildFailureClassification {
    # SpotX can fail inside its OWN downloader after LibreSpot has already
    # hash-verified run.ps1 (SpotX issues #870, #836). Without classification
    # those runs surface as a generic "Process exited with code N". Returns
    # $null when no known signature matches, otherwise a stable category id
    # plus sanitized guidance (never echoes raw child output, which can
    # contain attacker-influenced mirror HTML).
    param([string]$Line)
    if ([string]::IsNullOrWhiteSpace($Line)) { return $null }

    if ($Line -match 'curl exit code 28|ERR_CONNECTION_TIMED_OUT|Operation timed out after') {
        return [pscustomobject]@{
            Category = 'SpotXChildDownloadTimeout'
            Guidance = "SpotX's own downloader timed out while fetching Spotify components. LibreSpot already verified the SpotX script itself, so this is an upstream network or CDN outage - retry in a few minutes, or choose a different download method under Custom Install > Advanced adjustments."
        }
    }

    if ($Line -match 'loadspot\.amd64fox1\.workers\.dev') {
        return [pscustomobject]@{
            Category = 'SpotXWorkerEndpointFailure'
            Guidance = "SpotX's Cloudflare worker download endpoint failed. This is an upstream SpotX outage (see SpotX issues #870/#836), not a problem on this machine - retry later, or choose a different download method under Custom Install > Advanced adjustments."
        }
    }

    if ($Line -match 'suspected phishing|reported for potential phishing|This website has been blocked') {
        return [pscustomobject]@{
            Category = 'SpotXMirrorBlockedPhishing'
            Guidance = 'A SpotX download mirror is currently flagged by Cloudflare as suspected phishing, so the download was blocked upstream. Turn off the mirror option (or retry without it) and run the setup again.'
        }
    }

    return $null
}
