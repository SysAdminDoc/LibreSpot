@{
    Run = @{ Path = $PSScriptRoot; PassThru = $true }
    Output = @{ Verbosity = 'Detailed' }
    TestResult = @{ Enabled = $true; OutputPath = "$PSScriptRoot/test-results.xml"; OutputFormat = 'NUnitXml' }
}
