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
        var inNewestSection = false;

        // Read the newest changelog section that actually has content — the
        // first top-level "## " heading whose bullets are non-empty, whether
        // that is [Unreleased] or the latest dated release. An empty leading
        // "## [Unreleased]" section (the normal state right after a release)
        // is skipped rather than kept as the newest section, which would blank
        // the in-app "what's new" preview. "### " subsections don't end a section.
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal) && !line.StartsWith("### ", StringComparison.Ordinal))
            {
                if (highlights.Count > 0)
                {
                    break;
                }

                inNewestSection = true;
                continue;
            }

            if (!inNewestSection || !line.StartsWith("- ", StringComparison.Ordinal))
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
            ? ["No changelog entries are embedded in this build."]
            : highlights;
    }
}
