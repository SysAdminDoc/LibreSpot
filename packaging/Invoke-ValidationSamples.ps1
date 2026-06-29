[CmdletBinding()]
param(
    [ValidateSet('all', 'winget', 'scoop', 'chocolatey')]
    [string]$Tool = 'all',

    [string]$PublishRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'publish'),

    [switch]$RunInstallChecks
)

$ErrorActionPreference = 'Stop'

function Test-ToolAvailable {
    param([Parameter(Mandatory)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name is not available on PATH. Install it before running this validation sample."
    }
}

$selected = if ($Tool -eq 'all') { @('winget', 'scoop', 'chocolatey') } else { @($Tool) }

foreach ($name in $selected) {
    switch ($name) {
        'winget' {
            Test-ToolAvailable winget
            $wingetRoot = Join-Path $PublishRoot 'winget'
            # PACKAGE-CHECK: winget validate .\publish\winget\
            & winget validate $wingetRoot
        }
        'scoop' {
            Test-ToolAvailable scoop
            $scoopManifest = Join-Path $PublishRoot 'scoop\librespot.json'
            # PACKAGE-CHECK: scoop install .\publish\scoop\librespot.json
            # PACKAGE-CHECK: .\bin\checkver.ps1 librespot -u -dir .\publish\scoop\
            if ($RunInstallChecks) {
                & scoop install $scoopManifest
            } else {
                Write-Information "Skipping Scoop install check; pass -RunInstallChecks in a disposable VM to execute it." -InformationAction Continue
            }
        }
        'chocolatey' {
            Test-ToolAvailable choco
            $chocolateyRoot = Join-Path $PublishRoot 'chocolatey'
            Push-Location $chocolateyRoot
            try {
                # PACKAGE-CHECK: choco pack
                & choco pack
                if ($RunInstallChecks) {
                    # PACKAGE-CHECK: choco install librespot --source .
                    & choco install librespot --source . --yes
                } else {
                    Write-Information "Skipping Chocolatey install check; pass -RunInstallChecks in a disposable VM to execute it." -InformationAction Continue
                }
            } finally {
                Pop-Location
            }
        }
    }
}
