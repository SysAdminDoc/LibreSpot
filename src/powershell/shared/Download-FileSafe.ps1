function Download-FileSafe { param([string]$Uri,[string]$OutFile)
    Write-DownloaderCveWarningIfNeeded
    Write-Log "Downloading: $Uri"
    $headers = @{'User-Agent'="LibreSpot/$global:VERSION"}
    try {
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        } catch {}
        $outDir = Split-Path -Path $OutFile -Parent
        if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
            New-Item -Path $outDir -ItemType Directory -Force | Out-Null
        }
        if (Test-Path -LiteralPath $OutFile) {
            Remove-Item -LiteralPath $OutFile -Force -ErrorAction SilentlyContinue
        }
        try {
            Invoke-WebRequest -Uri $Uri -OutFile $OutFile -UseBasicParsing -Headers $headers -TimeoutSec 120 -ErrorAction Stop
        }
        catch {
            $webHint = Get-DownloadFailureHint -Uri $Uri -ErrorRecord $_ -Stage 'Web request'
            Write-Log "$webHint Trying BITS fallback." -Level 'WARN'
            try {
                Import-Module BitsTransfer -EA SilentlyContinue
                $bitsJob = Start-BitsTransfer -Source $Uri -Destination $OutFile -Asynchronous -EA Stop
                $deadline = (Get-Date).AddSeconds(120)
                while ($bitsJob.JobState -in @('Transferring','Connecting','Queued','TransientError')) {
                    if ((Get-Date) -gt $deadline) { Remove-BitsTransfer $bitsJob -EA SilentlyContinue; throw "BITS transfer timed out (120s)" }
                    Start-Sleep -Milliseconds 500
                }
                if ($bitsJob.JobState -ne 'Transferred') {
                    $js=$bitsJob.JobState
                    $bitsDetail = "BITS state: $js"
                    try { if ($bitsJob.ErrorDescription) { $bitsDetail = "$bitsDetail - $($bitsJob.ErrorDescription)" } } catch {}
                    Remove-BitsTransfer $bitsJob -EA SilentlyContinue
                    throw $bitsDetail
                }
                Complete-BitsTransfer $bitsJob
            } catch {
                $bitsHint = Get-DownloadFailureHint -Uri $Uri -ErrorRecord $_ -Stage 'BITS'
                throw "Download failed after WebRequest and BITS fallback. $webHint $bitsHint"
            }
        }
        if (-not (Test-Path -LiteralPath $OutFile)) { throw "Download produced no file: $OutFile" }
        if ((Get-Item -LiteralPath $OutFile).Length -eq 0) { throw "Download produced empty file: $OutFile" }
    } catch {
        if (Test-Path -LiteralPath $OutFile) {
            Remove-Item -LiteralPath $OutFile -Force -ErrorAction SilentlyContinue
        }
        throw
    }
}
