using FsCheck.Xunit;
using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class PropertyTests
{
    [Property(MaxTest = 500)]
    public bool NormalizeConfiguration_CacheLimitAlwaysInRange(int cacheLimit)
    {
        var config = new InstallConfiguration { SpotX_CacheLimit = cacheLimit };
        var n = AppCatalog.NormalizeConfiguration(config);
        return n.SpotX_CacheLimit >= 0 && n.SpotX_CacheLimit <= 50_000;
    }

    [Property(MaxTest = 200)]
    public bool NormalizeConfiguration_LyricsInvariantsHold(bool lyricsEnabled, bool lyricsBlock, bool oldLyrics)
    {
        var config = new InstallConfiguration
        {
            SpotX_LyricsEnabled = lyricsEnabled,
            SpotX_LyricsBlock = lyricsBlock,
            SpotX_OldLyrics = oldLyrics
        };
        var n = AppCatalog.NormalizeConfiguration(config);
        if (n.SpotX_LyricsBlock && !n.SpotX_LyricsEnabled) return false;
        if (n.SpotX_OldLyrics && !n.SpotX_LyricsEnabled) return false;
        if (n.SpotX_OldLyrics && n.SpotX_LyricsBlock) return false;
        return true;
    }

    [Property(MaxTest = 200)]
    public bool NormalizeConfiguration_SidebarInvariantHolds(bool sidebarOff, bool sidebarClr)
    {
        var config = new InstallConfiguration
        {
            SpotX_RightSidebarOff = sidebarOff,
            SpotX_RightSidebarClr = sidebarClr
        };
        var n = AppCatalog.NormalizeConfiguration(config);
        return !(n.SpotX_RightSidebarClr && n.SpotX_RightSidebarOff);
    }

    [Property(MaxTest = 200)]
    public bool NormalizeConfiguration_BoolFieldsNeverThrow(
        bool a, bool b, bool c, bool d, bool e, bool f, bool g, bool h)
    {
        var config = new InstallConfiguration
        {
            SpotX_NewTheme = a,
            SpotX_PodcastsOff = b,
            SpotX_BlockUpdate = c,
            SpotX_Premium = d,
            SpotX_DisableStartup = e,
            SpotX_Plus = f,
            SpotX_ExpSpotify = g,
            SpotX_SendVersionOff = h
        };
        var n = AppCatalog.NormalizeConfiguration(config);
        return n is not null;
    }

    [Theory]
    [InlineData("")]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("zh-CN")]
    [InlineData("pt-BR")]
    [InlineData("INVALID")]
    [InlineData("'; DROP TABLE")]
    [InlineData("a\nb")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("en-US")]
    [InlineData("EN")]
    [InlineData("  en  ")]
    [InlineData("../../etc/passwd")]
    [InlineData("; rm -rf /")]
    public void NormalizeConfiguration_LanguageIsSafeOrEmpty(string language)
    {
        var config = new InstallConfiguration { SpotX_Language = language };
        var n = AppCatalog.NormalizeConfiguration(config);
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "", "en", "ru", "de", "fr", "es", "pt", "pt-BR", "it", "nl", "pl",
            "sv", "no", "da", "fi", "ja", "ko", "zh-CN", "zh-TW", "ar", "tr",
            "cs", "hu", "ro", "uk", "id", "th", "vi"
        };
        Assert.Contains(n.SpotX_Language, allowed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("curl")]
    [InlineData("webclient")]
    [InlineData("CURL")]
    [InlineData("  WebClient  ")]
    [InlineData("bits")]
    [InlineData("wget")]
    [InlineData("; evil")]
    public void NormalizeConfiguration_DownloadMethodIsSafe(string method)
    {
        var config = new InstallConfiguration { SpotX_DownloadMethod = method };
        var n = AppCatalog.NormalizeConfiguration(config);
        Assert.Contains(n.SpotX_DownloadMethod, new[] { "", "curl", "webclient" });
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("1.2.92")]
    [InlineData("1.2.53.440.x86")]
    [InlineData("  AUTO  ")]
    [InlineData("future-version")]
    [InlineData("")]
    [InlineData("1.2.92; rm -rf /")]
    [InlineData("../../etc")]
    public void NormalizeConfiguration_SpotifyVersionIdIsSafe(string versionId)
    {
        var config = new InstallConfiguration { SpotX_SpotifyVersionId = versionId };
        var n = AppCatalog.NormalizeConfiguration(config);
        var validIds = AppCatalog.SpotifyVersionManifest.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains(n.SpotX_SpotifyVersionId, validIds);
    }

    [Theory]
    [InlineData("spotify")]
    [InlineData("bad-theme")]
    [InlineData("")]
    [InlineData("  spotify  ")]
    [InlineData("<script>")]
    [InlineData("'; DROP")]
    public void NormalizeConfiguration_LyricsThemeIsSafe(string theme)
    {
        var config = new InstallConfiguration { SpotX_LyricsTheme = theme };
        var n = AppCatalog.NormalizeConfiguration(config);
        Assert.Contains(n.SpotX_LyricsTheme, AppCatalog.LyricsThemes);
    }

    [Theory]
    [InlineData("nonexistent")]
    [InlineData("")]
    [InlineData("../../etc")]
    [InlineData("Sleek")]
    [InlineData("Catppuccin")]
    [InlineData("(None - Marketplace Only)")]
    public void NormalizeConfiguration_ThemeIsSafe(string theme)
    {
        var config = new InstallConfiguration { Spicetify_Theme = theme };
        var n = AppCatalog.NormalizeConfiguration(config);
        Assert.True(AppCatalog.ThemeSchemes.ContainsKey(n.Spicetify_Theme),
            $"Theme '{n.Spicetify_Theme}' must be in ThemeSchemes");
        Assert.True(AppCatalog.ThemeSchemes[n.Spicetify_Theme].Contains(n.Spicetify_Scheme),
            $"Scheme '{n.Spicetify_Scheme}' must be valid for theme '{n.Spicetify_Theme}'");
    }

    [Fact]
    public void NormalizeConfiguration_ExtensionsFilterInvalidAndDeduplicate()
    {
        var payloads = new[]
        {
            "fullAppDisplay.js", "unknown.js", "", "../../etc/passwd",
            "fullAppDisplay.js", "beautifulLyrics.js", "songStats.js",
            "  trashbin.js  ", "a\nb"
        };
        var config = new InstallConfiguration { Spicetify_Extensions = [.. payloads] };
        var n = AppCatalog.NormalizeConfiguration(config);

        Assert.All(n.Spicetify_Extensions, ext =>
            Assert.True(AppCatalog.ExtensionDefinitions.Any(d => d.Key == ext), $"'{ext}' must be valid"));
        Assert.Equal(n.Spicetify_Extensions.Count, n.Spicetify_Extensions.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void NormalizeConfiguration_IsIdempotent()
    {
        var config = new InstallConfiguration
        {
            SpotX_CacheLimit = 75_000,
            SpotX_LyricsTheme = "invalid",
            Spicetify_Theme = "nonexistent",
            SpotX_LyricsEnabled = true,
            SpotX_LyricsBlock = true,
            SpotX_OldLyrics = true,
            SpotX_RightSidebarOff = true,
            SpotX_RightSidebarClr = true,
            SpotX_Language = "INVALID",
            SpotX_DownloadMethod = "bits",
            SpotX_SpotifyVersionId = "future",
            Spicetify_Extensions = ["unknown.js", "beautifulLyrics.js", "fullAppDisplay.js", "fullAppDisplay.js"]
        };
        var first = AppCatalog.NormalizeConfiguration(config);
        var second = AppCatalog.NormalizeConfiguration(first);

        Assert.Equal(first.SpotX_CacheLimit, second.SpotX_CacheLimit);
        Assert.Equal(first.SpotX_LyricsTheme, second.SpotX_LyricsTheme);
        Assert.Equal(first.Spicetify_Theme, second.Spicetify_Theme);
        Assert.Equal(first.Spicetify_Scheme, second.Spicetify_Scheme);
        Assert.Equal(first.SpotX_LyricsBlock, second.SpotX_LyricsBlock);
        Assert.Equal(first.SpotX_OldLyrics, second.SpotX_OldLyrics);
        Assert.Equal(first.SpotX_RightSidebarClr, second.SpotX_RightSidebarClr);
        Assert.Equal(first.SpotX_DownloadMethod, second.SpotX_DownloadMethod);
        Assert.Equal(first.SpotX_SpotifyVersionId, second.SpotX_SpotifyVersionId);
        Assert.Equal(first.SpotX_Language, second.SpotX_Language);
        Assert.Equal(first.Spicetify_Extensions, second.Spicetify_Extensions);
    }

    [Fact]
    public void NormalizeConfiguration_NullExtensionsDoNotThrow()
    {
        var config = new InstallConfiguration
        {
            Spicetify_Extensions = null!,
            SpotX_LyricsTheme = null!,
            Spicetify_Theme = null!,
            Spicetify_Scheme = null!,
            SpotX_Language = null!,
            SpotX_DownloadMethod = null!,
            SpotX_SpotifyVersionId = null!
        };
        var n = AppCatalog.NormalizeConfiguration(config);
        Assert.NotNull(n);
        Assert.NotNull(n.Spicetify_Extensions);
        Assert.NotNull(n.SpotX_LyricsTheme);
        Assert.NotNull(n.Spicetify_Theme);
        Assert.NotNull(n.SpotX_Language);
    }
}
