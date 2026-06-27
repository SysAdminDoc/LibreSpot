function Test-AutoReapplyTaskRegistered {
    try {
        $out = & schtasks.exe /Query /TN $global:WATCHER_TASK_NAME 2>$null
        return ($LASTEXITCODE -eq 0) -and ($out -and $out.Length -gt 0)
    } catch { return $false }
}
