function Get-DesktopPath {
    try {
        $shell = (Get-ItemProperty "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders" -EA Stop).Desktop
        if ($shell) { $shell = [Environment]::ExpandEnvironmentVariables($shell) }
        if ($shell -and (Test-Path $shell)) { return $shell }
    } catch {}
    return [Environment]::GetFolderPath('Desktop')
}
