using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class DesktopCancellationRegressionTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate LibreSpot.ps1 from the test runner.");
    }

    private static string ReadFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray()));

    [Fact]
    public void MainWindowClosing_AllowsCloseAfterCancelWasAlreadyRequested()
    {
        var source = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml.cs");
        var method = Regex.Match(
            source,
            @"private\s+void\s+MainWindow_Closing\s*\(.+?^\s*private\s+void\s+MainWindow_Closed",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(method.Success, "MainWindow_Closing method not found.");
        var body = method.Value;
        var alreadyCancelIndex = body.IndexOf("_viewModel.IsRunning && _viewModel.IsCancelRequested", StringComparison.Ordinal);
        var promptIndex = body.IndexOf("PresentCloseWhileRunningPrompt", StringComparison.Ordinal);

        Assert.True(alreadyCancelIndex >= 0, "Closing must special-case an already requested cancel.");
        Assert.True(promptIndex > alreadyCancelIndex, "A second close after cancel should not reopen the destructive prompt.");
        Assert.Contains("_allowCloseWhileRunning = true;", body);
    }

    [Fact]
    public void CancelRunningBackend_SetsVisibleStoppingStateBeforeCancelingToken()
    {
        var source = ReadFile("src", "LibreSpot.Desktop", "ViewModels", "MainViewModel.cs");
        var method = Regex.Match(
            source,
            @"public\s+void\s+CancelRunningBackend\s*\(\)\s*\{(?<body>.+?)^\s*public\s+void\s+Dispose",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(method.Success, "CancelRunningBackend method not found.");
        var body = method.Groups["body"].Value;
        var stateIndex = body.IndexOf("IsCancelRequested = true;", StringComparison.Ordinal);
        var cancelIndex = body.IndexOf("_runCts?.Cancel()", StringComparison.Ordinal);

        Assert.True(stateIndex >= 0, "Cancellation must update visible state.");
        Assert.True(cancelIndex > stateIndex, "Visible stopping state should be set before the process tree cancel request.");
        Assert.Contains("ActivityStatus = Strings.StoppingBackend;", body);
        Assert.Contains("ActivityStep = L(\"Vm_CancelRequested\");", body);
    }

    [Fact]
    public void CancelRunCommand_UsesSharedBackendCancellationPathImmediately()
    {
        var source = ReadFile("src", "LibreSpot.Desktop", "ViewModels", "MainViewModel.cs");

        Assert.Contains("CancelRunCommand = new RelayCommand(CancelRunningBackend, () => IsRunning && !IsCancelRequested);", source);
        Assert.DoesNotContain("PresentCancelRunPrompt", source);
    }
}
