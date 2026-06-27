function Get-NetworkPreflightStatus {
    param(
        [string]$Uri = 'https://raw.githubusercontent.com',
        [string]$Purpose = 'download sources',
        [int]$TimeoutMilliseconds = 5000
    )
    $resp = $null
    $target = $Uri
    try { $target = ([uri]$Uri).Host } catch {}
    $result = [ordered]@{
        Ready   = $false
        Code    = 'Unknown'
        Target  = $target
        Message = ''
        Detail  = ''
    }
    try {
        $request = [System.Net.WebRequest]::Create($Uri)
        $request.Timeout = $TimeoutMilliseconds
        $request.Method = 'HEAD'
        try { $request.UserAgent = "LibreSpot/$global:VERSION" } catch {}
        $resp = $request.GetResponse()
        $statusCode = $null
        try { $statusCode = [int]$resp.StatusCode } catch {}
        if ($null -eq $statusCode -or ($statusCode -ge 200 -and $statusCode -lt 400)) {
            $result.Ready = $true
            $result.Code = 'Ready'
            $result.Message = "LibreSpot can reach $target for $Purpose."
            $result.Detail = if ($null -eq $statusCode) { 'HTTP status unavailable' } else { "HTTP $statusCode" }
        } elseif ($statusCode -eq 407) {
            $result.Code = 'ProxyAuthRequired'
            $result.Message = "Network preflight failed: proxy authentication is required for $target. Configure the system or WinHTTP proxy before retrying."
            $result.Detail = "HTTP $statusCode"
        } elseif (($statusCode -eq 403 -or $statusCode -eq 429) -and ($target -match 'github')) {
            $result.Code = 'GitHubRateLimitOrBlock'
            $result.Message = "Network preflight failed: GitHub rate limit or access block for $target. Wait for the rate-limit reset or retry from a network with GitHub access."
            $result.Detail = "HTTP $statusCode"
        } else {
            $result.Code = "Http$statusCode"
            $result.Message = "Network preflight failed: $target returned HTTP $statusCode while checking $Purpose."
            $result.Detail = "HTTP $statusCode"
        }
    } catch {
        $result.Code = Get-NetworkDiagnosticCode -Uri $Uri -ErrorRecord $_
        $result.Message = Get-DownloadFailureHint -Uri $Uri -ErrorRecord $_ -Stage 'Network preflight'
        try { $result.Detail = [string]$_.Exception.Message } catch {}
    }
    finally { if ($resp) { try { $resp.Close() } catch {} } }
    return [pscustomobject]$result
}
