using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class DocumentationContractTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void PublicDocs_DistinguishBundledAndFetchedComponentsAndLicenses()
    {
        var readme = ReadFile("README.md");
        var security = ReadFile("SECURITY.md");
        var appReadme = ReadFile("src", "LibreSpot.App", "README.md");
        var notices = ReadFile("src", "LibreSpot.App", "THIRD_PARTY_NOTICES.md");

        foreach (var document in new[] { readme, security, appReadme })
        {
            Assert.Contains("MIT", document, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AGPL-3.0-only", document, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("SpotX", document, StringComparison.Ordinal);
            Assert.Contains("Spicetify", document, StringComparison.Ordinal);
        }

        Assert.Contains("LibreSpot's own Prism theme and engine archive", readme, StringComparison.Ordinal);
        Assert.Contains("fetched from pinned upstream sources", readme, StringComparison.Ordinal);
        Assert.Contains("official GitHub repositories", security, StringComparison.Ordinal);
        Assert.Contains("Selected source parts are included under `vendor`", notices, StringComparison.Ordinal);
        Assert.Contains("No complete upstream repository is bundled", notices, StringComparison.Ordinal);

        Assert.DoesNotContain("doesn't host or redistribute any code", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bundle, host, or redistribute Spotify binaries or any upstream project code", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("LibreSpot is MIT-licensed, uses no Spotify API", security, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicDocs_NameUserFacingExecutablesAndCrashHelper()
    {
        var readme = ReadFile("README.md");
        var contract = ReadFile("schemas", "release-artifact-contract.json");

        Assert.Contains("The three user-facing executable artifacts are", readme, StringComparison.Ordinal);
        foreach (var name in new[] { "LibreSpot.exe", "LibreSpot-Desktop.exe", "LibreSpot.Cli.exe" })
        {
            Assert.Contains($"`{name}`", readme, StringComparison.Ordinal);
            Assert.Contains($"\"name\": \"{name}\"", contract, StringComparison.Ordinal);
        }

        Assert.Contains("adjacent `createdump.exe`", readme, StringComparison.Ordinal);
        Assert.Contains("not a fourth entry point", readme, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"createdump.exe\"", contract, StringComparison.Ordinal);
        Assert.Contains("`createdump.exe` helper", ReadFile("CHANGELOG.md"), StringComparison.Ordinal);

        Assert.Contains("Version 4.5.0 is prepared in this repository and has not been published", readme, StringComparison.Ordinal);
        Assert.Contains("public latest stable release, v4.4.0", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void ContributorDocs_RunConfiguredPesterSuiteAndDirectMtpExecutables()
    {
        var readme = ReadFile("README.md");
        var contributing = ReadFile(".github", "CONTRIBUTING.md");
        var pullRequest = ReadFile(".github", "PULL_REQUEST_TEMPLATE.md");

        foreach (var document in new[] { readme, contributing, pullRequest })
        {
            Assert.Contains("pester.config.ps1", document, StringComparison.Ordinal);
            Assert.Contains("5.9.1", document, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Invoke-Pester -Path .\\tests\\powershell\\LibreSpot.Tests.ps1 -CI", pullRequest, StringComparison.Ordinal);
        Assert.Contains("LibreSpot.Desktop.Tests.exe", contributing, StringComparison.Ordinal);
        Assert.Contains("LibreSpot.Core.Tests.exe", contributing, StringComparison.Ordinal);
        Assert.DoesNotContain("LibreSpot.Desktop.Tests.dll --filter-not-class", contributing, StringComparison.Ordinal);
        Assert.DoesNotContain("LibreSpot.Desktop.Tests.dll --filter-not-class", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void AppDocs_DescribeStoreRoutesAndActualPrismApis()
    {
        var appReadme = ReadFile("src", "LibreSpot.App", "README.md");
        var navigation = ReadFile("src", "LibreSpot.App", "src", "surface", "navigation.ts");
        var prism = ReadFile("resources", "themes", "Prism", "theme.js");
        var changelog = ReadFile("CHANGELOG.md");
        var unreleased = changelog.Split("## [Unreleased]", StringSplitOptions.None)[1]
            .Split("## [", StringSplitOptions.None)[0];

        Assert.Contains("Store, Look, Tweaks, Features, Presets, and Health", appReadme, StringComparison.Ordinal);
        Assert.Contains("/librespot/extensions", appReadme, StringComparison.Ordinal);
        Assert.Contains("/librespot/marketplace", appReadme, StringComparison.Ordinal);
        Assert.Contains("segment === \"extensions\" || segment === \"marketplace\"", navigation, StringComparison.Ordinal);
        Assert.Contains("new Spicetify.Menu.Item", prism, StringComparison.Ordinal);
        Assert.Contains("Spicetify.PopupModal.display", prism, StringComparison.Ordinal);
        Assert.DoesNotContain("ReactDOM", prism, StringComparison.Ordinal);
        Assert.DoesNotContain("Spicetify.React", prism, StringComparison.Ordinal);
        Assert.Contains("Spicetify.Menu.Item", unreleased, StringComparison.Ordinal);
        Assert.Contains("Spicetify.PopupModal.display", unreleased, StringComparison.Ordinal);
        Assert.DoesNotContain("Prism waits for Spotify's React, menu and modal APIs", unreleased, StringComparison.Ordinal);
    }

    [Fact]
    public void BackupDocs_DistinguishRawProfilesFromCompleteBackups()
    {
        var readme = ReadFile("README.md");
        var backup = ReadFile("src", "LibreSpot.App", "src", "core", "backup.ts");
        var health = ReadFile("src", "LibreSpot.App", "src", "panels", "health.ts");

        Assert.Contains("A raw `.librespot` profile contains LibreSpot-managed engine settings only", readme, StringComparison.Ordinal);
        Assert.Contains("Health's complete backup envelope also includes", readme, StringComparison.Ordinal);
        Assert.Contains("owned `marketplace:` keys", readme, StringComparison.Ordinal);
        Assert.Contains("parseRestoreSource", backup, StringComparison.Ordinal);
        Assert.Contains("A raw profile must reach its bounded parser", backup, StringComparison.Ordinal);
        Assert.Contains("One file holds this profile and Marketplace's owned settings", health, StringComparison.Ordinal);
        var blocked = ReadFile("Roadmap_Blocked.md");
        Assert.Contains("advanced backup action", blocked, StringComparison.Ordinal);
        Assert.Contains("captures owned Marketplace keys locally", blocked, StringComparison.Ordinal);
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
