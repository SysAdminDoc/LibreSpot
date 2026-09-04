using System.Globalization;
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

        Assert.Equal(ShellActivationKind.ProfileFile, activation.Kind);
        Assert.Equal(@"C:\Profiles\desk.librespot", activation.Value);
    }

    [Theory]
    [InlineData(@"--profile-file=C:\Profiles\desk.librespot")]
    [InlineData(@"C:\Profiles\desk.librespot")]
    public void Activation_RecognizesExplorerProfileFileForms(string argument)
    {
        var activation = ShellActivationService.Parse([argument]);

        Assert.Equal(ShellActivationKind.ProfileFile, activation.Kind);
        Assert.Equal(@"C:\Profiles\desk.librespot", activation.Value);
    }

    [Fact]
    public void Activation_KeepsProtocolFileSourceInRestrictedUriPath()
    {
        const string uri = "librespot://profile?file=C%3A%5CProfiles%5Cdesk.librespot";

        var activation = ShellActivationService.Parse([uri]);

        Assert.Equal(ShellActivationKind.ProfileShareUri, activation.Kind);
        Assert.Equal(uri, activation.Value);
    }

    [Fact]
    public void Activation_ParsesStoreSelectionUri()
    {
        const string uri = "librespot://store?kind=theme&id=Catppuccin&scheme=Mocha";

        var activation = ShellActivationService.Parse([uri]);

        Assert.Equal(ShellActivationKind.StoreSelection, activation.Kind);
        Assert.Equal(uri, activation.Value);
    }

    [Theory]
    [InlineData("librespot://store?kind=theme&id=BurntSienna&scheme=Burnt%20Sienna", StoreAssetKind.Theme, "BurntSienna", "Burnt Sienna")]
    [InlineData("librespot://store?kind=extension&id=shuffle%2B.js", StoreAssetKind.Extension, "shuffle+.js", null)]
    [InlineData("librespot://store?kind=app&id=stats", StoreAssetKind.App, "stats", null)]
    public void StoreSelection_DecodesSupportedItems(string uri, StoreAssetKind kind, string id, string? scheme)
    {
        var parsed = StoreSelectionService.TryParse(uri, out var request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal(kind, request.Kind);
        Assert.Equal(id, request.Id);
        Assert.Equal(scheme, request.Scheme);
    }

    [Theory]
    [InlineData("librespot://profile?data=abc")]
    [InlineData("https://example.com/?kind=theme&id=Prism")]
    [InlineData("librespot://store?kind=unknown&id=Prism")]
    [InlineData("librespot://store?kind=theme&id=")]
    [InlineData("librespot://store?kind=app&id=stats&scheme=Dark")]
    [InlineData("librespot://store?kind=theme&id=Prism&id=Compact")]
    [InlineData("librespot://store/path?kind=theme&id=Prism")]
    public void StoreSelection_RejectsMalformedOrUntrustedUris(string uri)
    {
        Assert.False(StoreSelectionService.TryParse(uri, out var request));
        Assert.Null(request);
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
    public void Activation_RejectsObsoleteElevatedResumeAction()
    {
        var activation = ShellActivationService.Parse(["--shell-action=resume-install"]);

        Assert.Equal(ShellActivationKind.None, activation.Kind);
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

        Assert.Contains(tasks, task => task.Title == "Home" && task.Arguments == "--shell-action=recommended");
        Assert.Contains(tasks, task => task.Title == "Settings" && task.Arguments == "--shell-action=custom");
        Assert.Contains(tasks, task => task.Title == "Maintenance" && task.Arguments == "--shell-action=maintenance");
        Assert.Contains(tasks, task => task.Title == "Home" && task.Description == "Check this PC and apply the supported setup.");
        Assert.Contains(tasks, task => task.Title == "Settings" && task.Description == "Choose themes, extensions, and install options.");
        Assert.Contains(tasks, task => task.Title == "Import profile" && task.Arguments == "--shell-action=import-profile");
        Assert.Contains(tasks, task => task.Title == "Open LibreSpot folder" && task.Arguments == "--shell-action=open-folder");
    }

    [Fact]
    public void ShellLabels_UseRequestedCultureForPersistedShellCopy()
    {
        var culture = CultureInfo.GetCultureInfo("es");
        var registration = ShellIntegrationService.BuildRegistrationPlan(
            @"C:\Tools\LibreSpot\LibreSpot.exe",
            culture);
        var tasks = ShellIntegrationService.BuildJumpTaskDefinitions(culture);

        Assert.Contains(registration, entry => entry.Value == "URL:enlace de perfil de LibreSpot");
        Assert.Contains(registration, entry => entry.Value == "Perfil de LibreSpot");
        Assert.Contains(tasks, task => task.Title == "Inicio" && task.Arguments == "--shell-action=recommended");
        Assert.Contains(tasks, task => task.Title == "Configuración" && task.Arguments == "--shell-action=custom");
        Assert.Contains(tasks, task => task.Title == "Mantenimiento" && task.Arguments == "--shell-action=maintenance");
        Assert.Contains(tasks, task => task.Title == "Inicio" && task.Description == "Comprueba este equipo y aplica la configuración compatible.");
        Assert.Contains(tasks, task => task.Title == "Configuración" && task.Description == "Elige temas, extensiones y opciones de instalación.");
    }
}
