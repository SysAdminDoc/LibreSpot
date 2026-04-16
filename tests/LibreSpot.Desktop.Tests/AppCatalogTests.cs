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
}
