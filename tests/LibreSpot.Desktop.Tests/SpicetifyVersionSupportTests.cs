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

    [Fact]
    public void SchemaV2Fixture_ParsesAllowlistRangesAndMapMetadata()
    {
        var path = Path.Combine(ResolveRepoRoot(), "schemas", "spicetify-supported-versions-v2.json");
        var parsed = SpicetifySupportContract.TryParse(File.ReadAllText(path), out var contract, out var error);

        Assert.True(parsed, error);
        Assert.NotNull(contract);
        Assert.Equal(2, contract!.SchemaVersion);
        Assert.Equal("allowlist", contract.Policy);
        Assert.Equal("classic", contract.DefaultMapStatus);
        Assert.Empty(contract.ExactVersions);
        Assert.Single(contract.Ranges);
        Assert.Equal("1020094", contract.Maps["1.2.94"].ClassmapKey);
        Assert.Equal("modular", contract.Maps["1.2.94"].Status);
    }

    [Theory]
    [InlineData("1.2.93.667", SpicetifySupportVerdict.Allowlisted, true, null)]
    [InlineData("1.2.95", SpicetifySupportVerdict.Degraded, true, "1.2.94")]
    [InlineData("1.2.69", SpicetifySupportVerdict.Refused, false, null)]
    [InlineData("1.3.0", SpicetifySupportVerdict.Refused, false, null)]
    public void SchemaV2Fixture_ClassifiesAllowlistedDegradedAndRefusedVersions(
        string rawVersion,
        SpicetifySupportVerdict expectedVerdict,
        bool expectedCanApply,
        string? expectedFallback)
    {
        var contract = LoadFixture();
        var result = contract.Evaluate(rawVersion);

        Assert.Equal(expectedVerdict, result.Verdict);
        Assert.Equal(expectedCanApply, result.CanApply);
        Assert.Equal(expectedFallback, result.FallbackVersion);
        Assert.Equal(expectedVerdict == SpicetifySupportVerdict.Refused ? 1 : 0, result.SupportCommandExitCode);
    }

    [Fact]
    public void SchemaV2Fixture_UnknownVersionFailsOpenForApplyButNotAutoReapply()
    {
        var result = LoadFixture().Evaluate("not-a-spotify-version");

        Assert.Equal(SpicetifySupportVerdict.Unknown, result.Verdict);
        Assert.True(result.CanApply);
        Assert.False(result.CanAutoApply);
        Assert.Equal(0, result.SupportCommandExitCode);
    }

    [Fact]
    public void MissingOrMalformedContractFailsClosed()
    {
        var missing = SpicetifySupportResult.Unavailable("1.2.95", "missing");
        Assert.False(missing.ListAvailable);
        Assert.True(missing.ContractUnavailable);
        Assert.False(missing.CanApply);
        Assert.False(missing.CanAutoApply);
        Assert.Equal(1, missing.SupportCommandExitCode);

        Assert.False(
            SpicetifySupportContract.TryParse("{\"schema_version\":99}", out _, out var error));
        Assert.Contains("unsupported", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V3FeatureDetectionActivatesContractOnlyForNewerCliMajor()
    {
        var fixture = File.ReadAllText(Path.Combine(ResolveRepoRoot(), "schemas", "spicetify-supported-versions-v2.json"));

        var v2 = SpicetifyV3ConflictDetector.EvaluateSupportContract("2.44.0", "1.2.95", "not-json");
        Assert.False(v2.IsFeatureActive);
        Assert.Equal(SpicetifySupportVerdict.Unknown, v2.Result.Verdict);

        var v3 = SpicetifyV3ConflictDetector.EvaluateSupportContract("3.0.0-beta.1", "1.2.95", fixture);
        Assert.True(v3.IsFeatureActive);
        Assert.Equal(SpicetifySupportVerdict.Degraded, v3.Result.Verdict);
        Assert.Equal("1.2.94", v3.Result.FallbackVersion);

        var malformedV3 = SpicetifyV3ConflictDetector.EvaluateSupportContract("3.0.0-beta.1", "1.2.95", "not-json");
        Assert.True(malformedV3.IsFeatureActive);
        Assert.False(malformedV3.Result.ListAvailable);
        Assert.True(malformedV3.Result.ContractUnavailable);
        Assert.False(malformedV3.Result.CanApply);
        Assert.False(malformedV3.Result.CanAutoApply);
        Assert.Equal(1, malformedV3.Result.SupportCommandExitCode);
    }

    private static SpicetifySupportContract LoadFixture()
    {
        var path = Path.Combine(ResolveRepoRoot(), "schemas", "spicetify-supported-versions-v2.json");
        Assert.True(SpicetifySupportContract.TryParse(File.ReadAllText(path), out var contract, out var error), error);
        return contract!;
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
