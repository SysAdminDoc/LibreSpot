function Repair-LibreSpotManagedCustomAppRoutes {
    param($Config)

    $appNames = [System.Collections.Generic.List[string]]::new()
    if ($Config -and $Config.Spicetify_Marketplace) {
        $appNames.Add('marketplace')
    }
    if ($Config -and @($Config.Spicetify_CustomApps) -contains 'librespot') {
        $appNames.Add('librespot')
    }

    $results = [System.Collections.Generic.List[object]]::new()
    foreach ($appName in $appNames) {
        $wiring = Repair-SpicetifyCustomAppWiring -AppName $appName
        $results.Add([pscustomobject]@{
            AppName    = $appName
            Status     = $wiring.Status
            BundlePath = $wiring.BundlePath
            Detail     = $wiring.Detail
        })
    }
    return @($results)
}
