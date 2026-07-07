using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ArchitectureCompatibilityTests
{
    [Theory]
    [InlineData("auto", "any", "X64", null)]
    [InlineData("auto", "any", "X86", null)]
    [InlineData("auto", "any", "Arm64", null)]
    public void AutoEntry_IsAlwaysCompatible(string id, string arch, string host, string? expected)
    {
        var entry = new AppCatalog.SpotifyVersionEntry(id, "Auto", "", "Auto notes", arch);

        var result = AppCatalog.CheckArchitectureCompatibility(entry, host);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("X64")]
    [InlineData("Arm64")]
    public void X86Entry_WarnsOnNonX86Host(string host)
    {
        var entry = new AppCatalog.SpotifyVersionEntry(
            "1.2.53.440.x86",
            "1.2.53.440 (x86 / 32-bit only)",
            "1.2.53.440.g7b2f582a",
            "For 32-bit Windows.",
            "x86");

        var result = AppCatalog.CheckArchitectureCompatibility(entry, host);

        Assert.NotNull(result);
        Assert.Contains("32-bit", result);
        Assert.Contains(host, result);
    }

    [Fact]
    public void X86Entry_IsCompatibleOnX86Host()
    {
        var entry = new AppCatalog.SpotifyVersionEntry(
            "1.2.53.440.x86",
            "1.2.53.440 (x86 / 32-bit only)",
            "1.2.53.440.g7b2f582a",
            "For 32-bit Windows.",
            "x86");

        var result = AppCatalog.CheckArchitectureCompatibility(entry, "X86");

        Assert.Null(result);
    }

    [Fact]
    public void X64Entry_WarnsOnX86Host()
    {
        var entry = new AppCatalog.SpotifyVersionEntry(
            AppCatalog.PinnedSpotXSpotifyVersionId,
            $"{AppCatalog.PinnedSpotXSpotifyVersionId} (current pinned)",
            AppCatalog.PinnedSpotXSpotifyVersion,
            "Current pinned.",
            "x64");

        var result = AppCatalog.CheckArchitectureCompatibility(entry, "X86");

        Assert.NotNull(result);
        Assert.Contains("64-bit", result);
    }

    [Fact]
    public void X64Entry_IsCompatibleOnX64Host()
    {
        var entry = new AppCatalog.SpotifyVersionEntry(
            AppCatalog.PinnedSpotXSpotifyVersionId,
            $"{AppCatalog.PinnedSpotXSpotifyVersionId} (current pinned)",
            AppCatalog.PinnedSpotXSpotifyVersion,
            "Current pinned.",
            "x64");

        Assert.Null(AppCatalog.CheckArchitectureCompatibility(entry, "X64"));
    }

    [Fact]
    public void X64Entry_WarnsOnArm64Host()
    {
        var entry = new AppCatalog.SpotifyVersionEntry(
            AppCatalog.PinnedSpotXSpotifyVersionId,
            $"{AppCatalog.PinnedSpotXSpotifyVersionId} (current pinned)",
            AppCatalog.PinnedSpotXSpotifyVersion,
            "Current pinned.",
            "x64");

        var result = AppCatalog.CheckArchitectureCompatibility(entry, "ARM64");
        Assert.NotNull(result);
        Assert.Contains("emulation", result);
    }

    [Fact]
    public void LegacyOsEntry_WarnsOnWindows10Plus()
    {
        // This test runs on Win10+ CI/dev machines, so the warning should fire.
        var entry = new AppCatalog.SpotifyVersionEntry(
            "1.2.5.1006.win7",
            "1.2.5.1006 (Windows 7 / 8.1)",
            "1.2.5.1006.g22820f93",
            "Last build supported on legacy Windows.",
            "legacy-os");

        var result = AppCatalog.CheckArchitectureCompatibility(entry, "X64");

        // We are running on Win10+, so the warning must fire.
        if (Environment.OSVersion.Version.Major >= 10)
        {
            Assert.NotNull(result);
            Assert.Contains("Windows 7/8.1", result);
        }
    }

    [Fact]
    public void AnyArchEntry_NeverWarns()
    {
        var entry = new AppCatalog.SpotifyVersionEntry(
            "1.2.85.519",
            "1.2.85.519 (older stable)",
            "1.2.85.519.g7c42e2e8",
            "Last release before Canvas-home changes.",
            "any");

        Assert.Null(AppCatalog.CheckArchitectureCompatibility(entry, "X64"));
        Assert.Null(AppCatalog.CheckArchitectureCompatibility(entry, "X86"));
        Assert.Null(AppCatalog.CheckArchitectureCompatibility(entry, "Arm64"));
    }

    [Fact]
    public void NullEntry_ReturnsNull()
    {
        Assert.Null(AppCatalog.CheckArchitectureCompatibility(null!, "X64"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void UnknownHostArchitecture_TreatedAsNonX86(string? host)
    {
        var entry = new AppCatalog.SpotifyVersionEntry(
            "1.2.53.440.x86",
            "x86 build",
            "1.2.53.440",
            "x86 only.",
            "x86");

        // "Unknown" or blank host is not x86, so the x86-only entry should warn.
        var result = AppCatalog.CheckArchitectureCompatibility(entry, host!);

        Assert.NotNull(result);
    }

    [Fact]
    public void ManifestEntries_HaveArchitectureValues()
    {
        var validArchitectures = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "any", "x64", "x86", "legacy-os"
        };

        foreach (var entry in AppCatalog.SpotifyVersionManifest)
        {
            Assert.True(
                validArchitectures.Contains(entry.Architecture),
                $"Entry '{entry.Id}' has unexpected Architecture value '{entry.Architecture}'.");
        }
    }

    [Fact]
    public void X86ManifestEntry_IsMarkedX86()
    {
        var entry = AppCatalog.SpotifyVersionManifest.First(e => e.Id == "1.2.53.440.x86");

        Assert.Equal("x86", entry.Architecture);
    }

    [Fact]
    public void Win7ManifestEntry_IsMarkedLegacyOs()
    {
        var entry = AppCatalog.SpotifyVersionManifest.First(e => e.Id == "1.2.5.1006.win7");

        Assert.Equal("legacy-os", entry.Architecture);
    }
}
