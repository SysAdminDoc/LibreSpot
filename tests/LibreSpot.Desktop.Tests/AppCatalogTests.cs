using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class AppCatalogTests
{
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
