function Get-NetworkDiagnosticCode {
    param(
        [string]$Uri,
        [object]$ErrorRecord
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

    if ($statusCode -eq 407 -or $lowerMessage -match 'proxy.*auth|407|proxy authentication') { return 'ProxyAuthRequired' }
    if ($statusCode -eq 429 -or (($statusCode -eq 403) -and ($target -match 'github'))) { return 'GitHubRateLimitOrBlock' }
    if ($lowerMessage -match 'could not be resolved|name resolution|no such host|\bdns\b') { return 'DnsFailure' }
    if ($lowerMessage -match 'ssl|tls|certificate|trust relationship') { return 'TlsFailure' }
    if ($lowerMessage -match 'timed out|timeout') { return 'Timeout' }
    if ($lowerMessage -match 'sha256 mismatch|hash mismatch|checksum') { return 'HashMismatch' }
    return 'NetworkFailure'
}
