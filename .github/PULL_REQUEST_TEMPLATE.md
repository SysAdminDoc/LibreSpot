## What changed

<!-- One-line summary of the change. -->

## Risk

<!-- Low, Medium, or High. Explain anything above Low. -->

## Test evidence

<!-- What did you test? Paste relevant output, or describe the manual steps. -->

## Screenshots

<!-- Required for UI changes. Delete this section otherwise. -->

## Release-note impact

<!-- Does this affect users? If so, what should the changelog say? -->

## Checklist

- [ ] Safe .NET tests pass locally (`tests/LibreSpot.Desktop.Tests/bin/Debug/net10.0-windows/LibreSpot.Desktop.Tests.exe --filter-not-class "*Wpf*" --minimum-expected-tests 1` and the matching Core test executable)
- [ ] PowerShell composition and 5.1/7 parsing pass (`pwsh -File .\Build-Scripts.ps1 -Validate`)
- [ ] PSScriptAnalyzer passes (`pwsh -File .\Build-Scripts.ps1 -Lint`)
- [ ] Full configured Pester suite passes (`Invoke-Pester -Configuration (New-PesterConfiguration -Hashtable (& .\tests\powershell\pester.config.ps1))` with Pester 5.9.1)
- [ ] No hardcoded English UI strings added without resource backing
- [ ] Version strings match across all files (if changed)
- [ ] README updated (if user-facing behavior changed)
