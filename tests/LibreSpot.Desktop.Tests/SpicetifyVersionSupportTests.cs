using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class SpicetifyVersionSupportTests
{
    [Theory]
    [InlineData("2.44.0", true, 2)]
    [InlineData("3.0.0", true, 3)]
    [InlineData("v3.1.2-dev", true, 3)]
    [InlineData("10.0.0", true, 10)]
    [InlineData("1.2.14", true, 1)]
    [InlineData("", false, 0)]
    [InlineData(null, false, 0)]
    [InlineData("Dev", false, 0)]
    public void TryGetMajor_ParsesLeadingMajor(string? version, bool expectedParsed, int expectedMajor)
    {
        var parsed = SpicetifyVersionSupport.TryGetMajor(version, out var major);

        Assert.Equal(expectedParsed, parsed);
        Assert.Equal(expectedMajor, major);
    }

    [Theory]
    [InlineData("3.0.0", true)]
    [InlineData("v3.1.2-dev", true)]
    [InlineData("4.0.0", true)]
    [InlineData("2.44.0", false)]
    [InlineData("1.2.0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("Dev", false)]
    public void IsUnsupportedMajor_OnlyFlagsNewerThanSupported(string? version, bool expectedUnsupported)
    {
        Assert.Equal(expectedUnsupported, SpicetifyVersionSupport.IsUnsupportedMajor(version));
    }

    [Fact]
    public void SupportedMajor_IsTwo()
    {
        // LibreSpot pins Spicetify 2.x; the guard exists for a future v3 contract.
        Assert.Equal(2, SpicetifyVersionSupport.SupportedMajor);
    }
}
