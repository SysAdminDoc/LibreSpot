using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class AppCatalogTests
{
    [Fact]
    public void NormalizeConfiguration_StampsCurrentSchemaVersion()
    {
        var configuration = new InstallConfiguration
        {
            ConfigSchemaVersion = 0
        };

        var normalized = AppCatalog.NormalizeConfiguration(configuration);

        Assert.Equal(AppCatalog.CurrentConfigSchemaVersion, normalized.ConfigSchemaVersion);
    }

    [Fact]
    public void NormalizeConfiguration_RejectsFutureSchemaVersion()
    {
        var configuration = new InstallConfiguration
        {
            ConfigSchemaVersion = AppCatalog.CurrentConfigSchemaVersion + 1
        };

        var ex = Assert.Throws<InvalidOperationException>(() => AppCatalog.NormalizeConfiguration(configuration));

        Assert.Contains("Saved config schema version", ex.Message);
        Assert.Contains("newer than this LibreSpot build supports", ex.Message);
    }

    [Fact]
    public void NormalizeConfiguration_DisablesSidebarStylingWhenSidebarIsHidden()
    {
        var configuration = new InstallConfiguration
        {
            SpotX_RightSidebarOff = true,
            SpotX_RightSidebarClr = true
        };

        var normalized = AppCatalog.NormalizeConfiguration(configuration);

        Assert.True(normalized.SpotX_RightSidebarOff);
        Assert.False(normalized.SpotX_RightSidebarClr);
    }

    [Fact]
    public void NormalizeConfiguration_RejectsUnknownSelectionsAndDeduplicatesExtensions()
    {
        var configuration = new InstallConfiguration
        {
            SpotX_LyricsTheme = "not-a-real-lyrics-theme",
            Spicetify_Theme = "not-a-real-theme",
            Spicetify_Scheme = "not-a-real-scheme",
            Spicetify_Extensions =
            [
                "fullAppDisplay.js",
                "trashbin.js",
                "fullAppDisplay.js",
                "unknown.js",
                ""
            ]
        };

        var normalized = AppCatalog.NormalizeConfiguration(configuration);

        Assert.Equal("spotify", normalized.SpotX_LyricsTheme);
        Assert.Equal("(None - Marketplace Only)", normalized.Spicetify_Theme);
        Assert.Equal("Default", normalized.Spicetify_Scheme);
        Assert.Equal(["fullAppDisplay.js", "trashbin.js"], normalized.Spicetify_Extensions);
    }

    [Fact]
    public void NormalizeConfiguration_MigratesRenamedCommunityExtensionsAndDropsDeletedOnes()
    {
        var configuration = new InstallConfiguration
        {
            Spicetify_Extensions =
            [
                "beautifulLyrics.js",
                "playlistIcons.js",
                "songStats.js",
                "beautiful-lyrics.mjs"
            ]
        };

        var normalized = AppCatalog.NormalizeConfiguration(configuration);

        Assert.Equal(["beautiful-lyrics.mjs", "playlist-icons.js"], normalized.Spicetify_Extensions);
        Assert.DoesNotContain(AppCatalog.ExtensionDefinitions, item => item.Key == "songStats.js");
    }

    [Fact]
    public void NormalizeConfiguration_HandlesNullInputsAndClampsCacheLimit()
    {
        var configuration = new InstallConfiguration
        {
            SpotX_CacheLimit = 999_999,
            SpotX_LyricsTheme = null!,
            Spicetify_Theme = null!,
            Spicetify_Scheme = null!,
            Spicetify_Extensions = null!
        };

        var normalized = AppCatalog.NormalizeConfiguration(configuration);

        Assert.Equal(50_000, normalized.SpotX_CacheLimit);
        Assert.Equal("spotify", normalized.SpotX_LyricsTheme);
        Assert.Equal("(None - Marketplace Only)", normalized.Spicetify_Theme);
        Assert.Equal("Default", normalized.Spicetify_Scheme);
        Assert.NotNull(normalized.Spicetify_Extensions);
        Assert.Empty(normalized.Spicetify_Extensions);
    }

    [Theory]
    [InlineData("ru", "ru")]
    [InlineData("zh-Hans", "zh-Hans")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("xx-FAKE", "en")]
    [InlineData("", "en")]
    public void NormalizeConfiguration_RestrictsUiCulture(string input, string expected)
    {
        var configuration = new InstallConfiguration { UiCulture = input };

        var normalized = AppCatalog.NormalizeConfiguration(configuration);
        var clone = normalized.Clone();

        Assert.Equal(expected, normalized.UiCulture);
        Assert.Equal(expected, clone.UiCulture);
    }

    [Fact]
    public void NormalizeConfiguration_CanonicalizesAdvancedCompatibilitySelections()
    {
        var configuration = new InstallConfiguration
        {
            SpotX_DownloadMethod = " CURL ",
            SpotX_SpotifyVersionId = " 1.2.53.440.X86 "
        };

        var normalized = AppCatalog.NormalizeConfiguration(configuration);

        Assert.Equal("curl", normalized.SpotX_DownloadMethod);
        Assert.Equal("1.2.53.440.x86", normalized.SpotX_SpotifyVersionId);
    }

    [Fact]
    public void NormalizeConfiguration_PreservesReviewedCustomPatches()
    {
        var configuration = new InstallConfiguration
        {
            SpotX_CustomPatchesEnabled = true,
            SpotX_CustomPatchesJson = "  { \"xpui\": { \"match\": \"one\", \"replace\": \"two\" } }  ",
            SpotX_CustomPatchesSourceUrl = " https://example.test/patches.json ",
            SpotX_CustomPatchesFetchedAtUtc = DateTimeOffset.Parse("2026-06-30T12:34:56-04:00"),
            SpotX_CustomPatchesSourceByteCount = 42,
            SpotX_CustomPatchesSourceSha256 = "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789"
        };

        var normalized = AppCatalog.NormalizeConfiguration(configuration);
        var clone = normalized.Clone();

        Assert.True(normalized.SpotX_CustomPatchesEnabled);
        Assert.Equal("{ \"xpui\": { \"match\": \"one\", \"replace\": \"two\" } }", normalized.SpotX_CustomPatchesJson);
        Assert.Equal("https://example.test/patches.json", normalized.SpotX_CustomPatchesSourceUrl);
        Assert.Equal(DateTimeOffset.Parse("2026-06-30T16:34:56Z"), normalized.SpotX_CustomPatchesFetchedAtUtc);
        Assert.Equal(42, normalized.SpotX_CustomPatchesSourceByteCount);
        Assert.Equal("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789", normalized.SpotX_CustomPatchesSourceSha256);
        Assert.True(clone.SpotX_CustomPatchesEnabled);
        Assert.Equal(normalized.SpotX_CustomPatchesJson, clone.SpotX_CustomPatchesJson);
        Assert.Equal(normalized.SpotX_CustomPatchesSourceUrl, clone.SpotX_CustomPatchesSourceUrl);
        Assert.Equal(normalized.SpotX_CustomPatchesFetchedAtUtc, clone.SpotX_CustomPatchesFetchedAtUtc);
        Assert.Equal(normalized.SpotX_CustomPatchesSourceByteCount, clone.SpotX_CustomPatchesSourceByteCount);
        Assert.Equal(normalized.SpotX_CustomPatchesSourceSha256, clone.SpotX_CustomPatchesSourceSha256);
    }

    [Fact]
    public void Clone_CoversEveryPublicSettableProperty()
    {
        var settableProperties = typeof(InstallConfiguration)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToList();

        var source = AppCatalog.CreateRecommendedConfiguration();
        source.Spicetify_Extensions = new List<string> { "test.js" };
        source.Spicetify_CustomApps = new List<string> { "stats" };
        source.SpotX_CustomPatchesJson = "{\"test\": true}";
        source.SpotX_CustomPatchesSourceUrl = "https://example.test/patches.json";
        source.SpotX_CustomPatchesFetchedAtUtc = DateTimeOffset.UtcNow;
        source.SpotX_CustomPatchesSourceByteCount = 42;
        source.SpotX_CustomPatchesSourceSha256 = "abc123";
        source.UiCulture = "ru";

        var clone = source.Clone();

        foreach (var property in settableProperties)
        {
            var sourceValue = property.GetValue(source);
            var cloneValue = property.GetValue(clone);
            if (sourceValue is System.Collections.IList sourceList)
            {
                var cloneList = Assert.IsAssignableFrom<System.Collections.IList>(cloneValue);
                Assert.Equal(sourceList.Count, cloneList.Count);
                Assert.NotSame(sourceList, cloneList);
            }
            else
            {
                Assert.Equal(sourceValue, cloneValue);
            }
        }
    }

    [Fact]
    public void SpotifyVersionManifest_UsesCurrentPinnedSpotXBaseline()
    {
        var current = Assert.Single(AppCatalog.SpotifyVersionManifest, entry => entry.Id == AppCatalog.PinnedSpotXSpotifyVersionId);

        Assert.Equal(AppCatalog.PinnedSpotXSpotifyVersion, current.Version);
        Assert.Contains("current pinned", current.Label);
        Assert.Contains("max-tested Windows CSS-map baseline", current.Notes);
        Assert.Contains(AppCatalog.SpotifyVersionManifest, entry => entry.Id == "1.2.86.502");
    }

    [Fact]
    public void CompatibilityBaseline_FlagsPinnedSpotXNewerThanSpicetifyWindowsMaximum()
    {
        Assert.Equal("2.43.2", AppCatalog.PinnedSpicetifyCliVersion);
        Assert.Equal("1.2.14", AppCatalog.SpicetifyWindowsMinTestedSpotify);
        Assert.Equal("1.2.88", AppCatalog.SpicetifyWindowsMaxTestedSpotify);
        Assert.True(
            Version.Parse(AppCatalog.PinnedSpotXSpotifyVersionId) > Version.Parse(AppCatalog.SpicetifyWindowsMaxTestedSpotify),
            "Pinned SpotX target should remain visibly newer than Spicetify CLI's Windows max-tested Spotify baseline until upstream support catches up.");
    }

    [Fact]
    public void MaintenanceActions_ExposeMarketplaceRepair()
    {
        var action = Assert.Single(AppCatalog.MaintenanceActions, item => item.Action == "RepairMarketplace");

        Assert.Equal("Repair and open Marketplace", action.Title);
        Assert.False(action.IsDestructive);
        Assert.Contains("spotify:app:marketplace", action.Description);
    }

    [Fact]
    public void MaintenanceActions_ExposeMarketplaceOpenOnlyAction()
    {
        var action = Assert.Single(AppCatalog.MaintenanceActions, item => item.Action == "OpenMarketplace");

        Assert.Equal("Open Marketplace", action.Title);
        Assert.False(action.IsDestructive);
        Assert.Contains("already installed and registered", action.Description);
    }

    [Fact]
    public void NormalizeConfiguration_FallsBackWhenAdvancedCompatibilitySelectionsAreUnknown()
    {
        var configuration = new InstallConfiguration
        {
            SpotX_DownloadMethod = "bits",
            SpotX_SpotifyVersionId = "future-build"
        };

        var normalized = AppCatalog.NormalizeConfiguration(configuration);

        Assert.Equal(string.Empty, normalized.SpotX_DownloadMethod);
        Assert.Equal("auto", normalized.SpotX_SpotifyVersionId);
    }

    [Fact]
    public void NormalizeConfiguration_DisablesLyricsVariantsWhenLyricsPatchIsDisabled()
    {
        var configuration = new InstallConfiguration
        {
            SpotX_LyricsEnabled = false,
            SpotX_LyricsBlock = true,
            SpotX_OldLyrics = true
        };

        var normalized = AppCatalog.NormalizeConfiguration(configuration);

        Assert.False(normalized.SpotX_LyricsEnabled);
        Assert.False(normalized.SpotX_LyricsBlock);
        Assert.False(normalized.SpotX_OldLyrics);
    }

    [Fact]
    public void NormalizeConfiguration_ResolvesMutuallyExclusiveLyricsVariants()
    {
        var configuration = new InstallConfiguration
        {
            SpotX_LyricsEnabled = true,
            SpotX_LyricsBlock = true,
            SpotX_OldLyrics = true
        };

        var normalized = AppCatalog.NormalizeConfiguration(configuration);

        Assert.True(normalized.SpotX_LyricsBlock);
        Assert.False(normalized.SpotX_OldLyrics);
    }
}
