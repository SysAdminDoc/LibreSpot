using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ParityManifestTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();
    private static readonly JsonDocument Manifest = LoadManifest();

    [Fact]
    public void Manifest_ListsEveryWpfInstallConfigurationProperty()
    {
        var manifestKeys = GetManifestConfigKeys();
        var wpfProperties = typeof(InstallConfiguration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missing = wpfProperties.Except(manifestKeys).ToList();
        Assert.True(
            missing.Count == 0,
            $"WPF InstallConfiguration properties not in manifest: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Manifest_ListsEveryBackendAllowedAction()
    {
        var manifestActions = GetManifestActions();

        var allowedField = typeof(BackendScriptService)
            .GetField("AllowedActions", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(allowedField);
        var allowed = (HashSet<string>)allowedField!.GetValue(null)!;

        var missing = allowed.Except(manifestActions).ToList();
        Assert.True(
            missing.Count == 0,
            $"BackendScriptService.AllowedActions not in manifest: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Manifest_ListsEveryMaintenanceCatalogAction()
    {
        var manifestActions = GetManifestActions(maintenanceOnly: true);

        var catalogActions = AppCatalog.MaintenanceActions.Select(a => a.Action).ToHashSet(StringComparer.Ordinal);

        var missing = catalogActions.Except(manifestActions).ToList();
        Assert.True(
            missing.Count == 0,
            $"AppCatalog.MaintenanceActions not in manifest maintenanceUI: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Manifest_MatchesBackendScriptValidateSet()
    {
        var backend = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");
        var match = Regex.Match(backend, @"\[ValidateSet\(([^)]+)\)\]");
        Assert.True(match.Success, "Backend script must have a ValidateSet on the Action parameter.");

        var validateSetActions = Regex.Matches(match.Groups[1].Value, @"'([^']+)'")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var manifestActions = GetManifestActions();

        var missing = validateSetActions.Except(manifestActions).ToList();
        Assert.True(
            missing.Count == 0,
            $"Backend ValidateSet actions not in manifest: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Manifest_ConfigKeysMatchScriptEasyDefaults()
    {
        var script = ReadFile("LibreSpot.ps1");

        var easyDefaultsMatch = Regex.Match(
            script,
            @"\$global:EasyDefaults\s*=\s*@\{(?<body>.+?)\}",
            RegexOptions.Singleline);
        Assert.True(easyDefaultsMatch.Success, "EasyDefaults hashtable not found in LibreSpot.ps1.");

        var body = easyDefaultsMatch.Groups["body"].Value;
        var scriptKeys = Regex.Matches(body, @"'?(\w+)'?\s*=")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var manifestKeys = GetManifestConfigKeys(scriptOnly: true);

        var missingFromManifest = scriptKeys.Except(manifestKeys).ToList();
        Assert.True(
            missingFromManifest.Count == 0,
            $"EasyDefaults keys not in manifest: {string.Join(", ", missingFromManifest)}");
    }

    [Fact]
    public void Manifest_HasNoUnownedOrUnknownRows()
    {
        var configKeys = Manifest.RootElement.GetProperty("configKeys");
        foreach (var entry in configKeys.EnumerateArray())
        {
            var key = entry.GetProperty("key").GetString()!;
            Assert.True(
                entry.TryGetProperty("script", out _) &&
                entry.TryGetProperty("wpf", out _) &&
                entry.TryGetProperty("wpfBackend", out _),
                $"Config key '{key}' is missing lane ownership flags.");
        }

        var actions = Manifest.RootElement.GetProperty("actions");
        foreach (var entry in actions.EnumerateArray())
        {
            var action = entry.GetProperty("action").GetString()!;
            Assert.True(
                entry.TryGetProperty("script", out _) &&
                entry.TryGetProperty("wpfBackend", out _) &&
                entry.TryGetProperty("wpfAllowed", out _) &&
                entry.TryGetProperty("maintenanceUI", out _),
                $"Action '{action}' is missing lane ownership flags.");
        }
    }

    private static HashSet<string> GetManifestConfigKeys(bool scriptOnly = false)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in Manifest.RootElement.GetProperty("configKeys").EnumerateArray())
        {
            if (scriptOnly && !entry.GetProperty("script").GetBoolean())
                continue;
            keys.Add(entry.GetProperty("key").GetString()!);
        }
        return keys;
    }

    private static HashSet<string> GetManifestActions(bool maintenanceOnly = false)
    {
        var actions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in Manifest.RootElement.GetProperty("actions").EnumerateArray())
        {
            if (maintenanceOnly && !entry.GetProperty("maintenanceUI").GetBoolean())
                continue;
            actions.Add(entry.GetProperty("action").GetString()!);
        }
        return actions;
    }

    private static JsonDocument LoadManifest()
    {
        var path = Path.Combine(RepoRoot, "schemas", "parity-manifest.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string ReadFile(params string[] relativeParts) =>
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
