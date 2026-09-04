#requires -Version 5.1

# The pinned tuple can only advance to the highest build that SpotX, a published
# classmap and Spicetify's declared range all cover. Before this the drift check
# compared one source, so a decision could be taken from the highest bound or
# from a headline about the newest Spotify and land on a build some part of the
# stack had never seen.

BeforeAll {
    $script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    # Dot-sourced for its functions; the usage banner goes to every stream.
    . (Join-Path $script:RepoRoot 'Build-Scripts.ps1') *> $null
}

Describe 'Next reviewable Spotify build' {
    It 'is bounded by the lowest of the three sources when the classmap is lowest' {
        $decision = Get-LibreSpotReviewableSpotifyBound `
            -SpotXTarget '1.2.99' `
            -NewestClassmap '1.2.95' `
            -SpicetifyDeclaredMax '1.2.96' `
            -PinnedVersion '1.2.93'

        $decision.Determinate | Should -BeTrue
        $decision.Bound | Should -Be '1.2.95'
        $decision.BoundedBy | Should -Be 'published classmap'
        $decision.BoundSource | Should -Be 'https://github.com/spicetify/classmaps'
        $decision.AbovePin | Should -BeTrue
    }

    It 'is bounded by the declared Spicetify range when that is lowest' {
        $decision = Get-LibreSpotReviewableSpotifyBound `
            -SpotXTarget '1.2.99' `
            -NewestClassmap '1.2.97' `
            -SpicetifyDeclaredMax '1.2.96' `
            -PinnedVersion '1.2.93'

        $decision.Bound | Should -Be '1.2.96'
        $decision.BoundedBy | Should -Be 'Spicetify declared range'
    }

    It 'reports no reviewable build when the bound equals the pin' {
        $decision = Get-LibreSpotReviewableSpotifyBound `
            -SpotXTarget '1.2.99' `
            -NewestClassmap '1.2.93' `
            -SpicetifyDeclaredMax '1.2.96' `
            -PinnedVersion '1.2.93'

        $decision.Bound | Should -Be '1.2.93'
        $decision.AbovePin | Should -BeFalse
    }

    It 'warns when public Spotify is ahead of what the stack covers' {
        $decision = Get-LibreSpotReviewableSpotifyBound `
            -SpotXTarget '1.2.99' `
            -NewestClassmap '1.2.97' `
            -SpicetifyDeclaredMax '1.2.96' `
            -PinnedVersion '1.2.93' `
            -PublicVersion '1.2.98'

        $decision.PublicIsAhead | Should -BeTrue
    }

    It 'does not invent a bound from an unknown source' {
        $decision = Get-LibreSpotReviewableSpotifyBound `
            -SpotXTarget '' `
            -NewestClassmap '1.2.97' `
            -SpicetifyDeclaredMax '1.2.96' `
            -PinnedVersion '1.2.93'

        $decision.Determinate | Should -BeFalse
        $decision.Bound | Should -BeNullOrEmpty
        $decision.MissingSources | Should -Contain 'SpotX target'
    }

    It 'names every source it consulted so a reader can check each bound' {
        $decision = Get-LibreSpotReviewableSpotifyBound `
            -SpotXTarget '1.2.99' `
            -NewestClassmap '1.2.97' `
            -SpicetifyDeclaredMax '1.2.96' `
            -PinnedVersion '1.2.93'

        @($decision.Sources).Count | Should -Be 3
        foreach ($entry in $decision.Sources) {
            $entry.Name | Should -Not -BeNullOrEmpty
            $entry.Source | Should -Not -BeNullOrEmpty
        }
    }

    It 'reads the declared range from the same baseline the product ships' {
        $baseline = Get-Content -LiteralPath (Join-Path $script:RepoRoot 'schemas/compatibility-baseline.json') -Raw | ConvertFrom-Json
        $baseline.spicetifyCli.windowsDeclaredMaxSpotify | Should -Not -BeNullOrEmpty

        $script = [System.IO.File]::ReadAllText((Join-Path $script:RepoRoot 'Build-Scripts.ps1'))
        $script | Should -Match 'windowsDeclaredMaxSpotify'
        $script | Should -Match 'Get-LibreSpotUpstreamSpotifyBounds'
        $script | Should -Match 'Write-LibreSpotReviewableSpotifyBound'
    }
}
