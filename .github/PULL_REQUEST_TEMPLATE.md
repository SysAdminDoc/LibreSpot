## What changed

<!-- One-line summary of the change. -->

## Risk

<!-- Low / Medium / High — explain anything above Low. -->

## Test evidence

<!-- What did you test? Paste relevant output, or describe the manual steps. -->

## Screenshots

<!-- Required for UI changes. Delete this section otherwise. -->

## Release-note impact

<!-- Does this affect users? If so, what should the changelog say? -->

## Checklist

- [ ] Safe .NET tests pass locally (`dotnet test tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj --filter "FullyQualifiedName!~WpfFlaUiSmokeTests&FullyQualifiedName!~WpfUiAutomationSmokeTests&FullyQualifiedName!~WpfQaMatrixTests"`)
- [ ] PowerShell composition and 5.1/7 parsing pass (`pwsh -File .\Build-Scripts.ps1 -Validate`)
- [ ] PSScriptAnalyzer passes (`pwsh -File .\Build-Scripts.ps1 -Lint`)
- [ ] Pester passes (`Invoke-Pester -Path .\tests\powershell\LibreSpot.Tests.ps1 -CI`)
- [ ] No hardcoded English UI strings added without resource backing
- [ ] Version strings match across all files (if changed)
- [ ] README updated (if user-facing behavior changed)
