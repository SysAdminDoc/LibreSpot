using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ReleaseArtifactContractTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();
    private static readonly JsonDocument Contract = LoadContract();

    [Fact]
    public void Contract_AllRequiredArtifactsAppearInWorkflowUpload()
    {
        var workflow = ReadFile(".github", "workflows", "release.yml");

        var requiredNames = Contract.RootElement
            .GetProperty("artifacts").EnumerateArray()
            .Where(a => a.GetProperty("required").GetBoolean())
            .Select(a => a.GetProperty("name").GetString()!)
            .ToList();

        foreach (var name in requiredNames)
        {
            Assert.True(
                workflow.Contains(name),
                $"Required artifact '{name}' not found in release workflow upload.");
        }
    }

    [Fact]
    public void Contract_ChecksumCoveredAssetsMatchArtifactsWithChecksumEntry()
    {
        var checksumCovered = Contract.RootElement
            .GetProperty("checksumContract")
            .GetProperty("coveredAssets").EnumerateArray()
            .Select(v => v.GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var artifactsWithChecksum = Contract.RootElement
            .GetProperty("artifacts").EnumerateArray()
            .Where(a => a.GetProperty("checksumEntry").GetBoolean())
            .Select(a => a.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(artifactsWithChecksum, checksumCovered);
    }

    [Fact]
    public void Contract_BuildProvenanceSubjectsAreRequiredArtifacts()
    {
        var requiredNames = Contract.RootElement
            .GetProperty("artifacts").EnumerateArray()
            .Where(a => a.GetProperty("required").GetBoolean())
            .Select(a => a.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var subjects = Contract.RootElement
            .GetProperty("attestationContract")
            .GetProperty("buildProvenance")
            .GetProperty("subjects").EnumerateArray()
            .Select(v => v.GetString()!)
            .ToList();

        foreach (var subject in subjects)
        {
            Assert.True(
                requiredNames.Contains(subject),
                $"Build provenance subject '{subject}' is not a required artifact.");
        }
    }

    [Fact]
    public void Contract_WorkflowUsesAttestationActions()
    {
        var workflow = ReadFile(".github", "workflows", "release.yml");

        var provenanceAction = Contract.RootElement
            .GetProperty("attestationContract")
            .GetProperty("buildProvenance")
            .GetProperty("action").GetString()!;

        var sbomAction = Contract.RootElement
            .GetProperty("attestationContract")
            .GetProperty("sbom")
            .GetProperty("action").GetString()!;

        Assert.Contains(provenanceAction, workflow);
        Assert.Contains(sbomAction, workflow);
    }

    [Fact]
    public void Contract_TagPatternsAreValidRegex()
    {
        foreach (var prop in Contract.RootElement.GetProperty("tagPatterns").EnumerateObject())
        {
            var pattern = prop.Value.GetString()!;
            var ex = Record.Exception(() => new Regex(pattern));
            Assert.Null(ex);
        }
    }

    [Fact]
    public void Contract_WorkflowValidatesTagFormat()
    {
        var workflow = ReadFile(".github", "workflows", "release.yml");
        Assert.True(
            Regex.IsMatch(workflow, @"preview|rc", RegexOptions.IgnoreCase),
            "Release workflow must distinguish preview/rc from stable tags.");
    }

    [Fact]
    public void Contract_AllArtifactsHaveRequiredFields()
    {
        var required = new[] { "name", "channels", "required", "checksumEntry", "signingRequirement", "attestation" };

        foreach (var artifact in Contract.RootElement.GetProperty("artifacts").EnumerateArray())
        {
            var name = artifact.GetProperty("name").GetString()!;
            foreach (var field in required)
            {
                Assert.True(
                    artifact.TryGetProperty(field, out _),
                    $"Artifact '{name}' is missing required field '{field}'.");
            }
        }
    }

    [Fact]
    public void Contract_DistributionChannelsReferenceKnownChannels()
    {
        var matrixPath = Path.Combine(RepoRoot, "schemas", "distribution-matrix.json");
        if (!File.Exists(matrixPath))
            return;

        var matrix = JsonDocument.Parse(File.ReadAllText(matrixPath));
        var knownChannels = matrix.RootElement
            .GetProperty("channels").EnumerateArray()
            .Select(c => c.GetProperty("channel").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var artifact in Contract.RootElement.GetProperty("artifacts").EnumerateArray())
        {
            if (!artifact.TryGetProperty("distributionChannels", out var channels))
                continue;

            var name = artifact.GetProperty("name").GetString()!;
            foreach (var ch in channels.EnumerateArray())
            {
                Assert.True(
                    knownChannels.Contains(ch.GetString()!),
                    $"Artifact '{name}' references unknown distribution channel '{ch.GetString()}'.");
            }
        }
    }

    private static JsonDocument LoadContract()
    {
        var path = Path.Combine(RepoRoot, "schemas", "release-artifact-contract.json");
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
