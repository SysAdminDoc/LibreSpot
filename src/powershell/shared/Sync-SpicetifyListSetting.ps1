function Sync-SpicetifyListSetting {
    param(
        [string]$Key,
        [string[]]$DesiredItems,
        [string[]]$ManagedItems
    )
    $desired = @($DesiredItems | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $managed = @($ManagedItems | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $current = @(Get-SpicetifyConfigListValue -Key $Key)

    $currentLookup = @{}
    foreach ($item in $current) { $currentLookup[$item.ToLowerInvariant()] = $true }

    $desiredLookup = @{}
    foreach ($item in $desired) { $desiredLookup[$item.ToLowerInvariant()] = $true }

    $managedLookup = @{}
    foreach ($item in $managed) { $managedLookup[$item.ToLowerInvariant()] = $true }

    $changed = $false
    foreach ($item in $desired) {
        if ($currentLookup.ContainsKey($item.ToLowerInvariant())) { continue }
        Invoke-SpicetifyCli -Arguments @('config', $Key, $item, '--bypass-admin') -FailureMessage "Could not enable $Key item '$item'."
        Write-Log "Enabled $Key item: $item"
        $changed = $true
    }

    foreach ($item in $current) {
        $itemKey = $item.ToLowerInvariant()
        if (-not $managedLookup.ContainsKey($itemKey)) { continue }
        if ($desiredLookup.ContainsKey($itemKey)) { continue }
        Invoke-SpicetifyCli -Arguments @('config', $Key, "$item-", '--bypass-admin') -FailureMessage "Could not remove $Key item '$item'."
        Write-Log "Removed $Key item: $item"
        $changed = $true
    }

    if (-not $changed) {
        Write-Log "No $Key changes were needed."
    }
}
