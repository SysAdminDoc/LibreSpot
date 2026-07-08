using System.Text.Json;
using System.Reflection;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class LocalProfileServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "LibreSpot.Profile.Tests", Guid.NewGuid().ToString("N"));
    private readonly ConfigurationService _configurationService;
    private readonly LocalProfileService _profileService;

    public LocalProfileServiceTests()
    {
        _configurationService = new ConfigurationService(_root);
        _profileService = new LocalProfileService(_configurationService);
    }

    [Fact]
    public async Task GetProfilesAsync_MigratesExistingConfigAndIncludesBundledTemplates()
    {
        var existing = AppCatalog.CreateRecommendedConfiguration();
        existing.SpotX_Premium = true;
        existing.RiskAcknowledged = true;
        await _configurationService.SaveAsync(existing);

        var profiles = await _profileService.GetProfilesAsync();

        Assert.Contains(profiles, profile => profile.IsBuiltIn && profile.Name == "Recommended");
        Assert.Contains(profiles, profile => profile.IsBuiltIn && profile.Name == "Minimal / Marketplace-only");
        Assert.Contains(profiles, profile => profile.IsBuiltIn && profile.Name == "Visual Theme");
        Assert.Contains(profiles, profile => profile.IsBuiltIn && profile.Name == "Lyrics Focus");
        Assert.Contains(profiles, profile => profile.IsBuiltIn && profile.Name == "Premium Account");
        Assert.Contains(profiles, profile => profile.IsBuiltIn && profile.Name == "Recovery / Reapply");
        var current = Assert.Single(profiles, profile => !profile.IsBuiltIn && profile.Name == "Current");
        Assert.True(current.IsActive);

        var loaded = await _profileService.LoadProfileAsync(current.Id);
        Assert.True(loaded.Configuration.SpotX_Premium);
        Assert.False(loaded.Configuration.RiskAcknowledged);
    }

    [Fact]
    public async Task CreateDuplicateRenameAndDeleteProfiles_EnforceNamesAndActiveFallback()
    {
        var created = await _profileService.CreateFromConfigurationAsync(
            "Daily Driver",
            "Main setup",
            AppCatalog.CreateRecommendedConfiguration());
        var duplicate = await _profileService.DuplicateAsync(created.Summary.Id, "Daily Driver Copy");
        var renamed = await _profileService.RenameAsync(duplicate.Summary.Id, "Rollback Profile");

        Assert.Equal("Rollback Profile", renamed.Summary.Name);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _profileService.CreateFromConfigurationAsync("Recommended", "", AppCatalog.CreateRecommendedConfiguration()));

        await _profileService.ApplyProfileAsync(created.Summary.Id);
        await _profileService.DeleteAsync(created.Summary.Id);

        var profiles = await _profileService.GetProfilesAsync();
        Assert.DoesNotContain(profiles, profile => profile.Id == created.Summary.Id);
        Assert.Contains(profiles, profile => profile.Id == "recommended" && profile.IsActive);
    }

    [Fact]
    public async Task GetProfilesAsync_SkipsMalformedAndSpoofedLocalProfileDocuments()
    {
        Directory.CreateDirectory(_profileService.ProfileDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(_profileService.ProfileDirectory, "bad.json"),
            """
            {
              "SchemaVersion": 1,
              "Id": "bad",
              "Name": "Bad",
              "Description": "Missing config should not break the profile gallery.",
              "CreatedAt": "2026-07-07T00:00:00Z",
              "UpdatedAt": "2026-07-07T00:00:00Z",
              "Configuration": null
            }
            """);
        await File.WriteAllTextAsync(
            Path.Combine(_profileService.ProfileDirectory, "recommended.json"),
            """
            {
              "SchemaVersion": 1,
              "Id": "recommended",
              "Name": "Spoofed Recommended",
              "Description": "Must not masquerade as a bundled template.",
              "CreatedAt": "2026-07-07T00:00:00Z",
              "UpdatedAt": "2026-07-07T00:00:00Z",
              "Configuration": {}
            }
            """);

        var profiles = await _profileService.GetProfilesAsync();

        Assert.DoesNotContain(profiles, profile => !profile.IsBuiltIn && profile.Id == "bad");
        Assert.DoesNotContain(profiles, profile => !profile.IsBuiltIn && profile.Id == "recommended");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _profileService.LoadProfileAsync("bad"));
    }

    [Fact]
    public async Task ApplyProfileAsync_WritesConfigAndPreviousActivePointer()
    {
        var firstConfig = AppCatalog.CreateRecommendedConfiguration();
        firstConfig.SpotX_LyricsTheme = "github";
        var secondConfig = AppCatalog.CreateRecommendedConfiguration();
        secondConfig.SpotX_LyricsTheme = "lavender";

        var first = await _profileService.CreateFromConfigurationAsync("First", "", firstConfig);
        var second = await _profileService.CreateFromConfigurationAsync("Second", "", secondConfig);

        await _profileService.ApplyProfileAsync(first.Summary.Id);
        await _profileService.ApplyProfileAsync(second.Summary.Id);

        var active = await _configurationService.LoadAsync();
        Assert.Equal("lavender", active.SpotX_LyricsTheme);
        Assert.Equal(first.Summary.Id, await _profileService.ReadPreviousActiveProfileIdAsync());
    }

    [Fact]
    public async Task ExportImport_RoundTripsSharedProfileWithoutRiskAcknowledgment()
    {
        var config = AppCatalog.CreateRecommendedConfiguration();
        config.SpotX_Premium = true;
        config.RiskAcknowledged = true;
        config.SpotX_CustomPatchesEnabled = true;
        config.SpotX_CustomPatchesJson = "{ \"xpui\": { \"match\": \"one\", \"replace\": \"two\" } }";
        config.SpotX_CustomPatchesSourceUrl = "https://user:secret@example.test/patches.json?token=topsecret#fragment";
        config.SpotX_CustomPatchesFetchedAtUtc = DateTimeOffset.Parse("2026-06-30T12:34:56Z");
        config.SpotX_CustomPatchesSourceByteCount = 54;
        config.SpotX_CustomPatchesSourceSha256 = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        var created = await _profileService.CreateFromConfigurationAsync("Share Me", "Portable setup", config);
        var exportPath = Path.Combine(_root, "share.librespot");

        await _profileService.ExportAsync(created.Summary.Id, exportPath);

        using var exported = JsonDocument.Parse(File.ReadAllText(exportPath));
        Assert.Equal("Share Me", exported.RootElement.GetProperty("profileName").GetString());
        Assert.Equal(
            typeof(LocalProfileService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            exported.RootElement.GetProperty("generatorVersion").GetString());
        Assert.False(exported.RootElement.GetProperty("settings").TryGetProperty("RiskAcknowledged", out _));
        Assert.Equal("https://example.test/patches.json", exported.RootElement.GetProperty("settings").GetProperty("SpotX_CustomPatchesSourceUrl").GetString());
        Assert.Equal(
            "{ \"xpui\": { \"match\": \"one\", \"replace\": \"two\" } }",
            exported.RootElement.GetProperty("settings").GetProperty("SpotX_CustomPatchesJson").GetString());
        Assert.Equal("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789", exported.RootElement.GetProperty("settings").GetProperty("SpotX_CustomPatchesSourceSha256").GetString());

        await _profileService.DeleteAsync(created.Summary.Id);
        var preview = await _profileService.PreviewImportAsync(exportPath);
        Assert.Equal("Share Me", preview.Name);
        Assert.True(preview.Configuration.SpotX_Premium);
        Assert.Equal("https://example.test/patches.json", preview.Configuration.SpotX_CustomPatchesSourceUrl);
        Assert.Equal(54, preview.Configuration.SpotX_CustomPatchesSourceByteCount);
        Assert.DoesNotContain(await _profileService.GetProfilesAsync(), profile => profile.Name == "Share Me");

        var imported = await _profileService.ImportAsync(exportPath);

        Assert.Equal("share-me", imported.Summary.Id);
        Assert.True(imported.Configuration.SpotX_Premium);
        Assert.False(imported.Configuration.RiskAcknowledged);
    }

    [Fact]
    public async Task ExportAsync_CanceledWriteDoesNotLeaveCorruptShareFile()
    {
        var created = await _profileService.CreateFromConfigurationAsync(
            "Cancelable Export",
            "Cancellation should not leave a half-written profile.",
            AppCatalog.CreateRecommendedConfiguration());
        var exportPath = Path.Combine(_root, "cancelled.librespot");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _profileService.ExportAsync(created.Summary.Id, exportPath, cts.Token));

        Assert.False(File.Exists(exportPath));
        Assert.Empty(Directory.EnumerateFiles(_root, "cancelled.librespot.*.tmp"));
    }

    [Fact]
    public async Task PreviewImportAsync_RejectsOversizedLocalProfileFiles()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "oversized.librespot");
        await File.WriteAllBytesAsync(path, new byte[(128 * 1024) + 1]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _profileService.PreviewImportAsync(path));

        Assert.Contains("too large", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShareCard_EmbedsInertPreviewUriAndImportsOnlyAfterConfirmation()
    {
        var config = AppCatalog.CreateRecommendedConfiguration();
        config.Spicetify_Theme = "Catppuccin";
        config.Spicetify_Scheme = "mocha";
        var created = await _profileService.CreateFromConfigurationAsync("Share Card", "QR payload", config);

        var card = await _profileService.CreateShareCardAsync(created.Summary.Id);

        Assert.Equal("Share Card", card.Name);
        Assert.StartsWith("librespot://profile?data=", card.ShareUri);
        Assert.Equal(card.ShareUri, card.QrPayload);

        var preview = await _profileService.PreviewShareUriAsync(card.ShareUri);

        Assert.Equal("Share Card", preview.Name);
        Assert.Equal("Catppuccin", preview.Configuration.Spicetify_Theme);

        await _profileService.DeleteAsync(created.Summary.Id);
        Assert.DoesNotContain(await _profileService.GetProfilesAsync(), profile => profile.Name == "Share Card");

        var imported = await _profileService.ImportAsync(preview);

        Assert.Equal("share-card", imported.Summary.Id);
        Assert.Equal("mocha", imported.Configuration.Spicetify_Scheme);
    }

    [Fact]
    public async Task PreviewShareUri_SupportsLocalFileAndHttpsSources()
    {
        var config = AppCatalog.CreateRecommendedConfiguration();
        config.SpotX_LyricsTheme = "github";
        var created = await _profileService.CreateFromConfigurationAsync("Remote Share", "Fetched preview", config);
        var exportPath = Path.Combine(_profileService.ProfileDirectory, "remote.librespot");
        await _profileService.ExportAsync(created.Summary.Id, exportPath);

        var localUri = $"librespot://profile?file={Uri.EscapeDataString(exportPath)}";
        var localPreview = await _profileService.PreviewShareUriAsync(localUri);

        Assert.Equal("Remote Share", localPreview.Name);
        Assert.Equal("github", localPreview.Configuration.SpotX_LyricsTheme);

        // The protocol handler is web-launchable; file= sources outside the
        // profile store must be refused so a page can't make the app read and
        // render arbitrary attacker-named local paths.
        var outsidePath = Path.Combine(_root, "outside.librespot");
        await _profileService.ExportAsync(created.Summary.Id, outsidePath);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _profileService.PreviewShareUriAsync($"librespot://profile?file={Uri.EscapeDataString(outsidePath)}"));

        var httpsPreview = await _profileService.PreviewShareUriAsync(
            "librespot://profile?url=https%3A%2F%2Fexample.test%2Fremote.librespot",
            (uri, _) =>
            {
                Assert.Equal("https://example.test/remote.librespot", uri.ToString());
                return Task.FromResult<Stream>(new MemoryStream(File.ReadAllBytes(exportPath)));
            });

        Assert.Equal("Remote Share", httpsPreview.Name);
        Assert.Equal("Fetched preview", httpsPreview.Description);
    }

    [Fact]
    public async Task PreviewShareUri_RejectsMalformedOversizedAndUnsupportedPayloads()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _profileService.PreviewShareUriAsync("librespot://install?data=abc"));

        var duplicateSource = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _profileService.PreviewShareUriAsync("librespot://profile?data=abc&data=def"));
        Assert.Contains("more than once", duplicateSource.Message);

        var invalidBase64 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _profileService.PreviewShareUriAsync("librespot://profile?data=not-valid!*"));
        Assert.Contains("base64url", invalidBase64.Message);

        var invalidEncoding = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _profileService.PreviewShareUriAsync("librespot://profile?data=%ZZ"));
        Assert.Contains("percent-encoding", invalidEncoding.Message);

        var oversized = EncodeBase64Url(new byte[8193]);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _profileService.PreviewShareUriAsync($"librespot://profile?data={oversized}"));

        var unsupported = EncodeBase64Url(System.Text.Encoding.UTF8.GetBytes(
            """
            {
              "schemaVersion": 99,
              "profileName": "Future",
              "settings": {}
            }
            """));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _profileService.PreviewShareUriAsync($"librespot://profile?data={unsupported}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _profileService.PreviewShareUriAsync("librespot://profile?url=http%3A%2F%2Fexample.test%2Fprofile.librespot"));
    }

    [Fact]
    public async Task ImportAsync_RejectsSharedRiskAcknowledgment()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "bad.librespot");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 1,
              "generator": "LibreSpot-Desktop",
              "generatorVersion": "4.0.0-preview.6",
              "createdAt": "2026-06-28T00:00:00Z",
              "profileName": "Unsafe",
              "settings": {
                "riskAcknowledged": true
              }
            }
            """);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _profileService.ImportAsync(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string EncodeBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
