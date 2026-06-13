using System.IO;
using System.Reflection;
using System.Text.Json;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ElevationBoundaryTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();
    private static readonly JsonDocument Boundary = LoadBoundary();

    [Fact]
    public void Boundary_CoversAllBackendAllowedActions()
    {
        var allowedField = typeof(BackendScriptService)
            .GetField("AllowedActions", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(allowedField);
        var allowed = (HashSet<string>)allowedField!.GetValue(null)!;

        var boundaryActions = Boundary.RootElement
            .GetProperty("actions").EnumerateArray()
            .Select(a => a.GetProperty("action").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var missing = allowed.Except(boundaryActions).ToList();
        Assert.True(
            missing.Count == 0,
            $"Backend AllowedActions not in elevation boundary: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Boundary_CoversAllMaintenanceActions()
    {
        var catalogActions = AppCatalog.MaintenanceActions
            .Select(a => a.Action)
            .ToHashSet(StringComparer.Ordinal);

        var boundaryActions = Boundary.RootElement
            .GetProperty("actions").EnumerateArray()
            .Select(a => a.GetProperty("action").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var missing = catalogActions.Except(boundaryActions).ToList();
        Assert.True(
            missing.Count == 0,
            $"MaintenanceActions not in elevation boundary: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Boundary_DestructiveActionsMatchCatalog()
    {
        var catalogDestructive = AppCatalog.MaintenanceActions
            .Where(a => a.IsDestructive)
            .Select(a => a.Action)
            .ToHashSet(StringComparer.Ordinal);

        var boundaryDestructive = Boundary.RootElement
            .GetProperty("actions").EnumerateArray()
            .Where(a => a.GetProperty("destructive").GetBoolean())
            .Select(a => a.GetProperty("action").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(catalogDestructive, boundaryDestructive);
    }

    [Fact]
    public void Boundary_CheckUpdatesIsNoAdmin()
    {
        var checkUpdates = Boundary.RootElement
            .GetProperty("actions").EnumerateArray()
            .FirstOrDefault(a => a.GetProperty("action").GetString() == "CheckUpdates");

        Assert.NotEqual(default, checkUpdates);
        Assert.Equal("no-admin", checkUpdates.GetProperty("elevation").GetString());
        Assert.False(checkUpdates.GetProperty("mutating").GetBoolean());
    }

    [Fact]
    public void Boundary_AllActionsHaveRequiredFields()
    {
        var required = new[] { "action", "elevation", "mutating", "destructive" };

        foreach (var action in Boundary.RootElement.GetProperty("actions").EnumerateArray())
        {
            var name = action.GetProperty("action").GetString()!;
            foreach (var field in required)
            {
                Assert.True(
                    action.TryGetProperty(field, out _),
                    $"Action '{name}' is missing required field '{field}'.");
            }
        }
    }

    [Fact]
    public void Boundary_WpfManifestIsAsInvoker()
    {
        var manifest = File.ReadAllText(
            Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "app.manifest"));

        Assert.Contains("asInvoker", manifest);
    }

    private static JsonDocument LoadBoundary()
    {
        var path = Path.Combine(RepoRoot, "schemas", "elevation-boundary.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

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
