using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class CommunityAssetsManifestTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();
    private static readonly JsonDocument Manifest = LoadManifest();

    [Fact]
    public void BundledLibreSpotArchive_MatchesEveryPinAndShipsItsPackageVersion()
    {
        var archivePath = Path.Combine(RepoRoot, "resources", "custom-apps", "librespot-engine.zip");
        Assert.True(File.Exists(archivePath), $"Bundled LibreSpot archive was not found at {archivePath}.");

        var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archivePath))).ToLowerInvariant();
        var pinnedSources = new[]
        {
            new[] { "src", "powershell", "data", "CommunityCustomApps.ps1" },
            new[] { "src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1" },
            new[] { "LibreSpot.ps1" }
        };

        foreach (var source in pinnedSources)
        {
            var appBlocks = ReadCommunityCustomAppBlocks(ReadFile(source));
            Assert.True(appBlocks.TryGetValue("librespot", out var appBlock), $"LibreSpot custom-app block is missing from {string.Join('/', source)}.");
            var hash = Regex.Match(appBlock, @"SHA256\s*=\s*'(?<hash>[a-f0-9]{64})'").Groups["hash"].Value;
            Assert.Equal(actualHash, hash);
        }

        using var package = JsonDocument.Parse(ReadFile("src", "LibreSpot.App", "package.json"));
        var expectedVersion = package.RootElement.GetProperty("version").GetString();
        using var archive = ZipFile.OpenRead(archivePath);
        var manifestEntry = archive.GetEntry("librespot/manifest.json");
        Assert.NotNull(manifestEntry);
        string archiveManifestText;
        using (var manifestReader = new StreamReader(manifestEntry.Open()))
        {
            archiveManifestText = manifestReader.ReadToEnd();
        }

        using var bundledManifest = JsonDocument.Parse(archiveManifestText);
        Assert.Equal(expectedVersion, bundledManifest.RootElement.GetProperty("version").GetString());

        // dist/ is a build output and is gitignored, so a checkout that has never
        // run `pnpm run bundle` legitimately has none and the pins above are still
        // fully checked. Reading it unconditionally threw an IO exception there
        // instead of failing with a reason. When it is present it has to agree with
        // the archive that was committed from it, which comparing it against
        // package.json never established.
        var distManifestPath = Path.Combine(RepoRoot, "src", "LibreSpot.App", "dist", "manifest.json");
        if (File.Exists(distManifestPath))
        {
            Assert.Equal(archiveManifestText, File.ReadAllText(distManifestPath));
        }
    }

    [Fact]
    public void CustomAppDownloads_ComeFromImmutableTaggedAssetsNotBranches()
    {
        // A branch URL serves whatever the branch holds today, so rebuilding an
        // archive breaks the pinned hash for every release already published.
        var pinnedSources = new[]
        {
            new[] { "src", "powershell", "data", "CommunityCustomApps.ps1" },
            new[] { "src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1" },
            new[] { "LibreSpot.ps1" }
        };

        using var package = JsonDocument.Parse(ReadFile("src", "LibreSpot.App", "package.json"));
        var engineVersion = package.RootElement.GetProperty("version").GetString();

        foreach (var source in pinnedSources)
        {
            var label = string.Join('/', source);
            var appBlocks = ReadCommunityCustomAppBlocks(ReadFile(source));
            Assert.NotEmpty(appBlocks);

            foreach (var (appId, appBlock) in appBlocks)
            {
                var url = Regex.Match(appBlock, @"Url\s*=\s*'(?<url>[^']+)'").Groups["url"].Value;
                Assert.False(string.IsNullOrWhiteSpace(url), $"Custom app '{appId}' has no Url in {label}.");
                Assert.StartsWith("https://", url, StringComparison.Ordinal);
                Assert.DoesNotContain("/main/", url, StringComparison.Ordinal);
                Assert.DoesNotContain("/master/", url, StringComparison.Ordinal);
                Assert.DoesNotContain("/refs/heads/", url, StringComparison.Ordinal);
                Assert.Contains("/releases/download/", url, StringComparison.Ordinal);

                var releaseTag = Regex.Match(appBlock, @"ReleaseTag\s*=\s*'(?<tag>[^']+)'").Groups["tag"].Value;
                Assert.False(string.IsNullOrWhiteSpace(releaseTag), $"Custom app '{appId}' has no ReleaseTag in {label}.");
                Assert.NotEqual("main", releaseTag);
                Assert.NotEqual("master", releaseTag);
                Assert.Contains($"/releases/download/{releaseTag}/", url, StringComparison.Ordinal);

                if (appId != "librespot")
                {
                    continue;
                }

                // The bundled engine must advertise the release that carries this
                // exact archive, and name the file both lanes look for locally.
                Assert.Equal($"v{engineVersion}", releaseTag);
                Assert.Equal(
                    "librespot-engine.zip",
                    Regex.Match(appBlock, @"BundledFileName\s*=\s*'(?<name>[^']+)'").Groups["name"].Value);
                Assert.Matches(@"Bundled\s*=\s*\$true", appBlock);
            }
        }
    }

    [Fact]
    public void BuildScripts_PublishesTheReleaseWithReproducibleProperties()
    {
        var script = ReadFile("Build-Scripts.ps1");

        // A release nobody can rebuild cannot be checked by anyone but the person
        // who built it, so the property set and the command that applies it are
        // both part of the contract.
        Assert.Contains("[switch]$PublishRelease,", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-LibreSpotReleasePublish", script, StringComparison.Ordinal);
        foreach (var property in new[] { "Deterministic", "ContinuousIntegrationBuild", "EmbedUntrackedSources", "PublishRepositoryUrl", "PublishSingleFile" })
        {
            Assert.Contains(property, script, StringComparison.Ordinal);
        }

        Assert.Contains("buildInputs", script, StringComparison.Ordinal);
        Assert.Contains("sdkVersion", script, StringComparison.Ordinal);
        Assert.Contains("nonDeterministicNotes", script, StringComparison.Ordinal);

        var props = ReadFile("Directory.Build.props");
        Assert.Contains("<Deterministic>true</Deterministic>", props, StringComparison.Ordinal);
        Assert.Contains("LibreSpotReleaseBuild", props, StringComparison.Ordinal);
        Assert.Contains("<EmbedUntrackedSources>true</EmbedUntrackedSources>", props, StringComparison.Ordinal);
        Assert.Contains("<PublishRepositoryUrl>true</PublishRepositoryUrl>", props, StringComparison.Ordinal);

        // The documented procedure must lead with the command that builds the root.
        var readme = ReadFile("README.md");
        Assert.Contains(".\\Build-Scripts.ps1 -PublishRelease", readme, StringComparison.Ordinal);
        Assert.Matches(@"-PublishRelease[\s\S]{0,400}-CompileStableExe[\s\S]{0,200}-GenerateSbom[\s\S]{0,200}-GenerateChecksums[\s\S]{0,200}-GenerateReleaseManifest", readme);
    }

    [Fact]
    public void PublishFootprint_RecordsTheCompressionDecisionAndItsMeasurements()
    {
        using var budget = JsonDocument.Parse(ReadFile("schemas", "publish-footprint-budget.json"));
        var root = budget.RootElement;

        // A size ceiling nobody measured is a guess. This holds the decision to the
        // numbers it was made from, so turning compression off again has to restate them.
        var compression = root.GetProperty("buildModeDecisions").EnumerateArray()
            .Single(decision => decision.GetProperty("option").GetString() == "enableCompressionInSingleFile");
        Assert.True(compression.GetProperty("enabled").GetBoolean());
        Assert.Contains("EnableCompressionInSingleFile", ReadFile("Build-Scripts.ps1"), StringComparison.Ordinal);

        Assert.True(root.GetProperty("budget").GetProperty("maxSizeMiB").GetInt32() <= 120);

        var metrics = root.GetProperty("coldStartMetrics");
        Assert.Equal("measured", metrics.GetProperty("status").GetString());
        foreach (var metric in metrics.GetProperty("measuredMetrics").EnumerateArray())
        {
            var name = metric.GetProperty("metric").GetString();
            foreach (var variant in new[] { "uncompressed", "compressed" })
            {
                var samples = metric.GetProperty(variant).EnumerateArray().Select(value => value.GetInt32()).ToArray();
                Assert.True(samples.Length >= 2, $"{name}/{variant} needs more than one sample.");
                Assert.All(samples, sample => Assert.InRange(sample, 1, 60_000));
            }
        }
    }

    [Fact]
    public void ReleaseContract_ShipsTheEngineArchiveWithAChecksumEntry()
    {
        using var contract = JsonDocument.Parse(ReadFile("schemas", "release-artifact-contract.json"));
        var artifact = contract.RootElement.GetProperty("artifacts").EnumerateArray()
            .Single(a => a.GetProperty("name").GetString() == "librespot-engine.zip");

        Assert.True(artifact.GetProperty("required").GetBoolean());
        Assert.True(artifact.GetProperty("checksumEntry").GetBoolean());

        var covered = contract.RootElement.GetProperty("checksumContract").GetProperty("coveredAssets")
            .EnumerateArray().Select(a => a.GetString()).ToArray();
        Assert.Contains("librespot-engine.zip", covered);
    }

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
            var required = new[] { "filename", "displayName", "owner", "repo", "branch", "commitSha", "sourceUrl", "sha256", "spdxLicense", "supportState", "lastVerifiedDate", "releaseNotesUrl", "fallbackBehavior", "networkBehavior" };
            foreach (var field in required)
            {
                Assert.True(
                    ext.TryGetProperty(field, out var val) && val.ValueKind != JsonValueKind.Undefined,
                    $"Extension '{filename}' is missing required field '{field}'.");
            }
        }
    }

    [Fact]
    public void Manifest_ThemeFolderMatchesBothScripts()
    {
        // The subfolder holding color.ini differs per repository. When it is wrong
        // the archive still downloads and its hash still verifies, then the install
        // is skipped with a warning while the run reports success, so the three
        // copies of this value have to agree.
        var sources = new[]
        {
            new[] { "src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1" },
            new[] { "LibreSpot.ps1" }
        };

        foreach (var theme in Manifest.RootElement.GetProperty("themes").EnumerateArray())
        {
            var themeId = theme.GetProperty("themeId").GetString()!;
            Assert.True(
                theme.TryGetProperty("themeFolder", out var folderElement),
                $"Theme '{themeId}' is missing required field 'themeFolder'.");
            var folder = folderElement.GetString()!;

            foreach (var source in sources)
            {
                var script = ReadFile(source);
                var match = Regex.Match(
                    script,
                    $@"['""]{Regex.Escape(themeId)}['""]\s*=\s*@\{{[^}}]*ThemeFolder\s*=\s*['""](?<folder>[^'""]+)['""]");
                Assert.True(match.Success, $"Could not find ThemeFolder for theme '{themeId}' in {string.Join('/', source)}.");
                Assert.Equal(folder, match.Groups["folder"].Value);
            }
        }
    }

    [Fact]
    public void Manifest_AllThemesHaveRequiredFields()
    {
        foreach (var theme in Manifest.RootElement.GetProperty("themes").EnumerateArray())
        {
            var themeId = theme.GetProperty("themeId").GetString()!;
            var required = new[] { "themeId", "displayName", "owner", "repo", "branch", "commitSha", "archiveSha256", "spdxLicense", "supportState", "lastVerifiedDate", "releaseNotesUrl", "fallbackBehavior", "schemes", "requiresJsInjection", "networkBehavior" };
            foreach (var field in required)
            {
                Assert.True(
                    theme.TryGetProperty(field, out var val) && val.ValueKind != JsonValueKind.Undefined,
                    $"Theme '{themeId}' is missing required field '{field}'.");
            }
        }
    }

    [Fact]
    public void Manifest_ThemeCommitShasAreNotNull()
    {
        foreach (var theme in Manifest.RootElement.GetProperty("themes").EnumerateArray())
        {
            var themeId = theme.GetProperty("themeId").GetString()!;

            var commitSha = theme.GetProperty("commitSha");
            Assert.True(
                commitSha.ValueKind == JsonValueKind.String && Regex.IsMatch(commitSha.GetString()!, @"^[a-f0-9]{40}$"),
                $"Theme '{themeId}' must have a 40-char hex commitSha, got: {commitSha}");

            var archiveHash = theme.GetProperty("archiveSha256");
            Assert.True(
                archiveHash.ValueKind == JsonValueKind.String && Regex.IsMatch(archiveHash.GetString()!, @"^[a-f0-9]{64}$"),
                $"Theme '{themeId}' must have a 64-char hex archiveSha256, got: {archiveHash}");
        }
    }

    [Fact]
    public void Manifest_ThemeCommitShaMatchesScript()
    {
        var script = ReadFile("LibreSpot.ps1");

        foreach (var theme in Manifest.RootElement.GetProperty("themes").EnumerateArray())
        {
            var themeId = theme.GetProperty("themeId").GetString()!;
            var manifestCommit = theme.GetProperty("commitSha").GetString()!;

            var commitMatch = Regex.Match(
                script,
                $@"""{Regex.Escape(themeId)}""\s*=\s*@\{{[^}}]*CommitSha\s*=\s*""([a-f0-9]{{40}})""",
                RegexOptions.Singleline);

            Assert.True(
                commitMatch.Success,
                $"Could not find CommitSha for theme '{themeId}' in script.");

            Assert.Equal(manifestCommit, commitMatch.Groups[1].Value);
        }
    }

    [Fact]
    public void Manifest_ThemeArchiveSha256MatchesScript()
    {
        var script = ReadFile("LibreSpot.ps1");

        foreach (var theme in Manifest.RootElement.GetProperty("themes").EnumerateArray())
        {
            var themeId = theme.GetProperty("themeId").GetString()!;
            var manifestHash = theme.GetProperty("archiveSha256").GetString()!;

            var hashMatch = Regex.Match(
                script,
                $@"""{Regex.Escape(themeId)}""\s*=\s*@\{{[^}}]*SHA256\s*=\s*""([a-f0-9]{{64}})""",
                RegexOptions.Singleline);

            Assert.True(
                hashMatch.Success,
                $"Could not find SHA256 for theme '{themeId}' in script.");

            Assert.Equal(manifestHash, hashMatch.Groups[1].Value);
        }
    }

    [Fact]
    public void Manifest_AllCustomAppsHaveRequiredFields()
    {
        var customApps = Manifest.RootElement.GetProperty("customApps");
        Assert.True(customApps.GetArrayLength() > 0, "customApps array must not be empty.");

        foreach (var app in customApps.EnumerateArray())
        {
            var appId = app.GetProperty("appId").GetString()!;
            var required = new[] { "appId", "displayName", "description", "owner", "repo", "branch", "commitSha", "assetPath", "sha256", "spdxLicense", "supportState", "lastVerifiedDate", "releaseNotesUrl", "fallbackBehavior", "networkBehavior", "easyModeDefault" };
            foreach (var field in required)
            {
                Assert.True(
                    app.TryGetProperty(field, out var val) && val.ValueKind != JsonValueKind.Undefined,
                    $"Custom app '{appId}' is missing required field '{field}'.");
            }
        }
    }

    [Fact]
    public void Manifest_CustomAppIdsAreUnique()
    {
        var ids = Manifest.RootElement.GetProperty("customApps").EnumerateArray()
            .Select(a => a.GetProperty("appId").GetString()!)
            .ToList();

        var duplicates = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(duplicates.Count == 0, $"Duplicate custom app IDs: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void Manifest_ListsEveryCommunityCustomAppInScript()
    {
        var script = ReadFile("LibreSpot.ps1");
        var scriptApps = ReadCommunityCustomAppBlocks(script)
            .Where(entry => !Regex.IsMatch(entry.Value, @"(?m)^\s*Bundled\s*=\s*\$true\s*$"))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);

        var manifestApps = Manifest.RootElement
            .GetProperty("customApps")
            .EnumerateArray()
            .Select(e => e.GetProperty("appId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var missingFromManifest = scriptApps.Except(manifestApps).ToList();
        Assert.True(
            missingFromManifest.Count == 0,
            $"CommunityCustomApps in script but not in manifest: {string.Join(", ", missingFromManifest)}");
    }

    [Fact]
    public void Manifest_CustomAppSha256MatchesScript()
    {
        var script = ReadFile("LibreSpot.ps1");
        var scriptApps = ReadCommunityCustomAppBlocks(script);

        foreach (var app in Manifest.RootElement.GetProperty("customApps").EnumerateArray())
        {
            var appId = app.GetProperty("appId").GetString()!;
            var manifestHash = app.GetProperty("sha256").GetString()!;
            Assert.True(
                scriptApps.TryGetValue(appId, out var appBlock),
                $"Could not find custom app '{appId}' in script.");

            var hashMatch = Regex.Match(
                appBlock!,
                @"(?m)^\s*SHA256\s*=\s*['""]([A-Fa-f0-9]{64})['""]\s*$");
            Assert.True(hashMatch.Success, $"Could not find SHA256 for custom app '{appId}' in script.");

            Assert.Equal(manifestHash, hashMatch.Groups[1].Value);
        }
    }

    [Fact]
    public void Manifest_CustomAppsHaveValidLicenses()
    {
        var policy = Manifest.RootElement.GetProperty("policy");
        var knownLicenses = new HashSet<string>(StringComparer.Ordinal) { "NOASSERTION" };
        foreach (var l in policy.GetProperty("allowedLicenses").EnumerateArray())
            knownLicenses.Add(l.GetString()!);
        foreach (var l in policy.GetProperty("reviewRequiredLicenses").EnumerateArray())
            knownLicenses.Add(l.GetString()!);
        foreach (var l in policy.GetProperty("blockedLicenses").EnumerateArray())
            knownLicenses.Add(l.GetString()!);

        foreach (var app in Manifest.RootElement.GetProperty("customApps").EnumerateArray())
        {
            var appId = app.GetProperty("appId").GetString()!;
            var license = app.GetProperty("spdxLicense").GetString()!;
            Assert.True(
                knownLicenses.Contains(license),
                $"Custom app '{appId}' has spdxLicense '{license}' which is not known to the policy.");
        }
    }

    [Fact]
    public void Manifest_CustomAppsWithReviewRequiredLicenseAndEasyModeRequireOverride()
    {
        var policy = Manifest.RootElement.GetProperty("policy");
        var reviewRequired = policy.GetProperty("reviewRequiredLicenses")
            .EnumerateArray().Select(l => l.GetString()!).ToHashSet(StringComparer.Ordinal);

        foreach (var app in Manifest.RootElement.GetProperty("customApps").EnumerateArray())
        {
            var appId = app.GetProperty("appId").GetString()!;
            var license = app.GetProperty("spdxLicense").GetString()!;
            var isEasyDefault = app.TryGetProperty("easyModeDefault", out var em) && em.ValueKind == JsonValueKind.True;

            if (!isEasyDefault)
                continue;

            bool needsOverride = license == "NOASSERTION" || reviewRequired.Contains(license);
            if (!needsOverride)
                continue;

            Assert.True(
                app.TryGetProperty("policyOverride", out var po) && po.ValueKind == JsonValueKind.Object,
                $"Custom app '{appId}' has license '{license}' and easyModeDefault=true but no policyOverride.");

            Assert.True(
                po.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(reason.GetString()),
                $"Custom app '{appId}' policyOverride is missing a non-empty 'reason'.");

            Assert.True(
                po.TryGetProperty("approvedBy", out var by) && by.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(by.GetString()),
                $"Custom app '{appId}' policyOverride is missing a non-empty 'approvedBy'.");

            Assert.True(
                po.TryGetProperty("approvedDate", out var date) && date.ValueKind == JsonValueKind.String
                    && Regex.IsMatch(date.GetString()!, @"^\d{4}-\d{2}-\d{2}$"),
                $"Custom app '{appId}' policyOverride is missing a valid 'approvedDate' (YYYY-MM-DD).");
        }
    }

    [Fact]
    public void Manifest_CustomAppsAppIdMatchesAssetPath()
    {
        foreach (var app in Manifest.RootElement.GetProperty("customApps").EnumerateArray())
        {
            var appId = app.GetProperty("appId").GetString()!;
            var assetPath = app.GetProperty("assetPath").GetString()!;

            // The assetPath should start with or equal the appId (the folder name spicetify uses)
            Assert.True(
                assetPath == appId || assetPath.StartsWith(appId + "/", StringComparison.Ordinal),
                $"Custom app '{appId}' has assetPath '{assetPath}' that does not match its appId.");
        }
    }

    // Every installable community asset must explicitly declare its runtime
    // network behavior so users (and the trust docs) know whether enabling it
    // contacts a server other than GitHub/Spotify. Assets that talk to a
    // third-party service must explain what they contact.
    [Fact]
    public void Manifest_AllAssetsDeclareNetworkBehavior()
    {
        // remote-loader: the pinned archive only fetches the real code from a CDN at
        // runtime, so the commit and hash cover the loader and nothing else.
        var allowedWebApiUse = new[] { "none", "platform-api", "client-id" };
        var allowed = new[] { "local-only", "third-party-service", "remote-loader" };

        IEnumerable<JsonElement> assets =
            Manifest.RootElement.GetProperty("extensions").EnumerateArray()
                .Concat(Manifest.RootElement.GetProperty("themes").EnumerateArray())
                .Concat(Manifest.RootElement.GetProperty("customApps").EnumerateArray());

        foreach (var asset in assets)
        {
            var id = asset.TryGetProperty("filename", out var fn)
                ? fn.GetString()!
                : asset.TryGetProperty("themeId", out var tid)
                    ? tid.GetString()!
                    : asset.GetProperty("appId").GetString()!;

            Assert.True(
                asset.TryGetProperty("networkBehavior", out var behavior) && behavior.ValueKind == JsonValueKind.String,
                $"Asset '{id}' is missing a string 'networkBehavior'.");

            var value = behavior.GetString()!;
            Assert.True(
                allowed.Contains(value),
                $"Asset '{id}' has unknown networkBehavior '{value}'. Allowed: {string.Join(", ", allowed)}.");

            // Spotify's 2026 Development Mode rules cap a registered client ID at
            // five authorised users, so an asset that calls the Web API with its
            // own client ID silently stops working for the sixth person who
            // installs it. Every asset records which of the three it does, and the
            // evidence that settles it.
            Assert.True(
                asset.TryGetProperty("webApiUse", out var webApiUse) && webApiUse.ValueKind == JsonValueKind.String,
                $"Asset '{id}' is missing a string 'webApiUse'.");

            var webApiValue = webApiUse.GetString()!;
            Assert.True(
                allowedWebApiUse.Contains(webApiValue),
                $"Asset '{id}' has unknown webApiUse '{webApiValue}'. Allowed: {string.Join(", ", allowedWebApiUse)}.");

            Assert.True(
                asset.TryGetProperty("webApiDetail", out var webApiDetail)
                    && !string.IsNullOrWhiteSpace(webApiDetail.GetString()),
                $"Asset '{id}' records webApiUse '{webApiValue}' with no evidence in 'webApiDetail'.");

            if (webApiValue == "client-id")
            {
                Assert.True(
                    asset.GetProperty("supportState").GetString() == "degraded",
                    $"Asset '{id}' calls the Web API with its own client ID, so it works for five users and then stops. Mark it degraded or explain it in the catalog.");
            }

            if (value == "third-party-service")
            {
                Assert.True(
                    asset.TryGetProperty("networkDetail", out var detail)
                        && detail.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(detail.GetString()),
                    $"Asset '{id}' is networked but has no 'networkDetail' explaining what it contacts.");
            }
        }
    }

    [Fact]
    public void Manifest_AllAssetsHaveDriftMetadata()
    {
        IEnumerable<(JsonElement Asset, string Id, string HashProperty)> assets =
            Manifest.RootElement.GetProperty("extensions").EnumerateArray()
                .Select(asset => (asset, asset.GetProperty("filename").GetString()!, "sha256"))
                .Concat(Manifest.RootElement.GetProperty("themes").EnumerateArray()
                    .Select(asset => (asset, asset.GetProperty("themeId").GetString()!, "archiveSha256")))
                .Concat(Manifest.RootElement.GetProperty("customApps").EnumerateArray()
                    .Select(asset => (asset, asset.GetProperty("appId").GetString()!, "sha256")));

        foreach (var (asset, id, hashProperty) in assets)
        {
            Assert.True(
                asset.TryGetProperty("branch", out var branch)
                    && branch.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(branch.GetString()),
                $"Asset '{id}' must declare the upstream branch used for drift checks.");

            Assert.True(
                asset.TryGetProperty("commitSha", out var commit)
                    && commit.ValueKind == JsonValueKind.String
                    && Regex.IsMatch(commit.GetString()!, @"^[a-f0-9]{40}$"),
                $"Asset '{id}' must declare a 40-char commitSha for drift checks.");

            Assert.True(
                asset.TryGetProperty(hashProperty, out var hash)
                    && hash.ValueKind == JsonValueKind.String
                    && Regex.IsMatch(hash.GetString()!, @"^[a-f0-9]{64}$"),
                $"Asset '{id}' must declare a 64-char {hashProperty} for install/cache verification.");
        }
    }

    [Fact]
    public void Manifest_AllAssetsHaveCatalogReviewEvidenceAndEasyDefaultsAreEligible()
    {
        var asOfUtc = new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero);

        foreach (var asset in AllAssets())
        {
            var id = AssetId(asset);
            Assert.True(
                asset.TryGetProperty("catalogReview", out var review) &&
                review.ValueKind == JsonValueKind.Object,
                $"Asset '{id}' is missing catalogReview metadata.");

            foreach (var field in new[] { "evaluatedDate", "lastPush", "decision", "reason", "evidenceUrls" })
            {
                Assert.True(
                    review.TryGetProperty(field, out var value) &&
                    value.ValueKind != JsonValueKind.Undefined,
                    $"Asset '{id}' catalogReview is missing '{field}'.");
            }

            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", review.GetProperty("evaluatedDate").GetString()!);
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", review.GetProperty("lastPush").GetString()!);
            Assert.Contains(
                review.GetProperty("decision").GetString()!,
                new[] { "accept", "reject", "defer", "marketplace-only" });
            Assert.False(string.IsNullOrWhiteSpace(review.GetProperty("reason").GetString()));

            var evidence = review.GetProperty("evidenceUrls").EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray();
            Assert.NotEmpty(evidence);
            Assert.All(evidence, url =>
            {
                Assert.True(Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
                            parsed.Scheme == Uri.UriSchemeHttps,
                    $"Asset '{id}' has a non-HTTPS catalog evidence URL: {url}");
            });

            var easyModeDefault = asset.TryGetProperty("easyModeDefault", out var easy) &&
                                  easy.ValueKind == JsonValueKind.True;
            var policy = CommunityAssetCatalogPolicy.Evaluate(
                review.GetProperty("decision").GetString()!,
                review.GetProperty("reason").GetString()!,
                DateTimeOffset.Parse(review.GetProperty("evaluatedDate").GetString()!),
                DateTimeOffset.Parse(review.GetProperty("lastPush").GetString()!),
                DateTimeOffset.Parse(asset.GetProperty("lastVerifiedDate").GetString()!),
                review.GetProperty("archived").GetBoolean(),
                evidence,
                asset.GetProperty("supportState").GetString()!,
                asset.GetProperty("networkBehavior").GetString()!,
                asset.TryGetProperty("networkDetail", out var detail) ? detail.GetString() : null,
                easyModeDefault,
                asOfUtc);

            if (easyModeDefault)
            {
                Assert.True(
                    policy.IsEasyModeEligible,
                    $"Easy-mode asset '{id}' is not eligible: {policy.Reason}");
            }
        }
    }

    [Fact]
    public void CatalogPolicy_BlocksStaleArchivedMissingEvidenceAndUnknownNetworkFixtures()
    {
        var asOfUtc = new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero);
        var currentPush = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var currentReview = new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero);
        var evidence = new[] { "https://github.com/example/project" };

        CommunityAssetCatalogReview EvaluateFixture(
            DateTimeOffset? lastPush = null,
            bool archived = false,
            IReadOnlyList<string>? evidenceUrls = null,
            string networkBehavior = "local-only") =>
            CommunityAssetCatalogPolicy.Evaluate(
                "accept",
                "fixture review",
                currentReview,
                lastPush ?? currentPush,
                currentReview,
                archived,
                evidenceUrls ?? evidence,
                "active",
                networkBehavior,
                null,
                easyModeDefault: true,
                asOfUtc);

        Assert.True(EvaluateFixture().IsEasyModeEligible);
        Assert.False(EvaluateFixture(lastPush: new DateTimeOffset(2025, 8, 10, 0, 0, 0, TimeSpan.Zero)).IsEasyModeEligible);
        Assert.False(EvaluateFixture(archived: true).IsEasyModeEligible);
        Assert.False(EvaluateFixture(evidenceUrls: Array.Empty<string>()).IsEasyModeEligible);
        Assert.False(EvaluateFixture(networkBehavior: "unknown").IsEasyModeEligible);
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

    // --- License-policy enforcement tests ---

    /// <summary>
    /// Any asset whose spdxLicense is NOASSERTION or in the reviewRequiredLicenses
    /// list must NOT be easyModeDefault=true unless it carries a policyOverride with
    /// reason, approvedBy, and approvedDate.
    /// </summary>
    [Fact]
    public void Manifest_NoassertionOrReviewRequiredWithEasyModeDefaultRequiresOverride()
    {
        var policy = Manifest.RootElement.GetProperty("policy");
        var reviewRequired = policy.GetProperty("reviewRequiredLicenses")
            .EnumerateArray().Select(l => l.GetString()!).ToHashSet(StringComparer.Ordinal);

        IEnumerable<JsonElement> assets =
            Manifest.RootElement.GetProperty("extensions").EnumerateArray()
                .Concat(Manifest.RootElement.GetProperty("themes").EnumerateArray())
                .Concat(Manifest.RootElement.GetProperty("customApps").EnumerateArray());

        foreach (var asset in assets)
        {
            var id = asset.TryGetProperty("filename", out var fn)
                ? fn.GetString()!
                : asset.TryGetProperty("themeId", out var tid)
                    ? tid.GetString()!
                    : asset.GetProperty("appId").GetString()!;

            var license = asset.GetProperty("spdxLicense").GetString()!;
            var isEasyDefault = asset.TryGetProperty("easyModeDefault", out var em)
                && em.ValueKind == JsonValueKind.True;

            if (!isEasyDefault)
                continue;

            bool needsOverride = license == "NOASSERTION" || reviewRequired.Contains(license);
            if (!needsOverride)
                continue;

            Assert.True(
                asset.TryGetProperty("policyOverride", out var po) && po.ValueKind == JsonValueKind.Object,
                $"Asset '{id}' has license '{license}' and easyModeDefault=true but no policyOverride object.");

            Assert.True(
                po.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(reason.GetString()),
                $"Asset '{id}' policyOverride is missing a non-empty 'reason'.");

            Assert.True(
                po.TryGetProperty("approvedBy", out var by) && by.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(by.GetString()),
                $"Asset '{id}' policyOverride is missing a non-empty 'approvedBy'.");

            Assert.True(
                po.TryGetProperty("approvedDate", out var date) && date.ValueKind == JsonValueKind.String
                    && Regex.IsMatch(date.GetString()!, @"^\d{4}-\d{2}-\d{2}$"),
                $"Asset '{id}' policyOverride is missing a valid 'approvedDate' (YYYY-MM-DD).");
        }
    }

    /// <summary>
    /// Every asset's spdxLicense must appear in one of the policy license lists or be "NOASSERTION".
    /// Unknown license identifiers that aren't tracked by the policy would slip through silently.
    /// </summary>
    [Fact]
    public void Manifest_AllLicensesAreKnownToPolicy()
    {
        var policy = Manifest.RootElement.GetProperty("policy");
        var knownLicenses = new HashSet<string>(StringComparer.Ordinal) { "NOASSERTION" };

        foreach (var l in policy.GetProperty("allowedLicenses").EnumerateArray())
            knownLicenses.Add(l.GetString()!);
        foreach (var l in policy.GetProperty("reviewRequiredLicenses").EnumerateArray())
            knownLicenses.Add(l.GetString()!);
        foreach (var l in policy.GetProperty("blockedLicenses").EnumerateArray())
            knownLicenses.Add(l.GetString()!);

        IEnumerable<JsonElement> assets =
            Manifest.RootElement.GetProperty("extensions").EnumerateArray()
                .Concat(Manifest.RootElement.GetProperty("themes").EnumerateArray())
                .Concat(Manifest.RootElement.GetProperty("customApps").EnumerateArray());

        foreach (var asset in assets)
        {
            var id = asset.TryGetProperty("filename", out var fn)
                ? fn.GetString()!
                : asset.TryGetProperty("themeId", out var tid)
                    ? tid.GetString()!
                    : asset.GetProperty("appId").GetString()!;

            var license = asset.GetProperty("spdxLicense").GetString()!;
            Assert.True(
                knownLicenses.Contains(license),
                $"Asset '{id}' has spdxLicense '{license}' which is not in allowedLicenses, reviewRequiredLicenses, blockedLicenses, or NOASSERTION.");
        }
    }

    /// <summary>
    /// Beautiful Lyrics has NOASSERTION + easyModeDefault=true. It must carry a valid
    /// operator policyOverride to satisfy the unknownLicensePolicy.
    /// </summary>
    [Fact]
    public void Manifest_BeautifulLyricsHasOperatorOverride()
    {
        var bl = Manifest.RootElement.GetProperty("extensions").EnumerateArray()
            .First(e => e.GetProperty("filename").GetString() == "beautiful-lyrics.mjs");

        Assert.Equal("NOASSERTION", bl.GetProperty("spdxLicense").GetString());
        Assert.True(bl.GetProperty("easyModeDefault").GetBoolean());

        Assert.True(
            bl.TryGetProperty("policyOverride", out var po) && po.ValueKind == JsonValueKind.Object,
            "Beautiful Lyrics (NOASSERTION + easyModeDefault) must have a policyOverride.");

        Assert.Equal("operator", po.GetProperty("approvedBy").GetString());
        Assert.True(
            !string.IsNullOrWhiteSpace(po.GetProperty("reason").GetString()),
            "Beautiful Lyrics policyOverride must include a non-empty reason.");
        Assert.True(
            Regex.IsMatch(po.GetProperty("approvedDate").GetString()!, @"^\d{4}-\d{2}-\d{2}$"),
            "Beautiful Lyrics policyOverride approvedDate must be YYYY-MM-DD.");
    }

    /// <summary>
    /// Hazy has NOASSERTION. It must carry a valid operator policyOverride to satisfy
    /// the unknownLicensePolicy.
    /// </summary>
    [Fact]
    public void Manifest_HazyHasOperatorOverride()
    {
        var hazy = Manifest.RootElement.GetProperty("themes").EnumerateArray()
            .First(t => t.GetProperty("themeId").GetString() == "Hazy");

        Assert.Equal("NOASSERTION", hazy.GetProperty("spdxLicense").GetString());

        Assert.True(
            hazy.TryGetProperty("policyOverride", out var po) && po.ValueKind == JsonValueKind.Object,
            "Hazy (NOASSERTION) must have a policyOverride.");

        Assert.Equal("operator", po.GetProperty("approvedBy").GetString());
        Assert.True(
            !string.IsNullOrWhiteSpace(po.GetProperty("reason").GetString()),
            "Hazy policyOverride must include a non-empty reason.");
        Assert.True(
            Regex.IsMatch(po.GetProperty("approvedDate").GetString()!, @"^\d{4}-\d{2}-\d{2}$"),
            "Hazy policyOverride approvedDate must be YYYY-MM-DD.");
    }

    private static JsonDocument LoadManifest()
    {
        var path = Path.Combine(RepoRoot, "schemas", "community-assets.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string ReadFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray()));

    private static Dictionary<string, string> ReadCommunityCustomAppBlocks(string script)
    {
        var registry = Regex.Match(
            script,
            @"\$global:CommunityCustomApps\s*=\s*\[ordered\]@\{(?<body>.+?)\n\}",
            RegexOptions.Singleline);
        Assert.True(registry.Success, "CommunityCustomApps registry was not found in the composed script.");

        // PowerShell accepts a bareword hashtable key, so a key pattern that only
        // matched quoted names let an entry hide from every gate built on this
        // helper. Match any key shape that opens a nested hashtable.
        // The lookahead must not reuse these group names: .NET allows duplicates and
        // keeps the LAST capture, so a named lookahead overwrites the key it just read.
        const string KeyPattern = @"(?:'(?<single>[^']+)'|""(?<double>[^""]+)""|(?<bare>[A-Za-z_][A-Za-z0-9_]*))";
        const string AnyKeyPattern = @"(?:'[^']+'|""[^""]+""|[A-Za-z_][A-Za-z0-9_]*)";
        var blocks = Regex.Matches(
                registry.Groups["body"].Value,
                $@"(?ms)^\s*{KeyPattern}\s*=\s*@\{{(?<entry>.*?)(?=^\s*{AnyKeyPattern}\s*=\s*@\{{|\z)")
            .ToDictionary(
                match => match.Groups["single"].Success
                    ? match.Groups["single"].Value
                    : match.Groups["double"].Success
                        ? match.Groups["double"].Value
                        : match.Groups["bare"].Value,
                match => match.Groups["entry"].Value,
                StringComparer.Ordinal);

        // Every top-level key in the registry must be one this parser understands,
        // so a shape it cannot read fails loudly instead of vanishing. Top level is
        // the shallowest indentation that opens a nested hashtable, which keeps this
        // working across the data file and both composed hosts.
        var opens = Regex.Matches(registry.Groups["body"].Value, @"(?m)^(?<indent>[ \t]+)(?<key>\S+)\s*=\s*@\{").ToArray();
        Assert.NotEmpty(opens);
        var topLevelIndent = opens.Min(match => match.Groups["indent"].Value.Length);
        var declared = opens
            .Where(match => match.Groups["indent"].Value.Length == topLevelIndent)
            .Select(match => match.Groups["key"].Value.Trim('\'', '"'))
            .ToArray();
        Assert.Equal(declared.OrderBy(key => key, StringComparer.Ordinal), blocks.Keys.OrderBy(key => key, StringComparer.Ordinal));

        return blocks;
    }

    private static IEnumerable<JsonElement> AllAssets() =>
        Manifest.RootElement.GetProperty("extensions").EnumerateArray()
            .Concat(Manifest.RootElement.GetProperty("themes").EnumerateArray())
            .Concat(Manifest.RootElement.GetProperty("customApps").EnumerateArray());

    private static string AssetId(JsonElement asset) =>
        asset.TryGetProperty("filename", out var filename)
            ? filename.GetString()!
            : asset.TryGetProperty("themeId", out var themeId)
                ? themeId.GetString()!
                : asset.GetProperty("appId").GetString()!;

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
