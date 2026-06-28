using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.Services;

public sealed record LocalProfileSummary(
    string Id,
    string Name,
    string Description,
    bool IsBuiltIn,
    bool IsActive,
    DateTimeOffset UpdatedAt);

public sealed record LocalProfile(
    LocalProfileSummary Summary,
    InstallConfiguration Configuration);

public sealed class LocalProfileService
{
    private const int ProfileStoreSchemaVersion = 1;
    private const int ShareProfileSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ConfigurationService _configurationService;
    private readonly string _profileDirectory;
    private readonly string _activeProfilePath;
    private readonly string _previousActiveProfilePath;

    public LocalProfileService(ConfigurationService configurationService)
    {
        _configurationService = configurationService;
        _profileDirectory = Path.Combine(configurationService.ConfigDirectory, "profiles");
        _activeProfilePath = Path.Combine(configurationService.ConfigDirectory, "active-profile.json");
        _previousActiveProfilePath = Path.Combine(configurationService.ConfigDirectory, "active-profile.previous.json");
    }

    public string ProfileDirectory => _profileDirectory;

    public async Task<IReadOnlyList<LocalProfileSummary>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var activeId = await ReadActiveProfileIdAsync(cancellationToken);
        var profiles = BuiltInProfiles()
            .Concat(await ReadUserProfilesAsync(cancellationToken))
            .OrderBy(profile => profile.Summary.IsBuiltIn ? 0 : 1)
            .ThenBy(profile => profile.Summary.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(profile => profile.Summary with { IsActive = string.Equals(profile.Summary.Id, activeId, StringComparison.OrdinalIgnoreCase) })
            .ToArray();

        return profiles;
    }

    public async Task<LocalProfile> LoadProfileAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var activeId = await ReadActiveProfileIdAsync(cancellationToken);
        var builtIn = BuiltInProfiles().FirstOrDefault(profile => SameId(profile.Summary.Id, id));
        if (builtIn is not null)
        {
            return builtIn with { Summary = builtIn.Summary with { IsActive = SameId(builtIn.Summary.Id, activeId) } };
        }

        var document = await ReadUserProfileDocumentAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Profile '{id}' was not found.");
        return ToProfile(document, activeId);
    }

    public async Task<LocalProfile> CreateFromConfigurationAsync(
        string name,
        string description,
        InstallConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var id = await CreateUniqueProfileIdAsync(name, cancellationToken);
        var document = new StoredProfileDocument(
            ProfileStoreSchemaVersion,
            id,
            NormalizeProfileName(name),
            description.Trim(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            SanitizedConfiguration(configuration));
        await WriteUserProfileDocumentAsync(document, cancellationToken);
        return ToProfile(document, await ReadActiveProfileIdAsync(cancellationToken));
    }

    public async Task<LocalProfile> DuplicateAsync(string id, string newName, CancellationToken cancellationToken = default)
    {
        var source = await LoadProfileAsync(id, cancellationToken);
        return await CreateFromConfigurationAsync(newName, source.Summary.Description, source.Configuration, cancellationToken);
    }

    public async Task<LocalProfile> RenameAsync(string id, string newName, CancellationToken cancellationToken = default)
    {
        if (IsBuiltInId(id))
        {
            throw new InvalidOperationException("Built-in profiles cannot be renamed.");
        }

        var document = await ReadUserProfileDocumentAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Profile '{id}' was not found.");
        await EnsureNameAvailableAsync(newName, id, cancellationToken);
        var renamed = document with
        {
            Name = NormalizeProfileName(newName),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await WriteUserProfileDocumentAsync(renamed, cancellationToken);
        return ToProfile(renamed, await ReadActiveProfileIdAsync(cancellationToken));
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (IsBuiltInId(id))
        {
            throw new InvalidOperationException("Built-in profiles cannot be deleted.");
        }

        var path = UserProfilePath(id);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Profile '{id}' was not found.");
        }

        File.Delete(path);
        if (SameId(await ReadActiveProfileIdAsync(cancellationToken), id))
        {
            await WriteActiveProfileIdAsync("recommended", cancellationToken);
        }
    }

    public async Task ApplyProfileAsync(string id, CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileAsync(id, cancellationToken);
        var previousActiveId = await ReadActiveProfileIdAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(previousActiveId))
        {
            await WritePointerAsync(_previousActiveProfilePath, previousActiveId, cancellationToken);
        }

        await _configurationService.SaveAsync(profile.Configuration, cancellationToken);
        await WriteActiveProfileIdAsync(profile.Summary.Id, cancellationToken);
    }

    public async Task<string?> ReadPreviousActiveProfileIdAsync(CancellationToken cancellationToken = default)
    {
        var pointer = await ReadPointerAsync(_previousActiveProfilePath, cancellationToken);
        return pointer?.ProfileId;
    }

    public async Task ExportAsync(string id, string destinationPath, CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileAsync(id, cancellationToken);
        var settings = JsonSerializer.SerializeToElement(SanitizedConfiguration(profile.Configuration), JsonOptions)
            .EnumerateObject()
            .Where(property => !string.Equals(property.Name, nameof(InstallConfiguration.RiskAcknowledged), StringComparison.Ordinal))
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
        var document = new ShareProfileDocument(
            ShareProfileSchemaVersion,
            "LibreSpot-Desktop",
            AppVersion,
            DateTimeOffset.UtcNow,
            profile.Summary.Name,
            string.IsNullOrWhiteSpace(profile.Summary.Description) ? null : profile.Summary.Description,
            settings);

        var fullPath = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);
        await using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public async Task<LocalProfile> ImportAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(sourcePath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (!root.TryGetProperty("schemaVersion", out var schemaVersion) ||
            schemaVersion.ValueKind != JsonValueKind.Number ||
            !schemaVersion.TryGetInt32(out var version) ||
            version != ShareProfileSchemaVersion)
        {
            throw new InvalidOperationException("Profile schema version is not supported.");
        }

        var profileName = RequiredString(root, "profileName");
        if (!root.TryGetProperty("settings", out var settings) || settings.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Profile settings are missing.");
        }

        if (settings.EnumerateObject().Any(property =>
                string.Equals(property.Name, nameof(InstallConfiguration.RiskAcknowledged), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Shared profiles cannot contain RiskAcknowledged.");
        }

        var configuration = settings.Deserialize<InstallConfiguration>(JsonOptions)
            ?? throw new InvalidOperationException("Profile settings could not be read.");
        var notes = root.TryGetProperty("notes", out var notesValue) && notesValue.ValueKind == JsonValueKind.String
            ? notesValue.GetString() ?? string.Empty
            : string.Empty;
        return await CreateFromConfigurationAsync(profileName, notes, configuration, cancellationToken);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_profileDirectory);
        if (!File.Exists(_activeProfilePath))
        {
            var activeId = File.Exists(_configurationService.ConfigPath)
                ? await MigrateCurrentConfigurationAsync(cancellationToken)
                : "recommended";
            await WriteActiveProfileIdAsync(activeId, cancellationToken);
        }
    }

    private async Task<string> MigrateCurrentConfigurationAsync(CancellationToken cancellationToken)
    {
        var existing = await _configurationService.LoadAsync(cancellationToken);
        var id = await CreateUniqueProfileIdAsync("Current", cancellationToken);
        var document = new StoredProfileDocument(
            ProfileStoreSchemaVersion,
            id,
            "Current",
            "Migrated from the existing config.json.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            SanitizedConfiguration(existing));
        await WriteUserProfileDocumentAsync(document, cancellationToken);
        return id;
    }

    private async Task<IReadOnlyList<LocalProfile>> ReadUserProfilesAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_profileDirectory))
        {
            return Array.Empty<LocalProfile>();
        }

        var activeId = await ReadActiveProfileIdAsync(cancellationToken);
        var profiles = new List<LocalProfile>();
        foreach (var path in Directory.EnumerateFiles(_profileDirectory, "*.json"))
        {
            await using var stream = File.OpenRead(path);
            var document = await JsonSerializer.DeserializeAsync<StoredProfileDocument>(stream, JsonOptions, cancellationToken);
            if (document is not null && document.SchemaVersion == ProfileStoreSchemaVersion)
            {
                profiles.Add(ToProfile(document, activeId));
            }
        }

        return profiles;
    }

    private async Task<StoredProfileDocument?> ReadUserProfileDocumentAsync(string id, CancellationToken cancellationToken)
    {
        var path = UserProfilePath(id);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<StoredProfileDocument>(stream, JsonOptions, cancellationToken);
    }

    private async Task WriteUserProfileDocumentAsync(StoredProfileDocument document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_profileDirectory);
        var path = UserProfilePath(document.Id);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private async Task<string> CreateUniqueProfileIdAsync(string name, CancellationToken cancellationToken)
    {
        await EnsureNameAvailableAsync(name, null, cancellationToken);
        var baseId = Slugify(name);
        var id = baseId;
        for (var suffix = 2; File.Exists(UserProfilePath(id)) || IsBuiltInId(id); suffix++)
        {
            id = $"{baseId}-{suffix}";
        }

        return id;
    }

    private async Task EnsureNameAvailableAsync(string name, string? currentId, CancellationToken cancellationToken)
    {
        var normalized = NormalizeProfileName(name);
        var profiles = BuiltInProfiles().Concat(await ReadUserProfilesAsync(cancellationToken));
        if (profiles.Any(profile =>
                !SameId(profile.Summary.Id, currentId) &&
                string.Equals(profile.Summary.Name, normalized, StringComparison.CurrentCultureIgnoreCase)))
        {
            throw new InvalidOperationException($"Profile name '{normalized}' is already in use.");
        }
    }

    private async Task<string?> ReadActiveProfileIdAsync(CancellationToken cancellationToken) =>
        (await ReadPointerAsync(_activeProfilePath, cancellationToken))?.ProfileId;

    private Task WriteActiveProfileIdAsync(string profileId, CancellationToken cancellationToken) =>
        WritePointerAsync(_activeProfilePath, profileId, cancellationToken);

    private static async Task<ProfilePointerDocument?> ReadPointerAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ProfilePointerDocument>(stream, JsonOptions, cancellationToken);
    }

    private static async Task WritePointerAsync(string path, string profileId, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Environment.CurrentDirectory);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, new ProfilePointerDocument(ProfileStoreSchemaVersion, profileId, DateTimeOffset.UtcNow), JsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private string UserProfilePath(string id) => Path.Combine(_profileDirectory, $"{Slugify(id)}.json");

    private static LocalProfile ToProfile(StoredProfileDocument document, string? activeId) =>
        new(
            new LocalProfileSummary(
                document.Id,
                document.Name,
                document.Description,
                IsBuiltIn: false,
                IsActive: SameId(document.Id, activeId),
                document.UpdatedAt),
            SanitizedConfiguration(document.Configuration));

    private static IReadOnlyList<LocalProfile> BuiltInProfiles()
    {
        var recommended = AppCatalog.CreateRecommendedConfiguration();
        var minimal = recommended.Clone();
        minimal.Mode = "Custom";
        minimal.Spicetify_Theme = "(None - Marketplace Only)";
        minimal.Spicetify_Extensions = [];

        var visual = recommended.Clone();
        visual.Mode = "Custom";
        visual.Spicetify_Theme = "Dribbblish";
        visual.Spicetify_Scheme = "catppuccin-mocha";
        visual.Spicetify_Extensions = ["fullAppDisplay.js", "shuffle+.js"];

        var lyrics = recommended.Clone();
        lyrics.Mode = "Custom";
        lyrics.SpotX_LyricsEnabled = true;
        lyrics.SpotX_LyricsTheme = "lavender";
        lyrics.Spicetify_Extensions = ["beautiful-lyrics.mjs", "popupLyrics.js"];

        var premium = recommended.Clone();
        premium.Mode = "Custom";
        premium.SpotX_Premium = true;
        premium.SpotX_PodcastsOff = false;
        premium.SpotX_AdSectionsOff = true;

        var recovery = recommended.Clone();
        recovery.Mode = "Custom";
        recovery.CleanInstall = false;
        recovery.LaunchAfter = false;
        recovery.AutoReapply_Enabled = true;

        return
        [
            BuiltIn("recommended", "Recommended", "Opinionated defaults for first installs.", recommended),
            BuiltIn("minimal-marketplace", "Minimal / Marketplace-only", "Marketplace with no bundled theme or extension choices.", minimal),
            BuiltIn("visual-theme", "Visual Theme", "A visual setup with Dribbblish and useful interface extensions.", visual),
            BuiltIn("lyrics-focus", "Lyrics Focus", "Lyrics-focused SpotX and Spicetify settings.", lyrics),
            BuiltIn("premium-account", "Premium Account", "Keeps premium-account UI expectations calmer while blocking ad sections.", premium),
            BuiltIn("recovery-reapply", "Recovery / Reapply", "Conservative settings for reapply and watcher recovery runs.", recovery)
        ];
    }

    private static LocalProfile BuiltIn(string id, string name, string description, InstallConfiguration configuration) =>
        new(
            new LocalProfileSummary(id, name, description, IsBuiltIn: true, IsActive: false, DateTimeOffset.UnixEpoch),
            SanitizedConfiguration(configuration));

    private static InstallConfiguration SanitizedConfiguration(InstallConfiguration configuration)
    {
        var normalized = AppCatalog.NormalizeConfiguration(configuration).Clone();
        normalized.RiskAcknowledged = false;
        return normalized;
    }

    private static string NormalizeProfileName(string name)
    {
        var normalized = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Profile name is required.");
        }

        return normalized.Length <= 100 ? normalized : normalized[..100];
    }

    private static string Slugify(string value)
    {
        var chars = NormalizeProfileName(value)
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var slug = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(slug) ? "profile" : slug;
    }

    private static bool IsBuiltInId(string id) => BuiltInProfiles().Any(profile => SameId(profile.Summary.Id, id));
    private static bool SameId(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string RequiredString(JsonElement root, string property)
    {
        if (root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        throw new InvalidOperationException($"{property} is required.");
    }

    private static string AppVersion =>
        typeof(LocalProfileService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    private sealed record StoredProfileDocument(
        int SchemaVersion,
        string Id,
        string Name,
        string Description,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        InstallConfiguration Configuration);

    private sealed record ShareProfileDocument(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("generator")] string Generator,
        [property: JsonPropertyName("generatorVersion")] string GeneratorVersion,
        [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
        [property: JsonPropertyName("profileName")] string ProfileName,
        [property: JsonPropertyName("notes")] string? Notes,
        [property: JsonPropertyName("settings")] IReadOnlyDictionary<string, JsonElement> Settings);

    private sealed record ProfilePointerDocument(
        int SchemaVersion,
        string ProfileId,
        DateTimeOffset UpdatedAt);
}
