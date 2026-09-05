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

Describe 'Newest verified classmap' {
    BeforeAll {
        # A capture of https://raw.githubusercontent.com/spicetify/classmaps/main/index.json
        # taken on 2026-09-05. Pinned rather than fetched so this asserts the
        # parser, not the network, and so a future upstream change cannot
        # quietly turn a red test green.
        $script:ClassmapIndex = @'
        {
          "expose": {
            "file": "expose.json",
            "sha256": "3c609aca27255ad637cae51c1355482ef02fe6c1e61156d3e82039e1fe6a372a"
          },
          "keys": {
            "1020038": {
              "classmap": {
                "file": "classmap-1718192021222.json",
                "sha256": "29522d09a1de092d6ba4c68f249d8a0bcfa1539d0c0f74f9347328443ec09d73"
              }
            },
            "1020040": {
              "classmap": {
                "file": "classmap-190747c4b8f.json",
                "sha256": "d281c6c73b4ae40d9bddc5ade971b818a69325b0626e5347a3a47615df2fcde3"
              }
            },
            "1020045": {
              "classmap": {
                "file": "classmap-191b119b48e.json",
                "sha256": "97dd9b8882fa3e1eb39b63d6d52303a6550147e8553276ddf7639dcfde9b29ef"
              }
            },
            "1020084": {
              "classmap": {
                "file": "classmap-1a05f280501.json",
                "sha256": "e5800bf048dfe57043d5e3611da043d44f3480e1fa6dba4781085f9ec37c784e"
              },
              "meta": {
                "file": "META.json",
                "sha256": "0670e8c0d8e34b31612f986ba1bb3ab917c38fdbd4006255b4f27efdb48093ba"
              },
              "spotifyVersion": "1.2.84.476",
              "status": "verified"
            },
            "1020092": {
              "classmap": {
                "file": "classmap-19f8522e902.json",
                "sha256": "817cf36500c70d90d6c77944e86095b084f0c56482c4ef9e345fc3c211ffaa53"
              },
              "cssMapOverlay": {
                "file": "css-map.json",
                "sha256": "8c05d9ca347e4e13fb31013df4c42bdbf16046408b989afe4d0ea6a25037800e"
              },
              "meta": {
                "file": "META.json",
                "sha256": "6866bb9564e11aa019c5bc18f9185f2ee2f4ba4d696f9fb5e7905d5d0745a199"
              },
              "spotifyVersion": "1.2.92.148",
              "status": "verified"
            },
            "1020094": {
              "classmap": {
                "file": "classmap-19f856aefd5.json",
                "sha256": "d69d189567b5ddb7fcafffa9f4e7d84ee8b2ed27160ae168ae7bd8c8d03d7956"
              },
              "cssMapOverlay": {
                "file": "css-map.json",
                "sha256": "8c05d9ca347e4e13fb31013df4c42bdbf16046408b989afe4d0ea6a25037800e"
              },
              "meta": {
                "file": "META.json",
                "sha256": "1733318f5c5f6811057fd0a94f33213b50829d15a9e066b066c5ad3654ba1409"
              },
              "spotifyVersion": "1.2.94.583",
              "status": "verified"
            },
            "1020096": {
              "classmap": {
                "file": "classmap-19f856aefd5.json",
                "sha256": "d69d189567b5ddb7fcafffa9f4e7d84ee8b2ed27160ae168ae7bd8c8d03d7956"
              },
              "cssMapOverlay": {
                "file": "css-map.json",
                "sha256": "8c05d9ca347e4e13fb31013df4c42bdbf16046408b989afe4d0ea6a25037800e"
              },
              "meta": {
                "file": "META.json",
                "sha256": "5d5d57a74ebb2b2d7614258df1f6b81eac636cb3d138068e9dd1ff3595a539c4"
              },
              "spotifyVersion": "1.2.96.518",
              "status": "verified"
            },
            "1020097": {
              "classmap": {
                "file": "classmap-19f856aefd5.json",
                "sha256": "d69d189567b5ddb7fcafffa9f4e7d84ee8b2ed27160ae168ae7bd8c8d03d7956"
              },
              "cssMapOverlay": {
                "file": "css-map.json",
                "sha256": "8c05d9ca347e4e13fb31013df4c42bdbf16046408b989afe4d0ea6a25037800e"
              },
              "meta": {
                "file": "META.json",
                "sha256": "773129d3306739588e90c0abe4cf32311f8ac39a1448bca2b3984cf80cfe2c68"
              },
              "spotifyVersion": "1.2.97.270",
              "status": "verified"
            }
          },
          "version": 1
        }
'@
    }

    It 'reads the build from the index instead of the folder name' {
        # 1020097 is the highest key and upstream records it as 1.2.97.270.
        # The old directory-listing path could only ever say '1.2.97'.
        Get-LibreSpotNewestVerifiedClassmap -IndexJson $script:ClassmapIndex | Should -Be '1.2.97.270'
    }

    It 'ignores keys that carry no status' {
        # 1020038, 1020040 and 1020045 are the pre-versioned legacy maps. They
        # have neither spotifyVersion nor status, and a directory listing
        # counted them as coverage.
        $index = @'
{
  "keys": {
    "1020038": { "classmap": { "file": "classmap-1718192021222.json" } },
    "1020084": { "spotifyVersion": "1.2.84.476", "status": "verified" }
  }
}
'@
        Get-LibreSpotNewestVerifiedClassmap -IndexJson $index | Should -Be '1.2.84.476'
    }

    It 'does not count an unverified entry as coverage' {
        $index = @'
{
  "keys": {
    "1020096": { "spotifyVersion": "1.2.96.518", "status": "verified" },
    "1020099": { "spotifyVersion": "1.2.99.317", "status": "inherited" }
  }
}
'@
        Get-LibreSpotNewestVerifiedClassmap -IndexJson $index | Should -Be '1.2.96.518'
    }

    It 'reports unknown rather than guessing when the index cannot be parsed' {
        Get-LibreSpotNewestVerifiedClassmap -IndexJson 'not json at all' | Should -BeNullOrEmpty
        Get-LibreSpotNewestVerifiedClassmap -IndexJson '' | Should -BeNullOrEmpty
        Get-LibreSpotNewestVerifiedClassmap -IndexJson '{"nothing":true}' | Should -BeNullOrEmpty
        Get-LibreSpotNewestVerifiedClassmap -IndexJson '{"keys":{"1020096":{"spotifyVersion":"1.2.96.518"}}}' | Should -BeNullOrEmpty
    }

    It 'feeds the reviewable bound with the full four-part build' {
        $newest = Get-LibreSpotNewestVerifiedClassmap -IndexJson $script:ClassmapIndex
        $decision = Get-LibreSpotReviewableSpotifyBound `
            -SpotXTarget '1.2.99' `
            -NewestClassmap $newest `
            -SpicetifyDeclaredMax '1.2.98' `
            -PinnedVersion '1.2.93'

        $decision.Determinate | Should -BeTrue
        $decision.Bound | Should -Be '1.2.97'
        $decision.BoundedBy | Should -Be 'published classmap'
    }
}
