using System.Text.Json;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class DataInventoryContractTests
{
    private static readonly string[] ExpectedLocationIds =
    [
        "active-profile-pointer",
        "asset-cache",
        "community-asset-drift-cache",
        "corrupt-config-quarantine",
        "fleet-logs",
        "install-log",
        "local-profiles",
        "machine-config",
        "marketplace-evidence",
        "operation-journal",
        "previous-profile-pointer",
        "profile-activation-transaction",
        "remove-self-data-receipt",
        "run-receipt",
        "scheduled-task",
        "self-update-cache",
        "spicetify-backups",
        "spicetify-preservation-evidence",
        "stable-upstream-freshness-cache",
        "support-archives",
        "temporary-workspaces",
        "undo-state-snapshots",
        "user-config",
        "watcher-log",
        "watcher-state",
        "wpf-crashes",
        "wpf-logs",
        "wpf-runtime",
        "wpf-upstream-drift-cache"
    ];

    private static readonly string[] RemoveSelfDataIds =
    [
        "active-profile-pointer",
        "asset-cache",
        "community-asset-drift-cache",
        "corrupt-config-quarantine",
        "fleet-logs",
        "install-log",
        "local-profiles",
        "machine-config",
        "marketplace-evidence",
        "operation-journal",
        "previous-profile-pointer",
        "profile-activation-transaction",
        "run-receipt",
        "scheduled-task",
        "self-update-cache",
        "spicetify-backups",
        "spicetify-preservation-evidence",
        "stable-upstream-freshness-cache",
        "support-archives",
        "undo-state-snapshots",
        "user-config",
        "watcher-log",
        "watcher-state",
        "wpf-crashes",
        "wpf-logs",
        "wpf-runtime",
        "wpf-upstream-drift-cache"
    ];

    [Fact]
    public void Inventory_DeclaresEveryKnownOwnedLocationAndRequiredPolicy()
    {
        using var document = LoadInventory();
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());

        var locations = root.GetProperty("dataLocations").EnumerateArray().ToArray();
        var actualIds = locations
            .Select(location => location.GetProperty("id").GetString()!)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ExpectedLocationIds, actualIds);
        Assert.Equal(actualIds.Length, actualIds.Distinct(StringComparer.Ordinal).Count());

        foreach (var location in locations)
        {
            var id = location.GetProperty("id").GetString();
            Assert.False(string.IsNullOrWhiteSpace(location.GetProperty("path").GetString()), $"{id} path");
            Assert.False(string.IsNullOrWhiteSpace(location.GetProperty("owner").GetString()), $"{id} owner");
            Assert.False(string.IsNullOrWhiteSpace(location.GetProperty("codeOwner").GetString()), $"{id} codeOwner");
            Assert.True(location.TryGetProperty("schemaVersion", out _), $"{id} schemaVersion");
            Assert.False(string.IsNullOrWhiteSpace(location.GetProperty("sensitivity").GetString()), $"{id} sensitivity");
            Assert.False(string.IsNullOrWhiteSpace(location.GetProperty("retention").GetString()), $"{id} retention");
            Assert.False(string.IsNullOrWhiteSpace(location.GetProperty("retentionEnforcement").GetString()), $"{id} retentionEnforcement");
            Assert.False(string.IsNullOrWhiteSpace(location.GetProperty("userDeletePath").GetString()), $"{id} userDeletePath");
            Assert.False(string.IsNullOrWhiteSpace(location.GetProperty("deleteBehavior").GetString()), $"{id} deleteBehavior");
            Assert.False(string.IsNullOrWhiteSpace(location.GetProperty("supportExportBehavior").GetString()), $"{id} supportExportBehavior");
            Assert.Equal(JsonValueKind.Array, location.GetProperty("redactionRules").ValueKind);
            Assert.NotEmpty(location.GetProperty("writeSites").EnumerateArray());

            var included = location.GetProperty("includedInSupportBundle").GetBoolean();
            var exportBehavior = location.GetProperty("supportExportBehavior").GetString();
            Assert.Equal(included, !string.Equals(exportBehavior, "excluded", StringComparison.Ordinal) &&
                                   !string.Equals(exportBehavior, "output-artifact-never-reincluded", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Inventory_WriteSiteMarkersStayBoundToLiveStorageOwners()
    {
        using var document = LoadInventory();
        foreach (var location in document.RootElement.GetProperty("dataLocations").EnumerateArray())
        {
            var id = location.GetProperty("id").GetString();
            foreach (var writeSite in location.GetProperty("writeSites").EnumerateArray())
            {
                var source = writeSite.GetProperty("source").GetString()!;
                var marker = writeSite.GetProperty("marker").GetString()!;
                var fullPath = Path.Combine(RepoRoot, source.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(fullPath), $"{id} inventory source does not exist: {source}");
                Assert.Contains(marker, File.ReadAllText(fullPath), StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Inventory_RemoveSelfDataEntriesMatchAllManagedRoots()
    {
        using var document = LoadInventory();
        var locations = document.RootElement.GetProperty("dataLocations")
            .EnumerateArray()
            .Where(location => location.GetProperty("deleteBehavior").GetString() == "remove-self-data")
            .ToArray();
        var ids = locations
            .Select(location => location.GetProperty("id").GetString()!)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(RemoveSelfDataIds, ids);

        foreach (var location in locations.Where(location => location.GetProperty("id").GetString() != "scheduled-task"))
        {
            var path = location.GetProperty("path").GetString()!;
            Assert.True(
                path.StartsWith("%APPDATA%\\LibreSpot", StringComparison.Ordinal) ||
                path.StartsWith("%LOCALAPPDATA%\\LibreSpot", StringComparison.Ordinal) ||
                path.StartsWith("%ProgramData%\\LibreSpot", StringComparison.Ordinal) ||
                path.StartsWith("%USERPROFILE%\\LibreSpot_Backups", StringComparison.Ordinal),
                $"RemoveSelfData does not own the declared root for {location.GetProperty("id").GetString()}: {path}");
        }

        var stableHost = File.ReadAllText(Path.Combine(RepoRoot, "LibreSpot.ps1"));
        var backend = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1"));
        foreach (var script in new[] { stableHost, backend })
        {
            Assert.Contains("$global:CONFIG_DIR", script, StringComparison.Ordinal);
            Assert.Contains("$global:BACKUP_ROOT", script, StringComparison.Ordinal);
            Assert.Contains("Join-Path $env:LOCALAPPDATA 'LibreSpot'", script, StringComparison.Ordinal);
            Assert.Contains("Join-Path $env:ProgramData 'LibreSpot'", script, StringComparison.Ordinal);
            Assert.Contains("Unregister-AutoReapplyTask", script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Inventory_SupportExportMatchesCodeAndExcludesPrivateArtifacts()
    {
        using var document = LoadInventory();
        var locations = document.RootElement.GetProperty("dataLocations").EnumerateArray().ToDictionary(
            location => location.GetProperty("id").GetString()!,
            StringComparer.Ordinal);
        var service = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Services", "SupportBundleService.cs"));

        foreach (var pair in new[]
                 {
                     ("install-log", "install.log"),
                     ("operation-journal", "operation-journal.jsonl"),
                     ("watcher-log", "watcher.log"),
                     ("watcher-state", "watcher-state.json"),
                     ("spicetify-preservation-evidence", "spicetify-preservation-latest.json")
                 })
        {
            Assert.True(locations[pair.Item1].GetProperty("includedInSupportBundle").GetBoolean());
            Assert.Contains(pair.Item2, service, StringComparison.Ordinal);
        }

        foreach (var id in new[]
                 {
                     "local-profiles",
                     "active-profile-pointer",
                     "previous-profile-pointer",
                     "profile-activation-transaction",
                     "run-receipt",
                     "undo-state-snapshots",
                     "spicetify-backups",
                     "temporary-workspaces"
                 })
        {
            Assert.False(locations[id].GetProperty("includedInSupportBundle").GetBoolean(), id);
            Assert.Equal("excluded", locations[id].GetProperty("supportExportBehavior").GetString());
        }

        Assert.Contains("RemovePreviousStatePayloads", service, StringComparison.Ordinal);
        Assert.Contains("previousstateref", service, StringComparison.Ordinal);
    }

    private static JsonDocument LoadInventory() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "schemas", "data-inventory.json")));

    private static string RepoRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
        }
    }
}
