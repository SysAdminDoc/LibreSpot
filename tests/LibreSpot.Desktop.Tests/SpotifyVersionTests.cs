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
    [InlineData("1.2.96.518 (Release)", 1, 2, 96)]
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

    [Theory]
    [InlineData("1.2.96.518 (Release)")]
    [InlineData("1.2.3 rc1")]
    [InlineData("1.2.3.4.garbage")]
    [InlineData("1.2.3abc")]
    [InlineData("1.2")]
    public void BothEntryPointsAgreeOnWhetherAStringIsAVersion(string value)
    {
        // They differ in precision and in how short a value may be, but never
        // in whether trailing content invalidates the whole string.
        var spotify = SpotifyVersion.TryParse(value, out _);
        var tag = SpotifyVersion.TryParseReleaseTag(value, null, out _);

        // "1.2" is the one legitimate disagreement: a Spotify build always has
        // three components, an upstream tag may not.
        if (value == "1.2")
        {
            Assert.False(spotify);
            Assert.True(tag);
            return;
        }

        Assert.Equal(spotify, tag);
    }
}
