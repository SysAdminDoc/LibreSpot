using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ReleaseTruthTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void PreviewClaimsMatchProjectsShellAndReadme()
    {
        var desktopVersion = ProjectValue("src/LibreSpot.Desktop/LibreSpot.Desktop.csproj", "Version");
        var desktopInformationalVersion = ProjectValue("src/LibreSpot.Desktop/LibreSpot.Desktop.csproj", "InformationalVersion");
        var cliVersion = ProjectValue("src/LibreSpot.Cli/LibreSpot.Cli.csproj", "Version");
        var viewModel = Read("src/LibreSpot.Desktop/ViewModels/MainViewModel.cs");
        var readme = Read("README.md");

        Assert.Equal(desktopVersion, desktopInformationalVersion);
        Assert.Equal(desktopVersion, cliVersion);
        Assert.Contains("AssemblyInformationalVersionAttribute", viewModel);
        Assert.Contains("public string ShellDisplayVersion => $\"v{ProductVersion}\";", viewModel);
        Assert.DoesNotContain("ShellDisplayVersion => \"v", viewModel);
        Assert.Contains($"Version-{desktopVersion.Replace("-", "--")}-brightgreen.svg", readme);
        Assert.Contains($"## What's New in v{desktopVersion}", readme);
    }

    [Fact]
    public void ReadmeDistinguishesSourceScriptFromPublicStableRelease()
    {
        var readme = Read("README.md");
        var mainVersion = ScriptVersion("LibreSpot.ps1");
        var backendVersion = ScriptVersion("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1");
        var stableBadge = Match(readme, "Stable-(?<value>\\d+\\.\\d+\\.\\d+)-blue\\.svg");

        Assert.Equal(mainVersion, backendVersion);
        Assert.Contains($"Current source script version: **v{mainVersion}**", readme);
        Assert.Contains($"public latest stable release, v{stableBadge}", readme);
        Assert.DoesNotContain($"current latest stable release, v{mainVersion}", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerificationSectionNamesTheOnlyPlaceLibreSpotIsPublished()
    {
        // Several lookalike projects outrank this one on stars in the same
        // searches, so "download it from the official repo" is not enough on its
        // own: a reader has to be able to recognise one, and to check a file
        // rather than trust a page.
        var readme = Read("README.md");
        var security = Read("SECURITY.md");
        var verification = readme.Split("## How to verify a LibreSpot download")[1].Split("## ")[0];

        const string canonical = "https://github.com/SysAdminDoc/LibreSpot/releases";
        Assert.Contains(canonical, verification, StringComparison.Ordinal);
        Assert.Contains(canonical, security, StringComparison.Ordinal);

        Assert.Contains("gh release verify-asset", verification, StringComparison.Ordinal);
        Assert.Contains("activation", verification, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("template", verification, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplicationControlGuidanceCoversEveryShippedArtifactNotJustTheScript()
    {
        // The FAQ answered for LibreSpot.ps1 alone while the desktop executable had
        // become the first install path. Smart App Control blocks unsigned code with
        // no per-app allowance, so a reader on that device needs to be told the
        // executables are blocked too and that there is nothing to click through.
        var readme = Read("README.md");
        var security = Read("SECURITY.md");

        var smartAppControl = readme.Split("**Smart App Control")[1].Split("---")[0];

        foreach (var artifact in new[] { "LibreSpot-Desktop.exe", "LibreSpot.Cli.exe", "LibreSpot.exe", "LibreSpot.ps1" })
        {
            Assert.Contains(artifact, smartAppControl, StringComparison.Ordinal);
        }

        Assert.Contains("no per-app allowance", smartAppControl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("evaluation", smartAppControl, StringComparison.OrdinalIgnoreCase);

        // Nothing anywhere may tell a reader to turn the feature off.
        foreach (var text in new[] { readme, security })
        {
            Assert.DoesNotContain("disable Smart App Control", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("turn off Smart App Control", text, StringComparison.OrdinalIgnoreCase);
        }

        // SmartScreen reputation does not carry across releases, and both documents
        // should say so rather than leaving a returning user to wonder.
        Assert.Contains("reputation", security, StringComparison.OrdinalIgnoreCase);
        var smartScreen = readme.Split("**Windows SmartScreen says")[1].Split("**Smart App Control")[0];
        Assert.Contains("next release", smartScreen, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupportAndSigningDocsMatchTheStableReleaseLine()
    {
        var desktopVersion = ProjectValue("src/LibreSpot.Desktop/LibreSpot.Desktop.csproj", "Version");
        var security = Read("SECURITY.md");
        var supportTable = security.Split("## Supported Versions")[1].Split("## ")[0];
        var docs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SECURITY.md"] = security,
            ["SIGNPATH.md"] = Read("SIGNPATH.md"),
            ["Roadmap_Blocked.md"] = Read("Roadmap_Blocked.md"),
            ["README.md"] = Read("README.md")
        };

        if (!desktopVersion.Contains('-', StringComparison.Ordinal))
        {
            // Stable metadata must not sit beside preview-only support wording.
            Assert.Matches(@"(?m)^\| v4\.0\.x and later[^|]*\| Yes", supportTable);
            Assert.Contains("v3.7.x", supportTable, StringComparison.Ordinal);
            Assert.Contains("Superseded", supportTable, StringComparison.Ordinal);
            Assert.DoesNotContain("-preview", supportTable, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Best-effort", supportTable, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var (path, content) in docs)
        {
            foreach (var pendingClaim in new[] { "pending-enrollment", "enrollment is pending", "pending signing", "SignPath credentials", "Complete SignPath Foundation enrollment", "once signing is unblocked", "signing enrollment", "signed CLI artifact", "unsigned-gated", "waits until signing" })
            {
                Assert.False(
                    content.Contains(pendingClaim, StringComparison.OrdinalIgnoreCase),
                    $"{path} still describes signing as pending: '{pendingClaim}'.");
            }
        }

        Assert.Contains("unsigned by design", docs["SECURITY.md"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unsigned by design", docs["SIGNPATH.md"], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("latest stable release is still `v3.7.2`", docs["Roadmap_Blocked.md"], StringComparison.Ordinal);
        Assert.DoesNotContain("release workflow", docs["Roadmap_Blocked.md"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LiveReleaseValidatorQueriesGithubLatestAndRequiredAssets()
    {
        var buildScript = Read("Build-Scripts.ps1");

        Assert.Contains("repos/SysAdminDoc/LibreSpot/releases/latest", buildScript);
        Assert.Contains("'LibreSpot.ps1', 'LibreSpot.exe', 'checksums.txt'", buildScript);
        Assert.Contains("Test-PublicReleaseTruth", buildScript);
        Assert.Contains("Test-LocalReleaseTruth", buildScript);
        Assert.Contains("Get-LibreSpotProjectInformationalVersion", buildScript);
        Assert.DoesNotContain("must be a literal v-prefixed version", buildScript);
    }

    private static string ScriptVersion(string relativePath) =>
        Match(Read(relativePath), "(?m)^\\$global:VERSION\\s*=\\s*'(?<value>[^']+)'");

    private static string ProjectValue(string relativePath, string elementName)
    {
        var document = XDocument.Load(Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return document.Descendants(elementName).Select(element => element.Value.Trim()).First(value => value.Length > 0);
    }

    private static string Match(string content, string pattern)
    {
        var match = Regex.Match(content, pattern);
        Assert.True(match.Success, $"Expected pattern was not found: {pattern}");
        return match.Groups["value"].Value;
    }

    [Fact]
    public void ReadmeUninstallerPhaseCountMatchesTheScript()
    {
        var script = Read("LibreSpot.ps1");
        var readme = Read("README.md");

        var logged = Regex.Matches(script, @"\[Phase (?<index>\d+)/(?<total>\d+)\]")
            .Select(match => (Index: int.Parse(match.Groups["index"].Value), Total: int.Parse(match.Groups["total"].Value)))
            .ToList();
        Assert.NotEmpty(logged);
        var total = Assert.Single(logged.Select(entry => entry.Total).Distinct());
        Assert.Equal(Enumerable.Range(1, total), logged.Select(entry => entry.Index).Distinct().OrderBy(index => index));

        var section = Section(readme, "### Comprehensive Uninstaller");
        var steps = Regex.Matches(section, @"(?m)^(?<index>\d+)\. ").Select(match => int.Parse(match.Groups["index"].Value)).ToList();
        Assert.Equal(Enumerable.Range(1, total), steps);
        Assert.DoesNotMatch(@"\d+-phase uninstaller", section);
    }

    [Fact]
    public void ReadmeCitesTheSmartAppControlUpdateThatShipped()
    {
        // KB5079391 was withdrawn for install failure 0x80073712 and replaced
        // by out-of-band KB5086672 on 2026-03-31. A reader who searched for the
        // cited update found a removed one.
        var readme = Read("README.md");

        Assert.Contains("KB5086672", readme, StringComparison.Ordinal);
        Assert.Contains("2026-03-31", readme, StringComparison.Ordinal);
        Assert.Contains("26200.8117 and 26100.8117", readme, StringComparison.Ordinal);
        Assert.Contains("No clean install is needed.", readme, StringComparison.Ordinal);

        // The withdrawn update may still be named, but only as the one that was
        // pulled, never as the update to install.
        foreach (Match mention in Regex.Matches(readme, @"[^.]*KB5079391[^.]*\."))
        {
            Assert.Contains("withdrawn", mention.Value, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ReadmeOpensWithReportedFailuresAndEveryAnchorResolves()
    {
        // The most-reported SpotX and Spicetify failures are all shipped
        // LibreSpot features, and the README explained the mechanism 300 lines
        // down. A section that sends readers to headings that do not exist is
        // worse than no section, so every in-page link is resolved here.
        var readme = Read("README.md");
        var section = Section(readme, "## What keeps breaking, and what LibreSpot does about it");
        Assert.False(string.IsNullOrWhiteSpace(section), "README.md no longer opens with the reported-failure section.");

        // It has to come before the first instruction, or it is not positioning.
        Assert.True(
            readme.IndexOf("## What keeps breaking", StringComparison.Ordinal)
                < readme.IndexOf("## Quick Start", StringComparison.Ordinal),
            "The reported-failure section must sit above Quick Start.");

        var threads = Regex.Matches(section, @"\]\((?<url>https://(?:github\.com|www\.reddit\.com)/[^)]+)\)")
            .Select(match => match.Groups["url"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.True(threads.Length >= 5, $"The section cites {threads.Length} public threads; at least five failure modes are required.");

        var headingSlugs = Regex.Matches(readme, @"(?m)^#{2,6} (?<title>.+)$")
            .Select(match => GitHubSlug(match.Groups["title"].Value.Trim()))
            .ToHashSet(StringComparer.Ordinal);

        var anchors = Regex.Matches(section, @"\]\(#(?<anchor>[^)]+)\)")
            .Select(match => match.Groups["anchor"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.True(anchors.Length >= 5, $"The section resolves {anchors.Length} in-page anchors; each failure mode needs one.");

        foreach (var anchor in anchors)
        {
            Assert.True(
                headingSlugs.Contains(anchor),
                $"README anchor '#{anchor}' does not resolve to any heading in README.md.");
        }
    }

    // GitHub's heading slug: lower-cased, punctuation dropped, spaces to
    // hyphens. "Trust & risk disclosure" becomes "trust--risk-disclosure"
    // because the ampersand leaves its spaces behind.
    private static string GitHubSlug(string heading)
    {
        var lowered = heading.ToLowerInvariant();
        var kept = new System.Text.StringBuilder(lowered.Length);
        foreach (var character in lowered)
        {
            if (char.IsLetterOrDigit(character) || character == '-' || character == ' ' || character == '_')
            {
                kept.Append(character);
            }
        }

        return kept.ToString().Replace(' ', '-');
    }

    [Fact]
    public void ReadmeExtensionCountMatchesTheInstallerData()
    {
        // README said 16 while the data files carry ten built in and five
        // community. The number is only useful if it is read off the same
        // source the installer uses.
        var script = Read("LibreSpot.ps1");
        var readme = Read("README.md");

        var builtIn = CountOrderedEntries(script, "BuiltInExtensions");
        var community = CountOrderedEntries(script, "CommunityExtensions");
        Assert.True(builtIn > 0 && community > 0, "Could not read either extension block.");

        var total = builtIn + community;
        // The counts have to match; whether the prose spells them as words or
        // digits is the writer's call, so the pattern accepts either. Pinning
        // one spelling would fail a correct sentence for the wrong reason.
        Assert.Matches(
            new Regex(
                $@"select from {total} extensions, (?:{builtIn}|{NumberWord(builtIn)}) built in and "
                    + $@"(?:{community}|{NumberWord(community)}) community,"),
            readme);
        Assert.Contains($"### {total} Extensions ({builtIn} Built-in + {community} Community)", readme, StringComparison.Ordinal);

        // The Store counts the shared catalog, which also holds the first-party
        // companion nobody selects, so it reads one higher than this total. The
        // counting rule lives in the catalog's $comment; pin both ends of it so
        // a new entry cannot quietly change either number.
        using var catalog = System.Text.Json.JsonDocument.Parse(Read("schemas/librespot-customization.json"));
        var catalogExtensions = catalog.RootElement.GetProperty("extensions").EnumerateArray().ToArray();
        var companions = catalogExtensions
            .Where(entry => string.Equals(entry.GetProperty("source").GetString(), "SysAdminDoc/LibreSpot", StringComparison.Ordinal))
            .ToArray();
        Assert.Single(companions);
        Assert.Equal("librespot-engine.js", companions[0].GetProperty("id").GetString());
        Assert.Equal(total + companions.Length, catalogExtensions.Length);

        var rule = catalog.RootElement.GetProperty("$comment").GetString() ?? string.Empty;
        Assert.Contains("librespot-engine.js", rule, StringComparison.Ordinal);
        Assert.Contains("The Store header counts this list", rule, StringComparison.Ordinal);

        // And the README has to say why the screenshot beside it reads higher.
        Assert.Contains(
            $"reads one higher than the {total} you can pick",
            readme,
            StringComparison.Ordinal);
    }

    private static int CountOrderedEntries(string script, string variableName)
    {
        var block = Regex.Match(
            script,
            @"(?ms)^\$global:" + Regex.Escape(variableName) + @"\s*=\s*\[ordered\]@\{(?<body>.+?)^\}");
        return block.Success
            ? Regex.Matches(block.Groups["body"].Value, @"(?m)^\s{4}""[^""]+""\s*=").Count
            : 0;
    }

    // Only the spellings the README uses today. Anything else falls back to the
    // digits, and the caller accepts either form.
    private static string NumberWord(int value) => value switch
    {
        5 => "five",
        10 => "ten",
        11 => "eleven",
        12 => "twelve",
        _ => value.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    [Fact]
    public void ReadmeSaysWhichSurfacesAreEnglishOnlyUntilTheyAreNot()
    {
        // The desktop shell ships five reviewed locales, so "every language is
        // reachable" reads as covering the whole product. The panel inside
        // Spotify and the standalone script have no lookup layer at all, and the
        // README said nothing about it. This retires itself: once those strings
        // enter the translation scope, the claim has to come back out.
        var readme = Read("README.md");
        var crowdin = Read(".crowdin.yml");

        var appIsTranslated = crowdin.Contains("LibreSpot.App", StringComparison.Ordinal);

        if (appIsTranslated)
        {
            Assert.False(
                readme.Contains("in-client panel does not", StringComparison.Ordinal),
                "The in-Spotify app is in the Crowdin scope now, so README.md must stop saying its panel is English "
                    + "only. Update the sentence next to the language picker.");
            return;
        }

        // The clause has two halves and the gate used to check one of them. It
        // asserted "are English" and "in-client panel does not", so deleting the
        // sentence that says the shell is translated at all kept it green, and
        // "are English" was loose enough for any unrelated sentence to satisfy.
        // Both halves, and the two surfaces by name, or the README can say half
        // the truth and pass.
        Assert.Matches(@"all five interfaces are complete and translation-reviewed", readme);
        Assert.Matches(
            @"panel LibreSpot adds inside Spotify and the standalone `LibreSpot\.ps1` window are English only",
            readme);
        Assert.Contains("in-client panel does not", readme, StringComparison.Ordinal);

        // The five locales the shell claims have to be the five it ships, or the
        // half of the sentence that is not about English is wrong instead.
        // Strings.resx is the neutral fallback; the five interfaces are the five
        // culture-qualified files beside it.
        var cultures = Directory
            .GetFiles(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Properties"), "Strings.*.resx")
            .Length;
        Assert.True(
            cultures == 5,
            $"README says five interfaces are complete, but Properties holds {cultures} culture-qualified resx files. "
                + "Update the sentence and this count together.");
    }

    [Fact]
    public void RepositoryRootShipsExactlyOneIcon()
    {
        // There used to be two byte-identical icons at the root, consumed by
        // different build paths: the PS2EXE compile took one and the WPF project
        // the other. Nothing kept them in step, so an icon change could ship two
        // artifacts wearing different faces.
        var icons = Directory.GetFiles(RepoRoot, "*.ico", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            icons.Length == 1,
            "The repository root must hold exactly one .ico so every build path uses the same one. Found: "
                + string.Join(", ", icons));

        var icon = icons[0]!;

        // Both consumers have to name that file, or the single icon is single in
        // name only and a build breaks instead of drifting.
        Assert.Contains($"Join-Path $PSScriptRoot '{icon}'", Read("Build-Scripts.ps1"), StringComparison.Ordinal);

        var desktopProject = Read("src/LibreSpot.Desktop/LibreSpot.Desktop.csproj");
        Assert.Contains($@"<ApplicationIcon>..\..\{icon}</ApplicationIcon>", desktopProject, StringComparison.Ordinal);
        Assert.Contains($@"<Resource Include=""..\..\{icon}""", desktopProject, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadmeThemeCountMatchesThePreviewManifest()
    {
        // RD-190 gated the extension and lyrics-theme counts and left this one
        // behind, so a theme could be added to the manifest without the README
        // moving. The manifest carries one entry that is not a theme, the
        // Marketplace-only placeholder the gallery shows when nothing is picked.
        var readme = Read("README.md");
        using var manifest = System.Text.Json.JsonDocument.Parse(Read("schemas/theme-preview-manifest.json"));

        var entries = manifest.RootElement.GetProperty("themes").EnumerateArray().ToArray();
        var placeholders = entries
            .Where(entry => (entry.GetProperty("id").GetString() ?? string.Empty).Contains("Marketplace Only", StringComparison.Ordinal))
            .ToArray();

        // Without this the count could silently become "every entry" if the
        // placeholder were ever renamed, and the README would then be wrong in
        // the other direction while this test stayed green.
        Assert.True(
            placeholders.Length == 1,
            $"Expected exactly one Marketplace-only placeholder in the preview manifest, found {placeholders.Length}. "
                + "If the placeholder was renamed, update this test rather than the count.");

        var themes = entries.Except(placeholders).ToArray();
        var expected = themes.Length;

        // "22 Themes, 200+ Color Schemes" sat two lines above a body that said
        // 24, because the old regex only matched "N supported themes". Match
        // the bare "<n> themes" heading form too, but not "27 Lyrics Color
        // Themes", which counts SpotX lyrics options rather than themes.
        var matches = Regex.Matches(readme, @"\b(?<count>\d+) (?:supported )?themes\b", RegexOptions.IgnoreCase);
        // Both forms have to be present, not just two of either. The heading
        // carries the bare form and the body carries "supported themes"; a
        // count-only check passed while the heading had lost its number.
        Assert.True(
            Regex.IsMatch(readme, @"\b\d+ themes\b", RegexOptions.IgnoreCase),
            "README.md no longer states a bare theme count in the section heading.");
        Assert.True(
            Regex.IsMatch(readme, @"\b\d+ supported themes\b", RegexOptions.IgnoreCase),
            "README.md no longer states a supported theme count in the body.");

        foreach (Match match in matches)
        {
            Assert.Equal(expected, int.Parse(match.Groups["count"].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        // The named per-source lists have to add up to that total, otherwise a
        // reader counting the names lands somewhere else than the heading.
        foreach (var source in new[] { "official", "community" })
        {
            var sourceCount = themes.Count(entry =>
                string.Equals(entry.GetProperty("source").GetString(), source, StringComparison.Ordinal));
            var listed = Regex.Matches(readme, $@"\b(?<count>\d+) {source} themes\b", RegexOptions.IgnoreCase);
            Assert.True(listed.Count > 0, $"README.md no longer states how many {source} themes ship.");
            foreach (Match match in listed)
            {
                Assert.Equal(sourceCount, int.Parse(match.Groups["count"].Value, System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        // Scheme totals drift the same way. "200+" outran the manifest by 48.
        var expectedSchemes = themes.Sum(entry => entry.GetProperty("schemes").GetArrayLength());
        var schemeMatches = Regex.Matches(readme, @"\b(?<count>\d+)(?<floor>\+)? color schemes\b", RegexOptions.IgnoreCase);
        Assert.True(schemeMatches.Count > 0, "README.md no longer states a color scheme count.");

        foreach (Match match in schemeMatches)
        {
            var claimed = int.Parse(match.Groups["count"].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (match.Groups["floor"].Success)
            {
                Assert.True(
                    expectedSchemes >= claimed,
                    $"README claims {claimed}+ color schemes but the preview manifest carries {expectedSchemes}.");
            }
            else
            {
                Assert.Equal(expectedSchemes, claimed);
            }
        }
    }

    [Fact]
    public void ThemesRetainedAfterUpstreamRemovalSaySoInBothPlaces()
    {
        // Blackout was deleted from spicetify-themes on 2026-07-14, after the
        // commit LibreSpot pins. It still ships, which is a deliberate choice,
        // so the README has to say that rather than imply it is current
        // upstream. The manifest date and the README note move together.
        var readme = Read("README.md");
        using var manifest = System.Text.Json.JsonDocument.Parse(Read("schemas/theme-preview-manifest.json"));

        var retained = manifest.RootElement.GetProperty("themes").EnumerateArray()
            .Where(theme => theme.TryGetProperty("retainedAfterUpstreamRemoval", out _))
            .ToArray();
        Assert.NotEmpty(retained);

        foreach (var theme in retained)
        {
            var id = theme.GetProperty("id").GetString()!;
            var removed = theme.GetProperty("retainedAfterUpstreamRemoval").GetString()!;
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", removed);
            Assert.Contains($"{id} is retained deliberately.", readme, StringComparison.Ordinal);
            Assert.Contains($"Upstream removed it from `spicetify-themes` on {removed}", readme, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ReadmeStrykerBaselineIsNotPresentedAsNewerThanItIs()
    {
        // The README used to call the mutation baseline "current" with no date.
        // StrykerOutput/ is gitignored, so nothing in a checkout can confirm the
        // figure, and the files it measures kept changing under it. Record when
        // it was measured and refuse to let that date fall behind the code.
        var readme = Read("README.md");

        var measured = Regex.Match(readme, @"tested mutants, measured (?<date>\d{4}-\d{2}-\d{2})");
        Assert.True(
            measured.Success,
            "README.md must say when the Stryker baseline was measured, in the form "
                + "\"tested mutants, measured YYYY-MM-DD\". Calling a figure current says nothing a reader can check.");

        Assert.DoesNotContain("The current baseline is", readme, StringComparison.Ordinal);

        var measuredOn = DateTimeOffset.Parse(
            measured.Groups["date"].Value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal);

        // The config names what Stryker mutates. A baseline measured before the
        // newest change to any of those files describes code that no longer
        // exists.
        using var config = System.Text.Json.JsonDocument.Parse(Read("src/LibreSpot.Core/stryker-config.json"));
        var globs = config.RootElement
            .GetProperty("stryker-config")
            .GetProperty("mutate")
            .EnumerateArray()
            .Select(entry => Path.GetFileName(entry.GetString() ?? string.Empty))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        Assert.NotEmpty(globs);

        DateTimeOffset? newest = null;
        var newestFile = string.Empty;
        foreach (var name in globs)
        {
            var relative = $"src/LibreSpot.Core/{name}";
            Assert.True(
                File.Exists(Path.Combine(RepoRoot, relative)),
                $"stryker-config.json mutates {name} but {relative} does not exist.");

            var stamp = GitCommitDate(relative);
            // A shallow clone or an export has no history for the file. Saying so
            // beats quietly passing on a comparison that never happened.
            Assert.True(stamp.HasValue, $"Could not read the last commit date for {relative} from git.");

            if (newest is null || stamp!.Value > newest.Value)
            {
                newest = stamp;
                newestFile = relative;
            }
        }

        // Both sides reduced to a UTC calendar day. The README carries a date and
        // the commit carries an instant, so comparing them directly would call a
        // baseline measured the same afternoon "older" than the morning's commit.
        Assert.True(
            measuredOn.UtcDateTime.Date >= newest!.Value.UtcDateTime.Date,
            $"The README records the Stryker baseline as measured {measured.Groups["date"].Value}, but {newestFile} "
                + $"changed on {newest.Value:yyyy-MM-dd}. Re-run `dotnet stryker` from src/LibreSpot.Core and update "
                + "the figure and the date together, or the README is describing code that has moved on.");
    }

    private static DateTimeOffset? GitCommitDate(string relativePath)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"-C \"{RepoRoot}\" log -1 --format=%cI -- \"{relativePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;

        // Both pipes are read before waiting; draining one first can deadlock
        // when the other fills its buffer.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        _ = error.GetAwaiter().GetResult();

        var text = output.GetAwaiter().GetResult().Trim();
        if (process.ExitCode != 0 || text.Length == 0)
        {
            return null;
        }

        return DateTimeOffset.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void ReadmeLyricsThemeCountMatchesTheCatalog()
    {
        var readme = Read("README.md");
        var expected = AppCatalog.LyricsThemes.Count;

        var heading = Regex.Match(readme, @"### (?<count>\d+) Lyrics Color Themes");
        Assert.True(heading.Success, "The lyrics theme heading is missing from README.md.");
        Assert.Equal(expected, int.Parse(heading.Groups["count"].Value));

        var sentence = Regex.Match(readme, @"exposes all (?<count>\d+) SpotX static lyrics color options: (?<list>[^.]+)\.");
        Assert.True(sentence.Success, "The lyrics theme list is missing from README.md.");
        Assert.Equal(expected, int.Parse(sentence.Groups["count"].Value));

        var listed = sentence.Groups["list"].Value
            .Replace(", and ", ", ")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(AppCatalog.LyricsThemes.OrderBy(name => name, StringComparer.Ordinal),
            listed.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void ReadmeNamesTheLaneForClaimsOnlyOneLaneFulfils()
    {
        var readme = Read("README.md");
        var details = Section(readme, "### Other Details");

        // Every claim here held for the script long before the desktop app existed.
        // Presenting them as product-wide misled anyone reading the download page.
        Assert.Contains("Only the script keeps its own window on top", details, StringComparison.Ordinal);
        Assert.DoesNotContain("LibreSpot stays on top until finished", details, StringComparison.Ordinal);
        Assert.Contains("**Self-elevating script**", details, StringComparison.Ordinal);
        Assert.DoesNotContain("- **Self-elevating**, auto-requests admin privileges when needed", details, StringComparison.Ordinal);
        Assert.Contains("built for x64 only", details, StringComparison.Ordinal);
        Assert.DoesNotContain("- **Architecture support**, x64 and ARM64 with per-architecture hash verification", details, StringComparison.Ordinal);
        Assert.Contains("the script runs an install in background runspaces", details, StringComparison.Ordinal);

        // The desktop shell has no reachable global search, so no document may
        // describe one until the collapsed shell decision is made.
        Assert.DoesNotContain("from global search", Read("CHANGELOG.md"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadmePresentsTheSpotXPinByCommitAndExplainsItsRecordedVersion()
    {
        var readme = Read("README.md");

        Assert.Matches(@"\| SpotX \| `[0-9a-f]{8}`, \d{4}-\d{2}-\d{2} \(Spotify \d+\.\d+\.\d+\) \|", readme);
        Assert.Contains("upstream's newest tag is 1.9", readme, StringComparison.Ordinal);
        Assert.Contains("its own adapter version for commit", readme, StringComparison.Ordinal);
    }

    private static string Section(string document, string heading)
    {
        var start = document.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Heading '{heading}' is missing.");
        // Stop at the next heading of ANY level. Scanning only for "### " ran past
        // a following "## " and swallowed unrelated sections, which made every
        // assertion below satisfiable by text somewhere else in the file.
        var next = Regex.Match(document[(start + heading.Length)..], @"(?m)^#{1,6} ");
        var end = next.Success ? start + heading.Length + next.Index : document.Length;
        return document[start..end];
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }
}
