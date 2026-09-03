#requires -Version 5.1

# The JavaScript half of -DependencyHealth shipped with no test at any level, and
# three defects rode in behind that: the audit ran in the wrong directory and
# reported a clean result having read nothing, the allowlist was read off an array
# that could never carry it, and the owner/reason/recheck obligations those entries
# were documented to have were never enforced. Each test here fails against the
# code as it was.

BeforeAll {
    $script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    . (Join-Path $script:RepoRoot 'Build-Scripts.ps1') | Out-Null

    function script:New-AllowlistFile {
        param($JavaScriptAdvisories)

        $document = [ordered]@{
            schemaVersion                = 1
            acceptedTransitiveLag        = @()
            acceptedJavaScriptAdvisories = @($JavaScriptAdvisories)
        }

        $path = Join-Path ([System.IO.Path]::GetTempPath()) ("librespot-allowlist-" + [guid]::NewGuid().ToString('N') + ".json")
        $document | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $path -Encoding UTF8
        return $path
    }
}

Describe 'Get-DependencyHealthJavaScriptAllowlist' {
    It 'returns the accepted advisories so an acceptance can actually take effect' {
        # Regression: the consumer read acceptedJavaScriptAdvisories off the result
        # of Get-DependencyHealthAllowlist, which is the transitive-lag array. The
        # property was never there, so every acceptance was ignored and the expiry
        # branch behind it was unreachable.
        $path = New-AllowlistFile -JavaScriptAdvisories @(
            @{ id = '1234'; owner = 'matt'; reason = 'documented'; recheckDate = '2027-01-01' }
        )

        try {
            $lag = Get-DependencyHealthAllowlist -Path $path
            [bool]$lag.PSObject.Properties['acceptedJavaScriptAdvisories'] | Should -BeFalse

            $accepted = @(Get-DependencyHealthJavaScriptAllowlist -Path $path)
            $accepted.Count | Should -Be 1
            [string]$accepted[0].id | Should -Be '1234'
            [string]$accepted[0].owner | Should -Be 'matt'
        } finally {
            Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'Get-DependencyHealthAllowlist' {
    It 'refuses a JavaScript advisory acceptance that names no owner, reason or recheck date' -ForEach @(
        @{ Missing = 'owner';       Entry = @{ id = '1'; reason = 'r'; recheckDate = '2027-01-01' } }
        @{ Missing = 'reason';      Entry = @{ id = '1'; owner = 'matt'; recheckDate = '2027-01-01' } }
        @{ Missing = 'recheckDate'; Entry = @{ id = '1'; owner = 'matt'; reason = 'r' } }
        @{ Missing = 'id';          Entry = @{ owner = 'matt'; reason = 'r'; recheckDate = '2027-01-01' } }
    ) {
        $path = New-AllowlistFile -JavaScriptAdvisories @($Entry)

        try {
            { Get-DependencyHealthAllowlist -Path $path } |
                Should -Throw -ExpectedMessage "*missing '$Missing'*"
        } finally {
            Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        }
    }

    It 'accepts a complete JavaScript advisory entry' {
        $path = New-AllowlistFile -JavaScriptAdvisories @(
            @{ id = '1'; owner = 'matt'; reason = 'r'; recheckDate = '2027-01-01' }
        )

        try {
            { Get-DependencyHealthAllowlist -Path $path } | Should -Not -Throw
        } finally {
            Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'Get-LibreSpotJavaScriptAudit' {
    It 'reports a failure when the workspace cannot be audited instead of reporting a clean run' {
        # Regression: pnpm audits the lockfile of the directory it runs in. Run from
        # the repository root the command failed with ERR_PNPM_AUDIT_NO_LOCKFILE,
        # that error parsed as JSON, the advisories property was absent, and the loop
        # skipped on and called it clean.
        if (-not (Get-Command pnpm -ErrorAction SilentlyContinue)) {
            Set-ItResult -Skipped -Because 'pnpm is not installed on this machine.'
            return
        }

        $workspace = Join-Path ([System.IO.Path]::GetTempPath()) ("librespot-audit-" + [guid]::NewGuid().ToString('N'))
        New-Item -Path $workspace -ItemType Directory -Force | Out-Null
        '{ "name": "audit-fixture", "version": "0.0.0" }' |
            Set-Content -LiteralPath (Join-Path $workspace 'package.json') -Encoding UTF8

        try {
            $audit = Get-LibreSpotJavaScriptAudit -WorkspacePath $workspace

            $audit.ran | Should -BeTrue
            @($audit.failures).Count | Should -BeGreaterThan 0
            (@($audit.failures) -join ' ') | Should -BeLike '*pnpm audit*'
        } finally {
            Remove-Item -LiteralPath $workspace -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'says so rather than staying silent when there is nothing to audit' {
        $workspace = Join-Path ([System.IO.Path]::GetTempPath()) ("librespot-audit-" + [guid]::NewGuid().ToString('N'))
        New-Item -Path $workspace -ItemType Directory -Force | Out-Null

        try {
            $audit = Get-LibreSpotJavaScriptAudit -WorkspacePath $workspace

            $audit.ran | Should -BeFalse
            $audit.reason | Should -Not -BeNullOrEmpty
        } finally {
            Remove-Item -LiteralPath $workspace -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
