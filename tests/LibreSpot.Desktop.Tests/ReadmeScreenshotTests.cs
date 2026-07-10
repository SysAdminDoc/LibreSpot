using System.IO;
using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ReadmeScreenshotTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    private static readonly IReadOnlyDictionary<string, string> ExpectedWpfScreenshots =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["assets/screenshots/wpf-recommended.png"] = "recommended",
            ["assets/screenshots/wpf-custom.png"] = "custom",
            ["assets/screenshots/wpf-maintenance.png"] = "maintenance",
            ["assets/screenshots/wpf-activity-undo.png"] = "activity-undo"
        };

    [Fact]
    public void ReadmeWpfScreenshotsCarryCurrentShellVersionMetadata()
    {
        var readme = ReadText("README.md");
        var referencedScreenshots = Regex
            .Matches(readme, @"assets/screenshots/(?<file>wpf-[^""]+\.png)")
            .Select(match => $"assets/screenshots/{match.Groups["file"].Value}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var expectedShellVersion = GetShellDisplayVersion();
        var expectedAssemblyVersion = GetProjectInformationalVersion();
        var offenders = new List<string>();

        foreach (var (relativePath, expectedState) in ExpectedWpfScreenshots)
        {
            if (!referencedScreenshots.Contains(relativePath))
            {
                offenders.Add($"{relativePath}: README does not reference this WPF screenshot.");
                continue;
            }

            var fullPath = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                offenders.Add($"{relativePath}: screenshot file is missing.");
                continue;
            }

            AssertPngVisualContent(relativePath, fullPath, offenders);
            var metadata = ReadPngTextMetadata(fullPath);
            AssertMetadata(relativePath, "LibreSpotShellVersion", expectedShellVersion, metadata, offenders);
            AssertMetadata(relativePath, "LibreSpotCaptureAssemblyVersion", expectedAssemblyVersion, metadata, offenders);
            AssertMetadata(relativePath, "LibreSpotCaptureState", expectedState, metadata, offenders);
            if (!metadata.TryGetValue("LibreSpotCaptureUtc", out var capturedAt) ||
                !DateTimeOffset.TryParse(capturedAt, out _))
            {
                offenders.Add($"{relativePath}: LibreSpotCaptureUtc metadata is missing or invalid.");
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    private static void AssertPngVisualContent(string relativePath, string fullPath, ICollection<string> offenders)
    {
        var png = File.ReadAllBytes(fullPath);
        if (png.Length < 100_000)
        {
            offenders.Add($"{relativePath}: PNG is unexpectedly small ({png.Length:N0} bytes) and may not contain the rendered shell.");
            return;
        }

        if (png.Length < 24 || !Encoding.ASCII.GetString(png, 12, 4).Equals("IHDR", StringComparison.Ordinal))
        {
            offenders.Add($"{relativePath}: PNG does not contain a valid IHDR header.");
            return;
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        if (width < 1_000 || height < 700)
        {
            offenders.Add($"{relativePath}: rendered size is {width}x{height}, expected at least 1000x700.");
        }
    }

    private static void AssertMetadata(
        string relativePath,
        string key,
        string expected,
        IReadOnlyDictionary<string, string> metadata,
        ICollection<string> offenders)
    {
        if (!metadata.TryGetValue(key, out var actual))
        {
            offenders.Add($"{relativePath}: missing PNG metadata '{key}' (expected '{expected}').");
            return;
        }

        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            offenders.Add($"{relativePath}: metadata '{key}' is '{actual}', expected '{expected}'.");
        }
    }

    private static Dictionary<string, string> ReadPngTextMetadata(string path)
    {
        var png = File.ReadAllBytes(path);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (png.Length < signature.Length || !png.AsSpan(0, signature.Length).SequenceEqual(signature))
        {
            return values;
        }

        var offset = signature.Length;
        while (offset + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            if (length < 0 || offset + 12 + length > png.Length)
            {
                return values;
            }

            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            if (string.Equals(type, "tEXt", StringComparison.Ordinal))
            {
                AddTextChunk(values, png.AsSpan(offset + 8, length));
            }

            if (string.Equals(type, "IEND", StringComparison.Ordinal))
            {
                break;
            }

            offset += 12 + length;
        }

        return values;
    }

    private static void AddTextChunk(IDictionary<string, string> values, ReadOnlySpan<byte> data)
    {
        var split = data.IndexOf((byte)0);
        if (split <= 0)
        {
            return;
        }

        var key = Encoding.ASCII.GetString(data[..split]);
        var value = Encoding.ASCII.GetString(data[(split + 1)..]);
        values[key] = value;
    }

    private static string GetShellDisplayVersion()
    {
        var source = ReadText("src", "LibreSpot.Desktop", "ViewModels", "MainViewModel.cs");
        var match = Regex.Match(source, @"ShellDisplayVersion\s*=>\s*""(?<version>v[^""]+)""");
        Assert.True(match.Success, "Could not find MainViewModel.ShellDisplayVersion.");
        return match.Groups["version"].Value;
    }

    private static string GetProjectInformationalVersion()
    {
        var document = XDocument.Load(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "LibreSpot.Desktop.csproj"));
        var version = document.Descendants("InformationalVersion")
            .Select(element => element.Value.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        Assert.False(string.IsNullOrWhiteSpace(version), "LibreSpot.Desktop.csproj must define InformationalVersion.");
        return version!;
    }

    private static string ReadText(params string[] relativeParts) =>
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
