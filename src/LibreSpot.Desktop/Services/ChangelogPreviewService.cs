using System.IO;
using System.Reflection;

namespace LibreSpot.Desktop.Services;

public static class ChangelogPreviewService
{
    private const string ResourceName = "LibreSpot.Desktop.Docs.CHANGELOG.md";

    public static IReadOnlyList<string> LoadUnreleasedHighlights(int maxItems = 6)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return ["Changelog is not embedded in this build."];
        }

        using var reader = new StreamReader(stream);
        var lines = reader.ReadToEnd().Split(["\r\n", "\n"], StringSplitOptions.None);
        var highlights = new List<string>();
        var inUnreleased = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (inUnreleased)
                {
                    break;
                }

                inUnreleased = line.Equals("## [Unreleased]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inUnreleased || !line.StartsWith("- ", StringComparison.Ordinal))
            {
                continue;
            }

            highlights.Add(line[2..].Trim());
            if (highlights.Count >= maxItems)
            {
                break;
            }
        }

        return highlights.Count == 0
            ? ["No unreleased changelog entries are embedded in this build."]
            : highlights;
    }
}
