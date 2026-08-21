using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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
    public void Contract_ReleaseAttestationRecordsSigstoreVerificationCommands()
    {
        var attestation = Contract.RootElement.GetProperty("attestationContract");
        var releaseAttestation = attestation.GetProperty("releaseAttestation");

        Assert.Equal("github-immutable-release", attestation.GetProperty("provider").GetString());
        Assert.Equal("release-attestation-generated-on-publication", attestation.GetProperty("status").GetString());
        Assert.Equal("github", releaseAttestation.GetProperty("provider").GetString());
        Assert.Equal("generated-on-immutable-publication", releaseAttestation.GetProperty("status").GetString());
        Assert.Equal("all-uploaded-release-assets", releaseAttestation.GetProperty("coveredAssets").GetString());
        Assert.Equal("not-covered", releaseAttestation.GetProperty("sourceArchives").GetString());
        Assert.Equal("gh release verify <tag>", releaseAttestation.GetProperty("verificationCommand").GetString());
        Assert.Equal(
            "gh release verify-asset <tag> <local-asset-path>",
            releaseAttestation.GetProperty("assetVerificationCommand").GetString());
        Assert.Equal(
            releaseAttestation.GetProperty("verificationCommand").GetString(),
            attestation.GetProperty("verificationCommand").GetString());
        Assert.Equal(
            releaseAttestation.GetProperty("assetVerificationCommand").GetString(),
            attestation.GetProperty("assetVerificationCommand").GetString());
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
        var required = new[] { "name", "channels", "required", "checksumEntry", "signingRequirement", "attestation", "packageRole", "runtimeIdentifier", "buildMode" };

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
    public void Contract_UsesUnsignedByDesignChecksumPolicy()
    {
        var signing = Contract.RootElement.GetProperty("signingContract");

        Assert.Equal("none", signing.GetProperty("provider").GetString());
        Assert.Equal("unsigned", signing.GetProperty("type").GetString());
        Assert.Equal("unsigned-by-design", signing.GetProperty("status").GetString());
        Assert.Empty(signing.GetProperty("conditionalOn").EnumerateArray());
        Assert.Empty(signing.GetProperty("signedAssets").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, signing.GetProperty("verificationCommand").ValueKind);
        Assert.Contains(
            "checksums.txt",
            signing.GetProperty("verificationEvidence").EnumerateArray().Select(value => value.GetString()));

        foreach (var artifact in Contract.RootElement.GetProperty("artifacts").EnumerateArray())
        {
            Assert.Equal("none", artifact.GetProperty("signingRequirement").GetString());
        }

        using var matrix = JsonDocument.Parse(ReadFile("schemas", "distribution-matrix.json"));
        foreach (var channel in matrix.RootElement.GetProperty("channels").EnumerateArray())
        {
            var channelName = channel.GetProperty("channel").GetString();
            var requirement = channel.GetProperty("signingRequirement").GetString()!;
            Assert.True(
                requirement.StartsWith("none", StringComparison.OrdinalIgnoreCase),
                $"Distribution channel '{channelName}' must use the checksum-first unsigned policy.");

            foreach (var decision in channel.GetProperty("blockingDecisions").EnumerateArray())
            {
                Assert.DoesNotContain("code signing", decision.GetString()!, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Authenticode", decision.GetString()!, StringComparison.OrdinalIgnoreCase);
            }
        }

        using var scorecard = JsonDocument.Parse(ReadFile("schemas", "scorecard-baseline.json"));
        var signedReleaseRisk = scorecard.RootElement
            .GetProperty("acceptedRisks")
            .EnumerateArray()
            .Single(risk => risk.GetProperty("check").GetString() == "Signed-Releases");

        Assert.Equal(-1, scorecard.RootElement.GetProperty("checkFloors").GetProperty("Signed-Releases").GetInt32());
        Assert.Contains("unsigned by design", signedReleaseRisk.GetProperty("reason").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending", signedReleaseRisk.GetProperty("reason").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SignPath enrollment", signedReleaseRisk.GetProperty("revisitWhen").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Contract_ReleaseManifestArtifactIsSelfReferentialMetadata()
    {
        var manifestArtifact = Contract.RootElement
            .GetProperty("artifacts").EnumerateArray()
            .Single(a => a.GetProperty("name").GetString() == "librespot-release-manifest.json");

        Assert.Equal("release-manifest", manifestArtifact.GetProperty("packageRole").GetString());
        Assert.Equal("any", manifestArtifact.GetProperty("runtimeIdentifier").GetString());
        Assert.Equal("metadata", manifestArtifact.GetProperty("buildMode").GetString());
        Assert.True(manifestArtifact.GetProperty("selfReferential").GetBoolean());
    }

    [Fact]
    public void BuildScripts_GeneratesReleaseManifestFromArtifactsAndChecksums()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LibreSpot.ReleaseManifest.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var coveredAssets = Contract.RootElement
                .GetProperty("checksumContract")
                .GetProperty("coveredAssets").EnumerateArray()
                .Select(v => v.GetString()!)
                .ToArray();

            foreach (var artifact in Contract.RootElement.GetProperty("artifacts").EnumerateArray())
            {
                var name = artifact.GetProperty("name").GetString()!;
                if (name == "librespot-release-manifest.json")
                    continue;

                File.WriteAllText(Path.Combine(tempRoot, name), $"test artifact: {name}", Encoding.UTF8);
            }

            File.WriteAllText(
                Path.Combine(tempRoot, "LibreSpot.sbom.cdx.json"),
                """
                {
                  "bomFormat": "CycloneDX",
                  "specVersion": "1.7",
                  "metadata": {
                    "tools": {
                      "components": [
                        { "type": "application", "name": "CycloneDX module for .NET", "version": "6.2.0.0" }
                      ]
                    }
                  },
                  "components": [
                    {
                      "name": "LibreSpot",
                      "version": "4.0.0-preview.6",
                      "hashes": [ { "alg": "SHA-256", "content": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" } ],
                      "licenses": [ { "license": { "id": "MIT" } } ]
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            var checksumLines = coveredAssets
                .Select(name => $"{Sha256File(Path.Combine(tempRoot, name))}  {name}");

            File.WriteAllLines(Path.Combine(tempRoot, "checksums.txt"), checksumLines, Encoding.ASCII);

            var process = StartPowerShell(
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                "-File", Path.Combine(RepoRoot, "Build-Scripts.ps1"),
                "-GenerateReleaseManifest",
                "-SkipStableExeIdentity",
                "-ReleaseRoot", tempRoot,
                "-ReleaseVersion", "4.0.0-preview.6",
                "-ReleaseChannel", "preview");

            Assert.Equal(0, process.ExitCode);

            var manifestPath = Path.Combine(tempRoot, "librespot-release-manifest.json");
            Assert.True(File.Exists(manifestPath), "Release manifest should be generated.");

            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = manifest.RootElement;

            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("4.0.0-preview.6", root.GetProperty("version").GetString());
            Assert.Equal("preview", root.GetProperty("channel").GetString());
            Assert.Equal("local", root.GetProperty("buildMode").GetString());

            var artifacts = root.GetProperty("artifacts").EnumerateArray().ToArray();
            Assert.Equal(Contract.RootElement.GetProperty("artifacts").GetArrayLength(), artifacts.Length);

            var cli = artifacts.Single(a => a.GetProperty("name").GetString() == "LibreSpot.Cli.exe");
            Assert.Equal("fleet-cli", cli.GetProperty("packageRole").GetString());
            Assert.Equal("win-x64", cli.GetProperty("runtimeIdentifier").GetString());
            Assert.Equal(Sha256File(Path.Combine(tempRoot, "LibreSpot.Cli.exe")), cli.GetProperty("sha256").GetString());
            Assert.True(cli.GetProperty("checksumVerified").GetBoolean());

            var self = artifacts.Single(a => a.GetProperty("name").GetString() == "librespot-release-manifest.json");
            Assert.True(self.GetProperty("selfReferential").GetBoolean());
            Assert.Equal(JsonValueKind.Null, self.GetProperty("sha256").ValueKind);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
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
        Assert.Contains("Get-LibreSpotBootstrapSha256", bootstrapSection);
        Assert.Contains("System.Security.Cryptography.SHA256", bootstrapSection);

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

    [Fact]
    public void ReleaseTrustDocs_DescribeLocalReleaseEvidenceOnly()
    {
        var docs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["README.md"] = ReadFile("README.md"),
            ["SECURITY.md"] = ReadFile("SECURITY.md"),
            ["SIGNPATH.md"] = ReadFile("SIGNPATH.md")
        };

        foreach (var (path, content) in docs)
        {
            Assert.DoesNotContain(".github/workflows", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("gh attestation verify", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("actions/attest", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SLSA", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".NET 8", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("GitHub-hosted runners", content, StringComparison.OrdinalIgnoreCase);

            Assert.Contains("checksums", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("gh release verify", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("gh release verify-asset", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "SignPath",
                content,
                StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("GitHub Actions build-provenance attestations are not produced", docs["README.md"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not currently track build, release, or Scorecard GitHub Actions workflows", docs["SECURITY.md"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GitHub Actions build-provenance attestations are not produced", docs["SIGNPATH.md"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LibreSpot.Cli.exe", docs["SIGNPATH.md"], StringComparison.Ordinal);
        Assert.Contains(".NET 10", docs["SIGNPATH.md"], StringComparison.OrdinalIgnoreCase);
    }

    private static JsonDocument LoadContract()
    {
        var path = Path.Combine(RepoRoot, "schemas", "release-artifact-contract.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string ReadFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray()));

    private static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static Process StartPowerShell(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start powershell.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Xunit.Sdk.XunitException($"PowerShell failed with exit code {process.ExitCode}.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        }

        return process;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

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
