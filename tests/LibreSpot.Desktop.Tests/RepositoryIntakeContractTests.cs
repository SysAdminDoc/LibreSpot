using System.Linq;
using System.IO;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class RepositoryIntakeContractTests
{
    [Fact]
    public void BugReport_UsesCurrentSurfacesAndSafeEvidenceHandoff()
    {
        var form = ReadRepoFile(".github", "ISSUE_TEMPLATE", "bug-report.yml");

        Assert.Contains("Recommended setup", form);
        Assert.Contains("Fleet CLI (LibreSpot.Cli.exe)", form);
        Assert.Contains("Operation ID", form);
        Assert.Contains("Maintenance > Support bundle", form);
        Assert.Contains("never uploaded by LibreSpot", form);
        Assert.DoesNotContain("Easy Install", form);
        Assert.DoesNotContain("preview.6", form);
        Assert.DoesNotContain("paste the install log", form, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompatibilityAndFeatureForms_NameFleetAndCorrelationEvidence()
    {
        var compatibility = ReadRepoFile(".github", "ISSUE_TEMPLATE", "compatibility.yml");
        var feature = ReadRepoFile(".github", "ISSUE_TEMPLATE", "feature-request.yml");

        Assert.Contains("Fleet CLI (LibreSpot.Cli.exe)", compatibility);
        Assert.Contains("Operation ID", compatibility);
        Assert.Contains("Maintenance > Support bundle", compatibility);
        Assert.Contains("Fleet CLI / automation", feature);
    }

    [Fact]
    public void PullRequestChecklist_DescribesActualLocalValidation()
    {
        var template = ReadRepoFile(".github", "PULL_REQUEST_TEMPLATE.md");

        Assert.Contains("Build-Scripts.ps1 -Validate", template);
        Assert.Contains("Build-Scripts.ps1 -Lint", template);
        Assert.Contains("Invoke-Pester", template);
        Assert.DoesNotContain("CI checks this", template);
    }

    [Fact]
    public void RootMarkdown_IsExactlyTheDocumentSetTheHygieneRuleAllows()
    {
        // AGENTS.md names the tracked root documents. design-qa.md sat outside
        // that list and was listed in .gitignore at the same time, which does
        // nothing for an already tracked file: the rule and the tree disagreed
        // and only the tree counted.
        var root = RepoRoot();
        var allowed = new[]
        {
            "README.md",
            "CHANGELOG.md",
            "ROADMAP.md",
            "RESEARCH.md",
            "SECURITY.md",
            "SIGNPATH.md",
            "Roadmap_Blocked.md",
        };

        var listing = Git(root, "ls-files -- *.md");
        // git returns 128 outside a work tree and writes nothing to stdout. Left
        // unchecked the whole gate became a silent no-op in a source zip or a
        // checkout without .git.
        Assert.True(
            listing.ExitCode == 0,
            $"git ls-files failed with exit {listing.ExitCode}, so this gate checked nothing: {listing.Error}");

        var tracked = listing.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !line.Contains('/'))
            .ToArray();

        // Set equality, not one-way containment: a documented file that stops
        // being tracked is as wrong as an undocumented one that starts.
        Assert.Equal(
            allowed.OrderBy(name => name, StringComparer.Ordinal),
            tracked.OrderBy(name => name, StringComparer.Ordinal));

        // And nothing tracked may also be listed as ignored, because the ignore
        // silently does nothing and the next reader cannot tell which rule won.
        foreach (var file in tracked)
        {
            var ignored = Git(root, $"check-ignore --no-index -- {file}");
            Assert.True(
                ignored.ExitCode is 0 or 1,
                $"git check-ignore failed with exit {ignored.ExitCode}: {ignored.Error}");
            Assert.True(
                ignored.Output.Trim().Length == 0,
                $"{file} is tracked and also ignored by .gitignore. Pick one.");
        }
    }

    private static (int ExitCode, string Output, string Error) Git(string root, string arguments)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"-C \"{root}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;

        // Both pipes are read before waiting; reading one to the end first can
        // deadlock when the other fills its buffer.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }

    private static string ReadRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
        return File.ReadAllText(Path.Combine(new[] { root }.Concat(relativeParts).ToArray()));
    }
}
