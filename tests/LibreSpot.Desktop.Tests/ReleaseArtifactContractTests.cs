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
    public void Contract_AllRequiredArtifactsAreReleaseCovered()
    {
        foreach (var artifact in Contract.RootElement.GetProperty("artifacts").EnumerateArray())
        {
            if (!artifact.GetProperty("required").GetBoolean())
                continue;

            var name = artifact.GetProperty("name").GetString()!;
            var hasChecksumEntry = artifact.GetProperty("checksumEntry").GetBoolean();
            var isReleaseMetadata = name is "checksums.txt" or "librespot-release-manifest.json";
            var distributionChannels = artifact.GetProperty("distributionChannels").EnumerateArray().ToArray();

            Assert.NotEmpty(distributionChannels);
            Assert.True(
                hasChecksumEntry || isReleaseMetadata,
                $"Required artifact '{name}' must either be checksum-covered or be release metadata.");
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
    public void Contract_AttestationSubjectsMatchArtifactAttestationFields()
    {
        var buildProvenanceSubjects = Contract.RootElement
            .GetProperty("attestationContract")
            .GetProperty("buildProvenance")
            .GetProperty("subjects").EnumerateArray()
            .Select(v => v.GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var sbomSubject = Contract.RootElement
            .GetProperty("attestationContract")
            .GetProperty("sbom")
            .GetProperty("subject").GetString()!;

        foreach (var artifact in Contract.RootElement.GetProperty("artifacts").EnumerateArray())
        {
            var name = artifact.GetProperty("name").GetString()!;
            var attestation = artifact.GetProperty("attestation").GetString();

            if (attestation == "build-provenance")
            {
                Assert.Contains(name, buildProvenanceSubjects);
            }
            else
            {
                Assert.DoesNotContain(name, buildProvenanceSubjects);
            }
        }

        Assert.True(
            Contract.RootElement.GetProperty("artifacts").EnumerateArray()
                .Any(a => a.GetProperty("name").GetString() == sbomSubject),
            $"SBOM subject '{sbomSubject}' is not a known release artifact.");
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
    public void Contract_TagPatternsDistinguishStablePreviewAndRc()
    {
        var patterns = Contract.RootElement.GetProperty("tagPatterns");
        var stable = new Regex(patterns.GetProperty("stable").GetString()!);
        var preview = new Regex(patterns.GetProperty("preview").GetString()!);
        var rc = new Regex(patterns.GetProperty("rc").GetString()!);

        Assert.Matches(stable, "v4.0.0");
        Assert.DoesNotMatch(stable, "v4.0.0-preview.6");
        Assert.DoesNotMatch(stable, "v4.0.0-rc.1");

        Assert.Matches(preview, "v4.0.0-preview.6");
        Assert.DoesNotMatch(preview, "v4.0.0");
        Assert.DoesNotMatch(preview, "v4.0.0-rc.1");

        Assert.Matches(rc, "v4.0.0-rc.1");
        Assert.DoesNotMatch(rc, "v4.0.0");
        Assert.DoesNotMatch(rc, "v4.0.0-preview.6");
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
    public void Contract_PostUploadAuditIsManualLocal()
    {
        var status = Contract.RootElement
            .GetProperty("postUploadAudit")
            .GetProperty("implementationStatus").GetString();

        Assert.Equal("manual-local", status);
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

    [Fact]
    public void Readme_BootstrapReferencesValidReleaseAssets()
    {
        var readme = ReadFile("README.md");
        var bootstrapSection = readme.Split("## Quick Start")[1].Split("##")[0];

        Assert.Contains("checksums.txt", bootstrapSection);
        Assert.Contains("LibreSpot.ps1", bootstrapSection);
        Assert.Contains("SHA256", bootstrapSection);
        Assert.Contains("Get-FileHash", bootstrapSection);

        Assert.True(
            bootstrapSection.Contains("mismatch") || bootstrapSection.Contains("Mismatch"),
            "Bootstrap must fail on checksum mismatch.");

        Assert.True(
            bootstrapSection.Contains("Remove-Item") || bootstrapSection.Contains("remove"),
            "Bootstrap should remove the script on checksum failure.");
    }

    [Fact]
    public void Readme_BootstrapDownloadsBeforeExecution()
    {
        var readme = ReadFile("README.md");
        var bootstrapSection = readme.Split("## Quick Start")[1].Split("##")[0];

        var downloadIndex = bootstrapSection.IndexOf("Invoke-WebRequest", StringComparison.Ordinal);
        var executeIndex = bootstrapSection.IndexOf("& \"$d\\LibreSpot.ps1\"", StringComparison.Ordinal);

        Assert.True(downloadIndex >= 0, "Bootstrap must use Invoke-WebRequest to download.");
        Assert.True(executeIndex >= 0, "Bootstrap must execute the saved script.");
        Assert.True(downloadIndex < executeIndex, "Bootstrap must download before executing.");
    }

    [Fact]
    public void Readme_KeepsLowerTrustPipelinePathLabeled()
    {
        var readme = ReadFile("README.md");

        Assert.Contains("irm", readme);
        Assert.Contains("iex", readme);
        Assert.Contains("lower trust", readme, StringComparison.OrdinalIgnoreCase);
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
