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
