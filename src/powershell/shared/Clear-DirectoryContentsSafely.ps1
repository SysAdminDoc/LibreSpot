function Clear-DirectoryContentsSafely {
    param([string]$Path, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Container)) { return 0 }
    if (-not (Test-SafeRemovalTarget -Path $Path)) {
        Write-Log "  Refusing to clear unsafe directory target: $Path" -Level 'WARN'
        return 0
    }
    $removedCount = 0
    Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue | ForEach-Object {
        $itemLabel = if ($Label) { "${Label}: $($_.Name)" } else { $_.FullName }
        $removedCount += Remove-PathSafely -Path $_.FullName -Label $itemLabel
    }
    return $removedCount
}
