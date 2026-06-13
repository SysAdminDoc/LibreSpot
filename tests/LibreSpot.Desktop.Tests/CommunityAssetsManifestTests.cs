using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class CommunityAssetsManifestTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();
    private static readonly JsonDocument Manifest = LoadManifest();

    [Fact]
    public void Manifest_ListsEveryCommunityExtensionInScript()
    {
        var script = ReadFile("LibreSpot.ps1");

        var scriptExtensions = Regex.Matches(
                script,
                @"\$global:CommunityExtensions\s*=\s*\[ordered\]@\{(?<body>.+?)\n\}",
                RegexOptions.Singleline)
            .SelectMany(m => Regex.Matches(m.Groups["body"].Value, @"'([^']+\.(?:js|mjs))'\s*="))
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var manifestExtensions = Manifest.RootElement
            .GetProperty("extensions")
            .EnumerateArray()
            .Select(e => e.GetProperty("filename").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var missingFromManifest = scriptExtensions.Except(manifestExtensions).ToList();
        Assert.True(
            missingFromManifest.Count == 0,
            $"CommunityExtensions in script but not in manifest: {string.Join(", ", missingFromManifest)}");
    }

    [Fact]
    public void Manifest_ListsEveryCommunityThemeInScript()
    {
        var script = ReadFile("LibreSpot.ps1");

        var scriptThemes = Regex.Matches(
                script,
                @"\$global:CommunityThemeRepos\s*=\s*@\{(?<body>.+?)\n\}",
                RegexOptions.Singleline)
            .SelectMany(m => Regex.Matches(m.Groups["body"].Value, @"'(\w+)'\s*=\s*@\{"))
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var manifestThemes = Manifest.RootElement
            .GetProperty("themes")
            .EnumerateArray()
            .Select(e => e.GetProperty("themeId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var missingFromManifest = scriptThemes.Except(manifestThemes).ToList();
        Assert.True(
            missingFromManifest.Count == 0,
            $"CommunityThemeRepos in script but not in manifest: {string.Join(", ", missingFromManifest)}");
    }

    [Fact]
    public void Manifest_ExtensionSha256MatchesScript()
    {
        var script = ReadFile("LibreSpot.ps1");

        foreach (var ext in Manifest.RootElement.GetProperty("extensions").EnumerateArray())
        {
            var filename = ext.GetProperty("filename").GetString()!;
            var manifestHash = ext.GetProperty("sha256").GetString()!;

            var hashMatch = Regex.Match(
                script,
                $@"""{Regex.Escape(filename)}""\s*=\s*@\{{[^}}]*SHA256\s*=\s*""([A-Fa-f0-9]{{64}})""",
                RegexOptions.Singleline);

            Assert.True(
                hashMatch.Success,
                $"Could not find SHA256 for extension '{filename}' in script.");

            Assert.Equal(manifestHash, hashMatch.Groups[1].Value);
        }
    }

    [Fact]
    public void Manifest_ExtensionCommitShaMatchesScript()
    {
        var script = ReadFile("LibreSpot.ps1");

        foreach (var ext in Manifest.RootElement.GetProperty("extensions").EnumerateArray())
        {
            var filename = ext.GetProperty("filename").GetString()!;
            var manifestCommit = ext.GetProperty("commitSha").GetString()!;

            var urlMatch = Regex.Match(
                script,
                $@"""{Regex.Escape(filename)}""\s*=\s*@\{{[^}}]*Url\s*=\s*""[^""]*?/([a-f0-9]{{40}})/",
                RegexOptions.Singleline);

            Assert.True(
                urlMatch.Success,
                $"Could not find commit SHA in URL for extension '{filename}' in script.");

            Assert.Equal(manifestCommit, urlMatch.Groups[1].Value);
        }
    }

    [Fact]
    public void Manifest_ThemeRepoMatchesScript()
    {
        var script = ReadFile("LibreSpot.ps1");

        foreach (var theme in Manifest.RootElement.GetProperty("themes").EnumerateArray())
        {
            var themeId = theme.GetProperty("themeId").GetString()!;
            var manifestOwner = theme.GetProperty("owner").GetString()!;
            var manifestRepo = theme.GetProperty("repo").GetString()!;

            var repoMatch = Regex.Match(
                script,
                $@"""{Regex.Escape(themeId)}""\s*=\s*@\{{\s*Owner\s*=\s*""([^""]+)""\s*;\s*Repo\s*=\s*""([^""]+)""",
                RegexOptions.Singleline);

            Assert.True(
                repoMatch.Success,
                $"Could not find repo metadata for theme '{themeId}' in script.");

            Assert.Equal(manifestOwner, repoMatch.Groups[1].Value);
            Assert.Equal(manifestRepo, repoMatch.Groups[2].Value);
        }
    }

    [Fact]
    public void Manifest_AllExtensionsHaveRequiredFields()
    {
        foreach (var ext in Manifest.RootElement.GetProperty("extensions").EnumerateArray())
        {
            var filename = ext.GetProperty("filename").GetString()!;
            var required = new[] { "filename", "displayName", "owner", "repo", "commitSha", "sourceUrl", "sha256", "spdxLicense", "supportState", "fallbackBehavior" };
            foreach (var field in required)
            {
                Assert.True(
                    ext.TryGetProperty(field, out var val) && val.ValueKind != JsonValueKind.Undefined,
                    $"Extension '{filename}' is missing required field '{field}'.");
            }
        }
    }

    [Fact]
    public void Manifest_AllThemesHaveRequiredFields()
    {
        foreach (var theme in Manifest.RootElement.GetProperty("themes").EnumerateArray())
        {
            var themeId = theme.GetProperty("themeId").GetString()!;
            var required = new[] { "themeId", "displayName", "owner", "repo", "spdxLicense", "supportState", "fallbackBehavior", "schemes", "requiresJsInjection" };
            foreach (var field in required)
            {
                Assert.True(
                    theme.TryGetProperty(field, out var val) && val.ValueKind != JsonValueKind.Undefined,
                    $"Theme '{themeId}' is missing required field '{field}'.");
            }
        }
    }

    [Fact]
    public void Manifest_OfficialThemesArchiveMatchesScript()
    {
        var script = ReadFile("LibreSpot.ps1");
        var archive = Manifest.RootElement.GetProperty("officialThemesArchive");

        var commitSha = archive.GetProperty("commitSha").GetString()!;
        var archiveHash = archive.GetProperty("archiveSha256").GetString()!;

        Assert.Contains(commitSha, script);
        Assert.Contains(archiveHash, script);
    }

    private static JsonDocument LoadManifest()
    {
        var path = Path.Combine(RepoRoot, "schemas", "community-assets.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string ReadFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray()));

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }
}
