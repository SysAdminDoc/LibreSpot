using Xunit;

namespace LibreSpot.Desktop.Tests;

[CollectionDefinition("WPF UI automation", DisableParallelization = true)]
public sealed class WpfUiAutomationCollection
{
    public const string Name = "WPF UI automation";
}
