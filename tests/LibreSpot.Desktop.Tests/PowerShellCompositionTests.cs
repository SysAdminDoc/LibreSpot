using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class PowerShellCompositionTests
{
    [Fact]
    public void CompositionContract_OwnsEveryCanonicalSourceSet()
    {
        using var document = JsonDocument.Parse(ReadRepoFile("src", "powershell", "composition.json"));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            ["dataBlocks", "sharedFunctions", "laneFunctions"],
            root.GetProperty("componentOrder").EnumerateArray().Select(item => item.GetString()!).ToArray());

        var shared = root.GetProperty("sharedFunctions");
        var sharedDirectory = Path.Combine(RepoRoot, shared.GetProperty("directory").GetString()!.Replace('/', Path.DirectorySeparatorChar));
        var sharedFiles = Directory.GetFiles(sharedDirectory, shared.GetProperty("pattern").GetString()!);
        Assert.Equal(shared.GetProperty("expectedCount").GetInt32(), sharedFiles.Length);
        Assert.Equal(108, sharedFiles.Length);

        var expectedLaneNames = root.GetProperty("laneFunctions")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(13, expectedLaneNames.Length);
        foreach (var host in root.GetProperty("hosts").EnumerateArray())
        {
            var laneSource = ReadRepoFile(host.GetProperty("laneSource").GetString()!.Split('/'));
            var actualNames = Regex.Matches(laneSource, @"(?m)^function\s+([A-Za-z0-9_-]+)")
                .Select(match => match.Groups[1].Value)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(expectedLaneNames, actualNames);
        }

        var legacyDirectory = Path.Combine(RepoRoot, "src", "powershell", "lane-specific");
        Assert.True(!Directory.Exists(legacyDirectory) || !Directory.EnumerateFiles(legacyDirectory, "*.ps1").Any());

        var buildScript = ReadRepoFile("Build-Scripts.ps1");
        Assert.Contains("if ($GenerateReleaseManifest)", buildScript, StringComparison.Ordinal);
        Assert.Matches(@"(?s)if \(\$GenerateReleaseManifest\).*?Test-LibreSpotHostComposition -Smoke.*?New-LibreSpotReleaseManifest", buildScript);
        Assert.Contains("The separate sync commands are retired", buildScript, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompositionSmoke_ByteChecksAndImportsOnWindowsPowerShellAndPwsh()
    {
        var result = await RunBuildScriptAsync("-CompositionSmoke");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("composition byte-check passed", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("powershell import/parse smoke passed for main", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pwsh import/parse smoke passed for backend", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComposeHosts_IsDeterministicAndMatchesCheckedInHosts()
    {
        using var fixture = new TemporaryDirectory();
        var firstRoot = Path.Combine(fixture.Path, "first");
        var secondRoot = Path.Combine(fixture.Path, "second");

        var first = await RunBuildScriptAsync("-ComposeHosts", "-CompositionOutputRoot", firstRoot);
        var second = await RunBuildScriptAsync("-ComposeHosts", "-CompositionOutputRoot", secondRoot);
        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);

        foreach (var relativePath in new[]
                 {
                     "LibreSpot.ps1",
                     Path.Combine("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1")
                 })
        {
            var checkedIn = File.ReadAllBytes(Path.Combine(RepoRoot, relativePath));
            var firstBytes = File.ReadAllBytes(Path.Combine(firstRoot, relativePath));
            var secondBytes = File.ReadAllBytes(Path.Combine(secondRoot, relativePath));
            Assert.Equal(checkedIn, firstBytes);
            Assert.Equal(firstBytes, secondBytes);
        }
    }

    [Theory]
    [InlineData("order")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public async Task CompositionContract_RejectsInvalidOrderingMissingModulesAndDuplicateExports(string mutation)
    {
        using var fixture = new TemporaryDirectory();
        var contract = JsonNode.Parse(ReadRepoFile("src", "powershell", "composition.json"))!.AsObject();
        switch (mutation)
        {
            case "order":
                contract["componentOrder"] = new JsonArray("sharedFunctions", "dataBlocks", "laneFunctions");
                break;
            case "missing":
                contract["sharedFunctions"]!["expectedCount"] = 106;
                break;
            case "duplicate":
                contract["laneFunctions"]!.AsArray().Add("Write-Log");
                break;
        }

        var path = Path.Combine(fixture.Path, mutation + ".json");
        File.WriteAllText(path, contract.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var result = await RunBuildScriptAsync("-CompositionSmoke", "-CompositionContractPath", path);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            mutation switch
            {
                "order" => "Invalid composition order",
                "missing" => "expected 106 shared modules",
                _ => "Duplicate lane function export"
            },
            result.Output,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompositionSmoke_RejectsAStaleManagedFunction()
    {
        using var fixture = new TemporaryDirectory();
        var mainPath = Path.Combine(fixture.Path, "LibreSpot.ps1");
        var backendPath = Path.Combine(fixture.Path, "LibreSpot.Backend.ps1");
        File.Copy(Path.Combine(RepoRoot, "LibreSpot.ps1"), mainPath);
        File.Copy(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1"), backendPath);
        var main = File.ReadAllText(mainPath);
        main = new Regex(@"(?m)^(function Get-FileSha256Lower\s*\{)").Replace(
            main,
            "$1\r\n    # composition-stale-canary",
            1);
        File.WriteAllText(mainPath, main, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var contract = JsonNode.Parse(ReadRepoFile("src", "powershell", "composition.json"))!.AsObject();
        var hosts = contract["hosts"]!.AsArray();
        hosts[0]!["target"] = mainPath;
        hosts[1]!["target"] = backendPath;
        var contractPath = Path.Combine(fixture.Path, "stale.json");
        File.WriteAllText(contractPath, contract.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var result = await RunBuildScriptAsync("-CompositionSmoke", "-CompositionContractPath", contractPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("host(s) are stale", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ProcessResult> RunBuildScriptAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = RepoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-NoLogo");
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(Path.Combine(RepoRoot, "Build-Scripts.ps1"));
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start pwsh.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await process.WaitForExitAsync(timeout.Token);
        return new ProcessResult(process.ExitCode, (await stdout) + "\n" + (await stderr));
    }

    private static string ReadRepoFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray()));

    private static string RepoRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
            {
                directory = directory.Parent;
            }
            return directory?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
        }
    }

    private sealed record ProcessResult(int ExitCode, string Output);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LibreSpot.Composition.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
