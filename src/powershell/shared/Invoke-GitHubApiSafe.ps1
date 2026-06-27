function Invoke-GitHubApiSafe { param([string]$Uri,[hashtable]$Headers,[int]$TimeoutSec=15,[string]$Label='GitHub API')
    try {
        $response = Invoke-WebRequest -Uri $Uri -Headers $Headers -TimeoutSec $TimeoutSec -UseBasicParsing -ErrorAction Stop
        $remaining = $response.Headers['x-ratelimit-remaining']
        if ($remaining -and [int]$remaining -le 5) {
            $resetEpoch = $response.Headers['x-ratelimit-reset']
            $resetTime = if ($resetEpoch) { ([DateTimeOffset]::FromUnixTimeSeconds([long]$resetEpoch)).LocalDateTime.ToString('HH:mm:ss') } else { 'unknown' }
            Write-Log "GitHub API rate limit nearly exhausted ($remaining remaining, resets at $resetTime). Subsequent checks may fail." -Level 'WARN'
        }
        return ($response.Content | ConvertFrom-Json)
    } catch {
        $statusCode = $null
        if ($_.Exception.PSObject.Properties['Response'] -and $_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        if ($statusCode -eq 403 -or $statusCode -eq 429) {
            $resetEpoch = $null
            try { $resetEpoch = $_.Exception.Response.Headers['x-ratelimit-reset'] } catch {}
            $resetMsg = ''
            if ($resetEpoch) {
                $resetTime = ([DateTimeOffset]::FromUnixTimeSeconds([long]$resetEpoch)).LocalDateTime.ToString('HH:mm:ss')
                $resetMsg = " Rate limit resets at $resetTime."
            }
            throw "GitHub API rate limit reached for $Label (HTTP $statusCode).$resetMsg Try again later or use an authenticated request."
        }
        throw (Get-DownloadFailureHint -Uri $Uri -ErrorRecord $_ -Stage $Label)
    }
}
