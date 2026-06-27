function Get-DownloadFailureHint {
    param(
        [string]$Uri,
        [object]$ErrorRecord,
        [string]$Stage = 'Download'
    )
    $message = ''
    try { $message = [string]$ErrorRecord.Exception.Message } catch { $message = [string]$ErrorRecord }
    $statusCode = $null
    try {
        if ($ErrorRecord.Exception.Response -and $ErrorRecord.Exception.Response.StatusCode) {
            $statusCode = [int]$ErrorRecord.Exception.Response.StatusCode
        }
    } catch {}
    $target = $Uri
    try { $target = ([uri]$Uri).Host } catch {}
    $lowerMessage = $message.ToLowerInvariant()
    if ($statusCode -eq 407 -or $lowerMessage -match 'proxy.*auth|407|proxy authentication') {
        return "$Stage failed: proxy authentication is required for $target. Configure the system or WinHTTP proxy before retrying."
    }
    if ($statusCode -eq 429 -or (($statusCode -eq 403) -and ($target -match 'github'))) {
        return "$Stage failed: GitHub rate limit or access block for $target. Wait for the rate-limit reset or retry from a network with GitHub access."
    }
    if ($lowerMessage -match 'could not be resolved|name resolution|no such host|\bdns\b') {
        return "$Stage failed: DNS could not resolve $target. Check DNS, VPN, firewall, or content-filtering rules."
    }
    if ($lowerMessage -match 'ssl|tls|certificate|trust relationship') {
        return "$Stage failed: TLS or certificate validation failed for $target. Check system time, enterprise TLS inspection, and root certificates."
    }
    if ($lowerMessage -match 'timed out|timeout') {
        return "$Stage failed: the connection to $target timed out. Check connectivity or retry after the network is stable."
    }
    if ($lowerMessage -match 'sha256 mismatch|hash mismatch|checksum') {
        return "$Stage hash verification failed for $target. The downloaded file does not match the expected SHA256 checksum. Try clearing the asset cache and re-downloading."
    }
    if ([string]::IsNullOrWhiteSpace($message)) {
        return "$Stage failed for $target."
    }
    return "$Stage failed for ${target}: $message"
}
