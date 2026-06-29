using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class WindowsShellIntegrationTests
{
    [Fact]
    public void Activation_ParsesDirectProfileUri()
    {
        var activation = ShellActivationService.Parse(["librespot://profile?data=abc"]);

        Assert.Equal(ShellActivationKind.ProfileShareUri, activation.Kind);
        Assert.Equal("librespot://profile?data=abc", activation.Value);
    }

    [Fact]
    public void Activation_ParsesProfileFileArgument()
    {
        var activation = ShellActivationService.Parse(["--profile-file", @"C:\Profiles\desk.librespot"]);

        Assert.Equal(ShellActivationKind.ProfileShareUri, activation.Kind);
        Assert.StartsWith("librespot://profile?file=", activation.Value);
        Assert.Contains("desk.librespot", Uri.UnescapeDataString(activation.Value!));
    }

    [Theory]
    [InlineData("--shell-action=recommended", ShellActivationKind.NavigateRecommended)]
    [InlineData("--shell-action=custom", ShellActivationKind.NavigateCustom)]
    [InlineData("--shell-action=maintenance", ShellActivationKind.NavigateMaintenance)]
    [InlineData("--shell-action=import-profile", ShellActivationKind.ImportProfile)]
    [InlineData("--shell-action=open-folder", ShellActivationKind.OpenLibreSpotFolder)]
    public void Activation_ParsesJumpListShellActions(string argument, ShellActivationKind expected)
    {
        var activation = ShellActivationService.Parse([argument]);

        Assert.Equal(expected, activation.Kind);
    }

    [Fact]
    public void RegistrationPlan_RegistersProtocolAndProfileAssociation()
    {
        var plan = ShellIntegrationService.BuildRegistrationPlan(@"C:\Tools\LibreSpot\LibreSpot.exe");

        Assert.Contains(plan, entry =>
            entry.KeyPath == @"Software\Classes\librespot" &&
            entry.ValueName == "URL Protocol");
        Assert.Contains(plan, entry =>
            entry.KeyPath == @"Software\Classes\librespot\shell\open\command" &&
            entry.Value == "\"C:\\Tools\\LibreSpot\\LibreSpot.exe\" \"%1\"");
        Assert.Contains(plan, entry =>
            entry.KeyPath == @"Software\Classes\.librespot" &&
            entry.ValueName == string.Empty &&
            entry.Value == ShellIntegrationService.ProfileProgId);
        Assert.Contains(plan, entry =>
            entry.KeyPath == @"Software\Classes\LibreSpot.Profile\shell\open\command" &&
            entry.Value == "\"C:\\Tools\\LibreSpot\\LibreSpot.exe\" --profile-file \"%1\"");
    }

    [Fact]
    public void JumpListTasks_MapToStartupActionsWithoutMutation()
    {
        var tasks = ShellIntegrationService.BuildJumpTaskDefinitions();

        Assert.Contains(tasks, task => task.Title == "Recommended setup" && task.Arguments == "--shell-action=recommended");
        Assert.Contains(tasks, task => task.Title == "Custom settings" && task.Arguments == "--shell-action=custom");
        Assert.Contains(tasks, task => task.Title == "Maintenance" && task.Arguments == "--shell-action=maintenance");
        Assert.Contains(tasks, task => task.Title == "Import profile" && task.Arguments == "--shell-action=import-profile");
        Assert.Contains(tasks, task => task.Title == "Open LibreSpot folder" && task.Arguments == "--shell-action=open-folder");
    }
}
