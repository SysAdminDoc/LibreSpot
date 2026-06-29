using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class CustomPatchServiceTests
{
    [Fact]
    public void Validate_AcceptsSpotXStyleMatchReplaceJson()
    {
        var service = new CustomPatchService();
        const string json = """
        {
          "xpui": {
            "ads": {
              "match": ["adsEnabled\\s*:\\s*true"],
              "replace": ["adsEnabled:false"]
            }
          }
        }
        """;

        var result = service.Validate(json, enabled: true);

        Assert.True(result.IsValid);
        Assert.Equal(1, result.PatchGroupCount);
        Assert.Equal(1, result.PatternCount);
        Assert.Equal(1, result.ReplacementCount);
    }

    [Fact]
    public void Validate_BlocksInvalidRegex()
    {
        var service = new CustomPatchService();
        const string json = """
        {
          "xpui": {
            "match": ["("],
            "replace": ["noop"]
          }
        }
        """;

        var result = service.Validate(json, enabled: true);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("valid .NET regex", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_BlocksMismatchedMatchReplaceArrays()
    {
        var service = new CustomPatchService();
        const string json = """
        {
          "xpui": {
            "match": ["one", "two"],
            "replace": ["one"]
          }
        }
        """;

        var result = service.Validate(json, enabled: true);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("match value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_BlocksOversizedJson()
    {
        var service = new CustomPatchService();
        var json = "{\"xpui\":{\"match\":\"" + new string('a', CustomPatchService.MaxPatchJsonBytes) + "\",\"replace\":\"noop\"}}";

        var result = service.Validate(json, enabled: true);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("limited to 64 KB", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Format_PrettyPrintsValidJson()
    {
        var service = new CustomPatchService();

        var formatted = service.Format("{\"xpui\":{\"match\":\"one\",\"replace\":\"two\"}}");

        Assert.Contains(Environment.NewLine, formatted);
        Assert.Contains("\"xpui\"", formatted);
    }
}
