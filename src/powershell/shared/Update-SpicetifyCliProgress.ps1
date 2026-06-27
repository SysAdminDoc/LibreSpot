function Update-SpicetifyCliProgress {
    param([string]$Line)

    $plain = Remove-ConsoleEscapeSequences -Text $Line
    $sh = $script:syncHash
    if (-not $sh -or -not $sh.Dispatcher) { return }
    if ($plain -match 'Patching files\s*\[\s*(\d+)\s*/\s*(\d+)\s*\]') {
        $done = [int]$matches[1]
        $total = [Math]::Max(1, [int]$matches[2])
        $percent = [int][Math]::Min(99, [Math]::Floor(($done / $total) * 100))
        $progressValue = [int][Math]::Min(99, 86 + [Math]::Floor(($done / $total) * 12))
        $progressBar = $sh.ProgressBar
        $statusLabel = $sh.StatusLabel
        $stepLabel = $sh.StepLabel
        $installContext = $sh.InstallContext
        try {
            $sh.Dispatcher.Invoke([Action]{
                try {
                    if ($progressBar -and $progressBar.Value -lt $progressValue) { $progressBar.Value = $progressValue }
                    if ($statusLabel) { $statusLabel.Text = "Spicetify is patching Spotify files ($percent%)" }
                    if ($stepLabel) { $stepLabel.Text = "Applying setup: patching file $done of $total" }
                    if ($installContext) { $installContext.Text = "Spicetify is rebuilding Spotify's UI package. This can take several minutes on slower disks." }
                } catch {}
            }) | Out-Null
        } catch {}
    } elseif ($plain -match 'Extracting backup|Preprocessing|Fetching remote CSS map|Patching files') {
        $statusLabel = $sh.StatusLabel
        $installContext = $sh.InstallContext
        try {
            $sh.Dispatcher.Invoke([Action]{
                try {
                    if ($statusLabel) { $statusLabel.Text = 'Spicetify is preparing Spotify files' }
                    if ($installContext) { $installContext.Text = "Spicetify is rebuilding Spotify's UI package. This can take several minutes on slower disks." }
                } catch {}
            }) | Out-Null
        } catch {}
    }
}
