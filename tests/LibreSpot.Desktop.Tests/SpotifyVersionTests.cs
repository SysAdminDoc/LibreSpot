using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class SpotifyVersionTests
{
    [Theory]
    [InlineData("v1.2.94", 1, 2, 94)]
    [InlineData("V1.2.94", 1, 2, 94)]
    [InlineData("1.2.94", 1, 2, 94)]
    [InlineData("  1.2.94  ", 1, 2, 94)]
    [InlineData("1.2.96.518", 1, 2, 96)]
    [InlineData("v1.2.96.518", 1, 2, 96)]
    [InlineData("1.2.93-beta.2", 1, 2, 93)]
    [InlineData("1.2.93+build7", 1, 2, 93)]
    // FileVersionInfo.FileVersion is free-form vendor text, so whitespace ends
    // the version the same way a prerelease suffix does.
    [InlineData("1.2.3 rc1", 1, 2, 3)]
    [InlineData("1.2.3 (build 4)", 1, 2, 3)]
    [InlineData("1.2.3 rc1", 1, 2, 3)]
    [InlineData("1.2.96.518 (Release)", 1, 2, 96)]
    // Spotify's own FileVersion runs to five pieces with a git hash on the end.
    // These are the strings in AppCatalog.SpotifyVersionEntry.
    [InlineData("1.2.90.451.gb094aab0", 1, 2, 90)]
    [InlineData("1.2.86.502.g8cd7fb22", 1, 2, 86)]
    [InlineData("1.2.5.1006.g22820f93", 1, 2, 5)]
    public void TryParse_ReadsSpotifyBuildsAtThreeComponentPrecision(string value, int major, int minor, int build)
    {
        Assert.True(SpotifyVersion.TryParse(value, out var version));
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1.2")]
    [InlineData("v1.2")]
    [InlineData("latest")]
    [InlineData("1.2.x")]
    [InlineData("-1.2.3")]
    [InlineData("1.2.3abc")]
    public void TryParse_RejectsWhatIsNotASpotifyBuild(string? value)
    {
        Assert.False(SpotifyVersion.TryParse(value, out var version));
        Assert.Equal(new Version(0, 0, 0), version);
    }

    [Fact]
    public void TryParse_TreatsAFourthComponentAsTheSameSupportedBuild()
    {
        Assert.True(SpotifyVersion.TryParse("1.2.93.400", out var detected));
        Assert.True(SpotifyVersion.TryParse("1.2.93", out var supportedThrough));
        Assert.Equal(0, detected.CompareTo(supportedThrough));
    }

    [Theory]
    [InlineData("v2.44.0", "v", 2, 44, 0, -1)]
    [InlineData("2.44.0.1", null, 2, 44, 0, 1)]
    [InlineData("3.1", null, 3, 1, 0, -1)]
    [InlineData("v3.1.0-beta.2", "v", 3, 1, 0, -1)]
    public void TryParseReleaseTag_KeepsTheFourthComponentAndPadsShortTags(
        string tag,
        string? prefixToStrip,
        int major,
        int minor,
        int build,
        int revision)
    {
        Assert.True(SpotifyVersion.TryParseReleaseTag(tag, prefixToStrip, out var version));
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(build, version.Build);
        Assert.Equal(revision, version.Revision);
    }

    [Fact]
    public void TryParseReleaseTag_OrdersAFourthComponentAboveTheBaseTag()
    {
        Assert.True(SpotifyVersion.TryParseReleaseTag("2.44.0.1", null, out var patched));
        Assert.True(SpotifyVersion.TryParseReleaseTag("2.44.0", null, out var baseline));
        Assert.True(patched > baseline);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("main")]
    [InlineData("v")]
    // Junk past the component cap is still junk: the old ParseSortableVersion
    // required every piece to be numeric and sorted such a tag last.
    [InlineData("1.2.3.4.garbage")]
    [InlineData("1.2.3.4.5")]
    public void TryParseReleaseTag_RejectsNonNumericTags(string? tag)
    {
        Assert.False(SpotifyVersion.TryParseReleaseTag(tag, "v", out var version));
        Assert.Equal(new Version(0, 0, 0), version);
    }

    [Theory]
    [InlineData("2.44.0", 2)]
    [InlineData("v3.0.0", 3)]
    [InlineData("3.1.2-dev", 3)]
    [InlineData("3.0", 3)]
    [InlineData("3", 3)]
    [InlineData("  v10.1  ", 10)]
    public void TryParseMajor_ReadsAMajorFromAPartialVersion(string value, int expected)
    {
        Assert.True(SpotifyVersion.TryParseMajor(value, out var major));
        Assert.Equal(expected, major);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Dev")]
    [InlineData("v")]
    public void TryParseMajor_RejectsWhatHasNoLeadingNumber(string? value)
    {
        Assert.False(SpotifyVersion.TryParseMajor(value, out var major));
        Assert.Equal(0, major);
    }

    [Fact]
    public void TheTwoEntryPointsTreatTrailingContentDifferentlyOnPurpose()
    {
        // A Spotify build carries a git hash after the third component, so
        // TryParse reads the first three and ignores the rest. A release tag is
        // ordered against other tags, so trailing junk makes it unreadable and
        // it sorts last instead of being read loosely.
        Assert.True(SpotifyVersion.TryParse("1.2.90.451.gb094aab0", out var build));
        Assert.Equal(new Version(1, 2, 90), build);

        Assert.False(SpotifyVersion.TryParseReleaseTag("1.2.90.451.gb094aab0", null, out var tag));
        Assert.Equal(new Version(0, 0, 0), tag);

        // Junk inside the first three components is unreadable either way.
        Assert.False(SpotifyVersion.TryParse("1.2.beta.451", out _));
        Assert.False(SpotifyVersion.TryParseReleaseTag("1.2.beta.451", null, out _));
    }

    [Fact]
    public void TryParse_ReadsEveryPinnedSpotifyBuildInTheCatalog()
    {
        // The catalog is the real input to CheckInstalledSpotifyCompatibility,
        // SpicetifySupportContract.Evaluate, and the compatibility cards.
        var unreadable = AppCatalog.SpotifyVersionManifest
            .Select(entry => entry.Version)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Where(version => !SpotifyVersion.TryParse(version, out _))
            .ToList();

        Assert.True(unreadable.Count == 0, $"Unreadable pinned Spotify builds: {string.Join(", ", unreadable)}");
    }

    [Fact]
    public void CheckInstalledSpotifyCompatibility_WarnsForABuildPastTheTestedCeiling()
    {
        // A five-piece build must still be compared, not silently skipped.
        Assert.NotEmpty(AppCatalog.CheckInstalledSpotifyCompatibility("9.9.9.9.gdeadbeef"));
        Assert.NotEmpty(AppCatalog.CheckInstalledSpotifyCompatibility("9.9.9"));
        Assert.Empty(AppCatalog.CheckInstalledSpotifyCompatibility("1.2.90.451.gb094aab0"));
    }
}
