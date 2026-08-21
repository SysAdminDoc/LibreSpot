using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace LibreSpot.Desktop.Tests;

/// <summary>
/// Disabled states must mute the foreground, never composite the whole control.
/// A root Opacity multiplies whatever brush the content already chose, which
/// pushes captions under the 3:1 floor in the dark palette and dims GrayText a
/// second time in high contrast.
/// </summary>
public sealed class DisabledStateContrastTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    // <Trigger Property="IsEnabled" Value="False"> ... </Trigger>, including the
    // MultiTrigger and DataTrigger spellings, up to the closing tag.
    private static readonly Regex DisabledTriggerPattern = new(
        @"<(?<tag>Multi)?(?:Data)?Trigger\b[^>]*?Property=""IsEnabled""[^>]*?Value=""False""[^>]*?>(?<body>.*?)</\k<tag>?(?:Data)?Trigger>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex OpacitySetterPattern = new(
        @"<Setter\b[^>]*?Property=""Opacity""[^>]*?>",
        RegexOptions.Compiled);

    [Fact]
    public void DisabledTriggers_MuteTheForegroundInsteadOfCompositingTheControl()
    {
        var xamlRoot = Path.Combine(RepoRoot, "src", "LibreSpot.Desktop");
        var offenders = new List<string>();
        var inspected = 0;

        foreach (var file in Directory.EnumerateFiles(xamlRoot, "*.xaml", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            foreach (Match trigger in DisabledTriggerPattern.Matches(content))
            {
                inspected++;
                var body = trigger.Groups["body"].Value;
                foreach (Match setter in OpacitySetterPattern.Matches(body))
                {
                    // A named TargetName is a specific decoration (a focus ring,
                    // a sheen), not the whole control.
                    if (setter.Value.Contains("TargetName=", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    offenders.Add(
                        $"{ToRelativePath(file)}: a disabled trigger sets {setter.Value.Trim()} on the control root. " +
                        "Set Foreground to DisabledTextBrush instead.");
                }
            }
        }

        Assert.True(inspected > 0, "The disabled-trigger scan matched nothing; the pattern is broken.");
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void DisabledStateLint_DetectsARootOpacityDim()
    {
        const string xaml = """
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.45" />
            </Trigger>
            """;

        var trigger = DisabledTriggerPattern.Match(xaml);

        Assert.True(trigger.Success, "The disabled-trigger pattern must match a plain Trigger.");
        var setters = OpacitySetterPattern.Matches(trigger.Groups["body"].Value);
        Assert.Single(setters);
        Assert.DoesNotContain("TargetName=", setters[0].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void DisabledStateLint_AllowsFadingANamedDecoration()
    {
        const string xaml = """
            <Trigger Property="IsEnabled" Value="False">
              <Setter TargetName="HoverTint" Property="Opacity" Value="0" />
            </Trigger>
            """;

        var trigger = DisabledTriggerPattern.Match(xaml);

        Assert.True(trigger.Success);
        var setters = OpacitySetterPattern.Matches(trigger.Groups["body"].Value);
        Assert.Single(setters);
        Assert.Contains("TargetName=", setters[0].Value, StringComparison.Ordinal);
    }

    private static string ToRelativePath(string fullPath) =>
        Path.GetRelativePath(RepoRoot, fullPath).Replace('\\', '/');

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
