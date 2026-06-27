function Write-SpicetifyCliOutputLine {
    param(
        [string]$Line,
        [hashtable]$ProgressState
    )

    $plain = Remove-ConsoleEscapeSequences -Text $Line
    $plain = [regex]::Replace($plain, '[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]', '')
    $plain = [regex]::Replace($plain, '\s+', ' ').Trim()
    if ([string]::IsNullOrWhiteSpace($plain)) { return $null }

    if ($plain -match 'Patching files\s*\[\s*0*(\d+)\s*/\s*0*(\d+)\s*\]') {
        $done = [int]$matches[1]
        $total = [Math]::Max(1, [int]$matches[2])
        $percent = [int][Math]::Min(100, [Math]::Floor(($done / $total) * 100))
        $bucket = if ($percent -ge 100) { 100 } else { [int]([Math]::Floor($percent / 10) * 10) }

        if ($ProgressState -and (-not $ProgressState.ContainsKey('LastUiPatchPercent') -or [int]$ProgressState['LastUiPatchPercent'] -ne $percent)) {
            Update-SpicetifyCliProgress -Line $plain
            $ProgressState['LastUiPatchPercent'] = $percent
        }

        $shouldLog = (-not $ProgressState) -or (-not $ProgressState.ContainsKey('LastPatchBucket')) -or ([int]$ProgressState['LastPatchBucket'] -ne $bucket)
        if ($shouldLog) {
            if ($ProgressState) { $ProgressState['LastPatchBucket'] = $bucket }
            $message = "Patching files: $done/$total ($percent%)"
            Write-Log "  $message"
            return $message
        }
        return $null
    }

    if ($plain -match '^(?:[-\\|/]\s*)?(Backing up app files|Extracting backup|Fetching remote CSS map|Copying raw assets|Updating theme''s styles|Applying additional modifications|Refreshing extensions|Refreshing custom apps)$') {
        $stage = $matches[1]
        Update-SpicetifyCliProgress -Line $stage
        if ((-not $ProgressState) -or (-not $ProgressState.ContainsKey('LastStage')) -or ([string]$ProgressState['LastStage'] -ne $stage)) {
            if ($ProgressState) { $ProgressState['LastStage'] = $stage }
            Write-Log "  $stage"
            return $stage
        }
        return $null
    }

    Update-SpicetifyCliProgress -Line $plain
    Write-Log "  $plain"
    return $plain
}
