$global:PinnedReleases = @{
    SpotX = @{
        Version = '2.0'
        Commit  = '550bc72cd15f6e2a172a6ecc0873d0991eb1c83c'
        Url     = 'https://raw.githubusercontent.com/SpotX-Official/SpotX/550bc72cd15f6e2a172a6ecc0873d0991eb1c83c/run.ps1'
        SHA256  = '863cd19429160c911ce7439426d9e2127064028ccabbaf3007b233a393607606'
        DefenderMutations = $false
        DefenderOptOut = ''
    }
    SpicetifyCLI = @{
        Version = '2.44.0'
        WindowsMinSpotify = '1.2.14'
        WindowsMaxTestedSpotify = '1.2.93'
        CompatibilityUrl = 'https://github.com/spicetify/cli/releases/tag/v2.44.0'
        SHA256  = @{
            x64   = '215435095420e3804001a650c072f51befde897b414b0dac054edc2ea258ebea'
            arm64 = 'a6f827ae6387203bb87ff4af1f5ab21e4671a542ce1a0e3cb82ddc77d2ac7444'
        }
    }
    Marketplace = @{
        Version = '1.0.9'
        Url     = 'https://github.com/spicetify/marketplace/releases/download/v1.0.9/marketplace.zip'
        SHA256  = '2713054703c2365e391658a58c782dd2ebdd8d573f2015b5a2bab58b7eee8685'
    }
    Themes = @{
        Commit  = 'df033493a7dae30ca6e371de9cec1897871dbb0c'
        SHA256  = 'c837828c71d7a938898f87965b1fe9e5812cec831bd9cb1619bd8feb6020fdc3'
    }
}
