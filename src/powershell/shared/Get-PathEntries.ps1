function Get-PathEntries {
    param([ValidateSet('User','Process')] [string]$Scope = 'User')
    if ($Scope -eq 'Process') {
        $rawPath = $env:PATH
    } else {
        # Environment.GetEnvironmentVariable expands REG_EXPAND_SZ values.
        # Read the registry value directly so a PATH edit preserves tokens
        # such as %USERPROFILE% and %JAVA_HOME% byte-for-byte.
        $environmentKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $false)
        try {
            $rawPath = if ($null -eq $environmentKey) {
                $null
            } else {
                $environmentKey.GetValue(
                    'Path',
                    $null,
                    [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
            }
        } finally {
            if ($null -ne $environmentKey) { $environmentKey.Dispose() }
        }
    }
    if ([string]::IsNullOrWhiteSpace($rawPath)) { return @() }
    return @($rawPath -split ';' | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}
