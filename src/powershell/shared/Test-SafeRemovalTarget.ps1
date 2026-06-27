function Test-SafeRemovalTarget {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    try {
        $resolved = (Get-Item -LiteralPath $Path -Force -ErrorAction Stop).FullName
    } catch {
        return $false
    }
    if ([string]::IsNullOrWhiteSpace($resolved)) { return $false }

    $normalized = $resolved.TrimEnd('\')
    $root = [System.IO.Path]::GetPathRoot($resolved).TrimEnd('\')
    if ($normalized -eq $root) { return $false }

    $blockedRaw = @(
        $env:USERPROFILE,
        $env:APPDATA,
        $env:LOCALAPPDATA,
        $env:TEMP,
        $env:SystemRoot,
        $env:ProgramFiles,
        ${env:ProgramFiles(x86)},
        $env:ProgramData,
        $env:ALLUSERSPROFILE,
        $env:PUBLIC,
        $env:OneDrive,
        $env:OneDriveConsumer,
        $env:OneDriveCommercial,
        [Environment]::GetFolderPath('Desktop'),
        [Environment]::GetFolderPath('Personal'),
        [Environment]::GetFolderPath('CommonDesktopDirectory'),
        [Environment]::GetFolderPath('CommonStartMenu')
    )
    $blockedTargets = @($blockedRaw | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.TrimEnd('\') } | Sort-Object -Unique)

    return ($normalized -notin $blockedTargets)
}
