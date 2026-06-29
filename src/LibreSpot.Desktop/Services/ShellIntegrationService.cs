using System.IO;
using System.Windows;
using System.Windows.Shell;
using Microsoft.Win32;
using Application = System.Windows.Application;

namespace LibreSpot.Desktop.Services;

public sealed record ShellRegistryValue(string KeyPath, string ValueName, string Value);

public sealed record ShellJumpTaskDefinition(string Title, string Description, string Arguments);

public static class ShellIntegrationService
{
    public const string ProtocolScheme = "librespot";
    public const string ProfileProgId = "LibreSpot.Profile";
    public const string ProfileContentType = "application/vnd.librespot.profile+json";

    public static string? TryGetCurrentExecutablePath()
    {
        if (Environment.GetCommandLineArgs().Any(arg => arg.StartsWith("--uia-smoke=", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        var fileName = Path.GetFileName(executablePath);
        return string.Equals(fileName, "dotnet.exe", StringComparison.OrdinalIgnoreCase)
            ? null
            : executablePath;
    }

    public static IReadOnlyList<ShellRegistryValue> BuildRegistrationPlan(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }

        var fullPath = Path.GetFullPath(executablePath);
        var iconPath = $"{fullPath},0";
        return
        [
            new ShellRegistryValue($@"Software\Classes\{ProtocolScheme}", string.Empty, "URL:LibreSpot profile link"),
            new ShellRegistryValue($@"Software\Classes\{ProtocolScheme}", "URL Protocol", string.Empty),
            new ShellRegistryValue($@"Software\Classes\{ProtocolScheme}\DefaultIcon", string.Empty, iconPath),
            new ShellRegistryValue($@"Software\Classes\{ProtocolScheme}\shell\open\command", string.Empty, $"{Quote(fullPath)} \"%1\""),

            new ShellRegistryValue($@"Software\Classes\{ShellActivationService.ProfileExtension}", string.Empty, ProfileProgId),
            new ShellRegistryValue($@"Software\Classes\{ShellActivationService.ProfileExtension}", "Content Type", ProfileContentType),
            new ShellRegistryValue($@"Software\Classes\{ShellActivationService.ProfileExtension}", "PerceivedType", "document"),
            new ShellRegistryValue($@"Software\Classes\{ShellActivationService.ProfileExtension}\OpenWithProgids", ProfileProgId, string.Empty),

            new ShellRegistryValue($@"Software\Classes\{ProfileProgId}", string.Empty, "LibreSpot profile"),
            new ShellRegistryValue($@"Software\Classes\{ProfileProgId}\DefaultIcon", string.Empty, iconPath),
            new ShellRegistryValue($@"Software\Classes\{ProfileProgId}\shell\open\command", string.Empty, $"{Quote(fullPath)} {ShellActivationService.ProfileFileArgument} \"%1\"")
        ];
    }

    public static IReadOnlyList<ShellJumpTaskDefinition> BuildJumpTaskDefinitions() =>
    [
        new ShellJumpTaskDefinition("Recommended setup", "Open LibreSpot on the guided Recommended setup page.", "--shell-action=recommended"),
        new ShellJumpTaskDefinition("Custom settings", "Open LibreSpot on the Custom profile editor.", "--shell-action=custom"),
        new ShellJumpTaskDefinition("Maintenance", "Open LibreSpot on the repair and rollback tools.", "--shell-action=maintenance"),
        new ShellJumpTaskDefinition("Import profile", "Open a .librespot profile import dialog.", "--shell-action=import-profile"),
        new ShellJumpTaskDefinition("Open LibreSpot folder", "Open the local LibreSpot config, log, and profile folder.", "--shell-action=open-folder")
    ];

    public static void RegisterCurrentUserShellHooksIfPossible()
    {
        var executablePath = TryGetCurrentExecutablePath();
        if (executablePath is null)
        {
            return;
        }

        try
        {
            foreach (var entry in BuildRegistrationPlan(executablePath))
            {
                using var key = Registry.CurrentUser.CreateSubKey(entry.KeyPath);
                key?.SetValue(entry.ValueName, entry.Value, RegistryValueKind.String);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Could not register LibreSpot shell hooks");
        }
    }

    public static void ConfigureJumpListIfPossible()
    {
        var executablePath = TryGetCurrentExecutablePath();
        if (executablePath is null || Application.Current is null)
        {
            return;
        }

        try
        {
            var jumpList = new JumpList
            {
                ShowFrequentCategory = false,
                ShowRecentCategory = false
            };

            foreach (var task in BuildJumpTaskDefinitions())
            {
                jumpList.JumpItems.Add(new JumpTask
                {
                    Title = task.Title,
                    Description = task.Description,
                    ApplicationPath = executablePath,
                    Arguments = task.Arguments,
                    CustomCategory = "LibreSpot",
                    IconResourcePath = executablePath,
                    IconResourceIndex = 0
                });
            }

            JumpList.SetJumpList(Application.Current, jumpList);
            jumpList.Apply();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Could not configure LibreSpot jump list");
        }
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}
