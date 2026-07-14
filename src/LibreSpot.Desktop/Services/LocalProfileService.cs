using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
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

public sealed record LocalProfileImportPreview(
    string Name,
    string Description,
    InstallConfiguration Configuration,
    string SourcePath);

public sealed record LocalProfileShareCard(
    string Name,
    DateTimeOffset CreatedAt,
    string ShareUri,
    string QrPayload);

internal enum ProfileActivationStage
{
    PreviousConfigStaged,
    NewConfigStaged,
    TransactionWritten,
    PreviousPointerWritten,
    ConfigWritten,
    ActivePointerWritten,
    TransactionRemoved
}

public sealed class LocalProfileService
{
    private const int ProfileStoreSchemaVersion = 1;
    private const int ShareProfileSchemaVersion = 1;
    private const int MaxEmbeddedProfileBytes = 8 * 1024;
    private const int MaxLocalProfileBytes = 128 * 1024;
    private const int MaxRemoteProfileBytes = 128 * 1024;
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
    private readonly string _activationLockPath;
    private readonly string _activationTransactionPath;
    private readonly Action<ProfileActivationStage>? _activationFaultInjector;

    public LocalProfileService(ConfigurationService configurationService)
        : this(configurationService, null)
    {
    }

    internal LocalProfileService(
        ConfigurationService configurationService,
        Action<ProfileActivationStage>? activationFaultInjector)
    {
        _configurationService = configurationService;
        _profileDirectory = Path.Combine(configurationService.ConfigDirectory, "profiles");
        _activeProfilePath = Path.Combine(configurationService.ConfigDirectory, "active-profile.json");
        _previousActiveProfilePath = Path.Combine(configurationService.ConfigDirectory, "active-profile.previous.json");
        _activationLockPath = Path.Combine(configurationService.ConfigDirectory, "profile-activation.lock");
        _activationTransactionPath = Path.Combine(configurationService.ConfigDirectory, "profile-activation.pending.json");
        _activationFaultInjector = activationFaultInjector;
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
        return await LoadProfileCoreAsync(id, cancellationToken);
    }

    private async Task<LocalProfile> LoadProfileCoreAsync(string id, CancellationToken cancellationToken)
    {
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

        await using var activationLock = await AcquireActivationLockAsync(cancellationToken);
        await RecoverInterruptedActivationAsync(cancellationToken);
        await EnsureInitializedCoreAsync(cancellationToken);

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
        await using var activationLock = await AcquireActivationLockAsync(cancellationToken);
        await RecoverInterruptedActivationAsync(cancellationToken);
        await EnsureInitializedCoreAsync(cancellationToken);

        var profile = await LoadProfileCoreAsync(id, cancellationToken);
        var previousActiveId = await ReadActiveProfileIdAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(previousActiveId))
        {
            throw new InvalidOperationException("The active profile pointer is unavailable.");
        }

        var priorPreviousId = await ReadPreviousActiveProfileIdAsync(cancellationToken);
        var transactionId = Guid.NewGuid().ToString("N");
        var oldConfigStageFile = $"profile-activation.{transactionId}.previous.staged.json";
        var newConfigStageFile = $"profile-activation.{transactionId}.next.staged.json";
        var oldConfigStagePath = Path.Combine(_configurationService.ConfigDirectory, oldConfigStageFile);
        var newConfigStagePath = Path.Combine(_configurationService.ConfigDirectory, newConfigStageFile);
        var oldConfigExisted = File.Exists(_configurationService.ConfigPath);
        var transactionWritten = false;

        try
        {
            if (oldConfigExisted)
            {
                await CopyFileDurablyAsync(_configurationService.ConfigPath, oldConfigStagePath, cancellationToken);
            }
            else
            {
                await WriteBytesDurablyAsync(oldConfigStagePath, [], cancellationToken);
            }
            InjectActivationFault(ProfileActivationStage.PreviousConfigStaged);

            await _configurationService.SaveToPathAsync(profile.Configuration, newConfigStagePath, cancellationToken);
            InjectActivationFault(ProfileActivationStage.NewConfigStaged);

            var transaction = new ProfileActivationTransactionDocument(
                ProfileStoreSchemaVersion,
                transactionId,
                previousActiveId,
                profile.Summary.Id,
                priorPreviousId,
                oldConfigExisted,
                await ComputeFileSha256Async(oldConfigStagePath, cancellationToken),
                await ComputeFileSha256Async(newConfigStagePath, cancellationToken),
                oldConfigStageFile,
                newConfigStageFile,
                DateTimeOffset.UtcNow);
            await WriteJsonAtomicallyAsync(_activationTransactionPath, transaction, cancellationToken);
            transactionWritten = true;
            InjectActivationFault(ProfileActivationStage.TransactionWritten);

            await WritePointerAsync(_previousActiveProfilePath, previousActiveId, cancellationToken);
            InjectActivationFault(ProfileActivationStage.PreviousPointerWritten);

            await ReplaceFromStageAsync(newConfigStagePath, _configurationService.ConfigPath, cancellationToken);
            InjectActivationFault(ProfileActivationStage.ConfigWritten);

            await WriteActiveProfileIdAsync(profile.Summary.Id, cancellationToken);
            InjectActivationFault(ProfileActivationStage.ActivePointerWritten);

            File.Delete(_activationTransactionPath);
            transactionWritten = false;
            InjectActivationFault(ProfileActivationStage.TransactionRemoved);
            DeleteFileBestEffort(oldConfigStagePath);
            DeleteFileBestEffort(newConfigStagePath);
        }
        catch
        {
            if (!transactionWritten)
            {
                DeleteFileBestEffort(oldConfigStagePath);
                DeleteFileBestEffort(newConfigStagePath);
            }
            throw;
        }
    }

    public async Task<string?> ReadPreviousActiveProfileIdAsync(CancellationToken cancellationToken = default)
    {
        var pointer = await ReadPointerAsync(_previousActiveProfilePath, cancellationToken);
        return pointer?.ProfileId;
    }

    public async Task ExportAsync(string id, string destinationPath, CancellationToken cancellationToken = default)
    {
        var document = await CreateShareProfileDocumentAsync(id, cancellationToken);

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $"{Path.GetFileName(fullPath)}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            throw;
        }
    }

    public async Task<LocalProfileShareCard> CreateShareCardAsync(string id, CancellationToken cancellationToken = default)
    {
        var document = await CreateShareProfileDocumentAsync(id, cancellationToken);
        var shareUri = CreateEmbeddedShareUri(document);
        return new LocalProfileShareCard(document.ProfileName, document.CreatedAt, shareUri, shareUri);
    }

    public async Task<LocalProfile> ImportAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var preview = await PreviewImportAsync(sourcePath, cancellationToken);
        return await ImportAsync(preview, cancellationToken);
    }

    public Task<LocalProfile> ImportAsync(LocalProfileImportPreview preview, CancellationToken cancellationToken = default) =>
        CreateFromConfigurationAsync(preview.Name, preview.Description, preview.Configuration, cancellationToken);

    public async Task<LocalProfileImportPreview> PreviewImportAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        if (!string.Equals(Path.GetExtension(fullPath), ".librespot", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Profile files must use the .librespot extension.");
        }

        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Exists && fileInfo.Length > MaxLocalProfileBytes)
        {
            throw new InvalidOperationException("Profile file is too large.");
        }

        await using var stream = File.OpenRead(fullPath);
        var payload = await ReadLimitedAsync(stream, MaxLocalProfileBytes, cancellationToken);
        await using var payloadStream = new MemoryStream(payload);
        return await PreviewImportStreamAsync(payloadStream, fullPath, cancellationToken);
    }

    public async Task<LocalProfileImportPreview> PreviewShareUriAsync(
        string shareUri,
        Func<Uri, CancellationToken, Task<Stream>>? httpsFetcher = null,
        CancellationToken cancellationToken = default)
    {
        var rawQueryIndex = (shareUri ?? string.Empty).IndexOf('?');
        if (rawQueryIndex >= 0 && HasInvalidPercentEncoding(shareUri![(rawQueryIndex + 1)..]))
        {
            throw new InvalidOperationException("Share URI query contains invalid percent-encoding.");
        }

        if (!Uri.TryCreate(shareUri, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "librespot", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "profile", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Share URI must use the librespot://profile scheme.");
        }

        var query = ParseQuery(uri.Query);
        var hasData = query.TryGetValue("data", out var data);
        var hasFile = query.TryGetValue("file", out var file);
        var hasUrl = query.TryGetValue("url", out var url);
        var sourceCount = new[] { hasData, hasFile, hasUrl }.Count(value => value);
        if (sourceCount != 1)
        {
            throw new InvalidOperationException("Share URI must contain exactly one profile source: data, file, or url.");
        }

        if (hasData)
        {
            var bytes = DecodeBase64Url(data!);
            if (bytes.Length > MaxEmbeddedProfileBytes)
            {
                throw new InvalidOperationException("Embedded profile payload is too large.");
            }

            await using var stream = new MemoryStream(bytes);
            return await PreviewImportStreamAsync(stream, uri.ToString(), cancellationToken);
        }

        if (hasFile)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                throw new InvalidOperationException("Share URI file path is empty.");
            }

            var resolvedFile = Path.GetFullPath(file);
            if (!string.Equals(Path.GetExtension(resolvedFile), ".librespot", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Share URI file references must use the .librespot extension.");
            }

            // librespot:// URIs are launchable by any web page via the OS
            // protocol handler; a file= source must not become a primitive
            // for reading and rendering arbitrary attacker-named local paths.
            // Local files outside the profile store import via the file
            // dialog or the .librespot file association instead.
            var profileRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_profileDirectory));
            var resolvedDirectory = Path.GetDirectoryName(resolvedFile);
            if (!string.Equals(resolvedDirectory is null ? null : Path.TrimEndingDirectorySeparator(resolvedDirectory), profileRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Share URI file references must point inside the LibreSpot profiles folder. Use Import from file for other locations.");
            }

            return await PreviewImportAsync(resolvedFile, cancellationToken);
        }

        if (!Uri.TryCreate(url!, UriKind.Absolute, out var sourceUri) ||
            !string.Equals(sourceUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Remote profile URLs must use HTTPS.");
        }

        await using var remote = httpsFetcher is null
            ? await FetchHttpsProfileAsync(sourceUri, cancellationToken)
            : await httpsFetcher(sourceUri, cancellationToken);
        var payload = await ReadLimitedAsync(remote, MaxRemoteProfileBytes, cancellationToken);
        await using var payloadStream = new MemoryStream(payload);
        return await PreviewImportStreamAsync(payloadStream, sourceUri.ToString(), cancellationToken);
    }

    private static async Task<LocalProfileImportPreview> PreviewImportStreamAsync(Stream stream, string sourcePath, CancellationToken cancellationToken)
    {
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
        return new LocalProfileImportPreview(
            NormalizeProfileName(profileName),
            notes.Trim(),
            SanitizedConfiguration(configuration),
            sourcePath);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        await using var activationLock = await AcquireActivationLockAsync(cancellationToken);
        await RecoverInterruptedActivationAsync(cancellationToken);
        await EnsureInitializedCoreAsync(cancellationToken);
    }

    private async Task EnsureInitializedCoreAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_profileDirectory);
        var activePointer = await ReadPointerAsync(_activeProfilePath, cancellationToken);
        var targetExists = activePointer is not null &&
            (IsBuiltInId(activePointer.ProfileId) ||
             await ReadUserProfileDocumentAsync(activePointer.ProfileId, cancellationToken) is not null);
        if (!targetExists)
        {
            var activeId = File.Exists(_configurationService.ConfigPath)
                ? await MigrateCurrentConfigurationAsync(cancellationToken)
                : "recommended";
            await WriteActiveProfileIdAsync(activeId, cancellationToken);
        }
    }

    private async Task<FileStream> AcquireActivationLockAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_configurationService.ConfigDirectory);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    _activationLockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(50, cancellationToken);
            }
        }
    }

    private async Task RecoverInterruptedActivationAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_activationTransactionPath))
        {
            CleanupOrphanedActivationStages();
            return;
        }

        ProfileActivationTransactionDocument transaction;
        try
        {
            var info = new FileInfo(_activationTransactionPath);
            if (info.Length is <= 0 or > MaxLocalProfileBytes)
            {
                throw new InvalidDataException("Profile activation transaction has an invalid size.");
            }

            await using var stream = File.Open(_activationTransactionPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            transaction = await JsonSerializer.DeserializeAsync<ProfileActivationTransactionDocument>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Profile activation transaction is empty.");
            ValidateActivationTransaction(transaction);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "The pending profile activation record is unreadable. Move profile-activation.pending.json aside before retrying.",
                ex);
        }

        var oldStagePath = Path.Combine(_configurationService.ConfigDirectory, transaction.OldConfigStageFile);
        var newStagePath = Path.Combine(_configurationService.ConfigDirectory, transaction.NewConfigStageFile);
        var activeId = await ReadActiveProfileIdAsync(cancellationToken);
        var currentFingerprint = File.Exists(_configurationService.ConfigPath)
            ? await ComputeFileSha256Async(_configurationService.ConfigPath, cancellationToken)
            : EmptySha256;

        if (SameId(activeId, transaction.NewProfileId) &&
            string.Equals(currentFingerprint, transaction.NewConfigFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            await WritePointerAsync(_previousActiveProfilePath, transaction.OldProfileId, cancellationToken);
            CompleteActivationRecovery(oldStagePath, newStagePath);
            return;
        }

        var currentIsOld = string.Equals(currentFingerprint, transaction.OldConfigFingerprint, StringComparison.OrdinalIgnoreCase) &&
            (transaction.OldConfigExisted || !File.Exists(_configurationService.ConfigPath));
        var oldStageIsValid = await HasExpectedFingerprintAsync(oldStagePath, transaction.OldConfigFingerprint, cancellationToken);
        if (currentIsOld || oldStageIsValid)
        {
            if (!currentIsOld)
            {
                if (transaction.OldConfigExisted)
                {
                    await ReplaceFromStageAsync(oldStagePath, _configurationService.ConfigPath, cancellationToken);
                }
                else
                {
                    File.Delete(_configurationService.ConfigPath);
                }
            }

            await WriteActiveProfileIdAsync(transaction.OldProfileId, cancellationToken);
            if (string.IsNullOrWhiteSpace(transaction.PreviousProfileId))
            {
                File.Delete(_previousActiveProfilePath);
            }
            else
            {
                await WritePointerAsync(_previousActiveProfilePath, transaction.PreviousProfileId, cancellationToken);
            }
            CompleteActivationRecovery(oldStagePath, newStagePath);
            return;
        }

        var currentIsNew = string.Equals(currentFingerprint, transaction.NewConfigFingerprint, StringComparison.OrdinalIgnoreCase);
        var newStageIsValid = await HasExpectedFingerprintAsync(newStagePath, transaction.NewConfigFingerprint, cancellationToken);
        if (!currentIsNew && newStageIsValid)
        {
            await ReplaceFromStageAsync(newStagePath, _configurationService.ConfigPath, cancellationToken);
            currentIsNew = true;
        }

        if (currentIsNew)
        {
            await WritePointerAsync(_previousActiveProfilePath, transaction.OldProfileId, cancellationToken);
            await WriteActiveProfileIdAsync(transaction.NewProfileId, cancellationToken);
            CompleteActivationRecovery(oldStagePath, newStagePath);
            return;
        }

        throw new InvalidOperationException("The pending profile activation cannot be recovered because neither staged configuration matches its recorded fingerprint.");
    }

    private void CompleteActivationRecovery(string oldStagePath, string newStagePath)
    {
        File.Delete(_activationTransactionPath);
        DeleteFileBestEffort(oldStagePath);
        DeleteFileBestEffort(newStagePath);
        CleanupOrphanedActivationStages();
    }

    private void CleanupOrphanedActivationStages()
    {
        if (!Directory.Exists(_configurationService.ConfigDirectory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(_configurationService.ConfigDirectory, "profile-activation.*.staged.json"))
        {
            DeleteFileBestEffort(path);
        }
    }

    private static void ValidateActivationTransaction(ProfileActivationTransactionDocument transaction)
    {
        if (transaction.SchemaVersion != ProfileStoreSchemaVersion ||
            string.IsNullOrWhiteSpace(transaction.TransactionId) ||
            !Guid.TryParseExact(transaction.TransactionId, "N", out _) ||
            string.IsNullOrWhiteSpace(transaction.OldProfileId) ||
            string.IsNullOrWhiteSpace(transaction.NewProfileId) ||
            !string.Equals(transaction.OldProfileId, Slugify(transaction.OldProfileId), StringComparison.Ordinal) ||
            !string.Equals(transaction.NewProfileId, Slugify(transaction.NewProfileId), StringComparison.Ordinal) ||
            !IsSha256(transaction.OldConfigFingerprint) ||
            !IsSha256(transaction.NewConfigFingerprint) ||
            !IsActivationStageFile(transaction.OldConfigStageFile, transaction.TransactionId, "previous") ||
            !IsActivationStageFile(transaction.NewConfigStageFile, transaction.TransactionId, "next"))
        {
            throw new InvalidDataException("Profile activation transaction fields are invalid.");
        }
    }

    private static bool IsActivationStageFile(string fileName, string transactionId, string role) =>
        string.Equals(fileName, $"profile-activation.{transactionId}.{role}.staged.json", StringComparison.Ordinal);

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    private static async Task<bool> HasExpectedFingerprintAsync(string path, string expected, CancellationToken cancellationToken) =>
        File.Exists(path) && string.Equals(await ComputeFileSha256Async(path, cancellationToken), expected, StringComparison.OrdinalIgnoreCase);

    private static async Task<string> ComputeFileSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static async Task CopyFileDurablyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        var info = new FileInfo(sourcePath);
        if (info.Length > ConfigurationService.MaxConfigBytes)
        {
            throw new InvalidDataException($"config.json is {info.Length} bytes; the maximum is {ConfigurationService.MaxConfigBytes} bytes.");
        }

        await using var source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(destination, cancellationToken);
        await destination.FlushAsync(cancellationToken);
        destination.Flush(flushToDisk: true);
    }

    private static async Task WriteBytesDurablyAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static async Task ReplaceFromStageAsync(string stagePath, string destinationPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $"profile-activation.{Guid.NewGuid():N}.commit.tmp");
        try
        {
            await CopyFileDurablyAsync(stagePath, tempPath, cancellationToken);
            File.Move(tempPath, destinationPath, overwrite: true);
        }
        finally
        {
            DeleteFileBestEffort(tempPath);
        }
    }

    private static async Task WriteJsonAtomicallyAsync<T>(string path, T document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $"profile-activation.{Guid.NewGuid():N}.marker.tmp");
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            DeleteFileBestEffort(tempPath);
        }
    }

    private static void DeleteFileBestEffort(string path)
    {
        try { File.Delete(path); } catch { }
    }

    private void InjectActivationFault(ProfileActivationStage stage) => _activationFaultInjector?.Invoke(stage);

    private static readonly string EmptySha256 = Convert.ToHexString(SHA256.HashData([])).ToLowerInvariant();

    private async Task<ShareProfileDocument> CreateShareProfileDocumentAsync(string id, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(id, cancellationToken);
        var settings = JsonSerializer.SerializeToElement(SanitizedConfiguration(profile.Configuration, redactShareProvenance: true), JsonOptions)
            .EnumerateObject()
            .Where(property => !string.Equals(property.Name, nameof(InstallConfiguration.RiskAcknowledged), StringComparison.Ordinal))
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);

        return new ShareProfileDocument(
            ShareProfileSchemaVersion,
            "LibreSpot-Desktop",
            AppVersion,
            DateTimeOffset.UtcNow,
            profile.Summary.Name,
            string.IsNullOrWhiteSpace(profile.Summary.Description) ? null : profile.Summary.Description,
            settings);
    }

    private static string CreateEmbeddedShareUri(ShareProfileDocument document)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        if (bytes.Length > MaxEmbeddedProfileBytes)
        {
            throw new InvalidOperationException("Profile is too large for an embedded share URI.");
        }

        return $"librespot://profile?data={EncodeBase64Url(bytes)}";
    }

    // Shared-profile imports can be triggered without confirmation via the
    // librespot:// protocol handler, so the fetch is SSRF-guarded at connect
    // time against loopback / link-local / private / metadata addresses.
    private static readonly HttpClient SharedHttpClient = new(PrivateNetworkGuard.CreateGuardedHandler()) { Timeout = TimeSpan.FromSeconds(30) };

    private static async Task<Stream> FetchHttpsProfileAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await SharedHttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        // The HTTPS-only rule must survive redirects: re-check the scheme of
        // the URI the client actually landed on.
        if (!string.Equals(response.RequestMessage?.RequestUri?.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Profile URL redirected to a non-HTTPS address.");
        }

        if (response.Content.Headers.ContentLength is > MaxRemoteProfileBytes)
        {
            throw new InvalidOperationException("Profile payload is too large.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return new MemoryStream(await ReadLimitedAsync(stream, MaxRemoteProfileBytes, cancellationToken));
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidOperationException("Profile payload is too large.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (HasInvalidPercentEncoding(pair[0]) || (pair.Length == 2 && HasInvalidPercentEncoding(pair[1])))
            {
                throw new InvalidOperationException("Share URI query contains invalid percent-encoding.");
            }

            string key;
            string value;
            try
            {
                key = Uri.UnescapeDataString(pair[0].Replace("+", "%20"));
                value = pair.Length == 2
                    ? Uri.UnescapeDataString(pair[1].Replace("+", "%20"))
                    : string.Empty;
            }
            catch (UriFormatException ex)
            {
                throw new InvalidOperationException("Share URI query contains invalid percent-encoding.", ex);
            }

            if (values.ContainsKey(key))
            {
                throw new InvalidOperationException($"Share URI contains the '{key}' parameter more than once.");
            }

            values[key] = value;
        }

        return values;
    }

    private static bool HasInvalidPercentEncoding(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (index + 2 >= value.Length ||
                !char.IsAsciiHexDigit(value[index + 1]) ||
                !char.IsAsciiHexDigit(value[index + 2]))
            {
                return true;
            }

            index += 2;
        }

        return false;
    }

    private static string EncodeBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] DecodeBase64Url(string value)
    {
        try
        {
            var normalized = value.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
            return Convert.FromBase64String(normalized);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Embedded profile payload is not valid base64url data.", ex);
        }
    }

    private async Task<string> MigrateCurrentConfigurationAsync(CancellationToken cancellationToken)
    {
        var existing = await _configurationService.LoadAsync(cancellationToken);
        var existingProfiles = await ReadUserProfilesAsync(cancellationToken);
        var name = existingProfiles.Any(profile =>
            string.Equals(profile.Summary.Name, "Current", StringComparison.CurrentCultureIgnoreCase))
            ? NextRecoveryProfileName(existingProfiles)
            : "Current";
        var id = await CreateUniqueProfileIdAsync(name, cancellationToken);
        var document = new StoredProfileDocument(
            ProfileStoreSchemaVersion,
            id,
            name,
            name == "Current"
                ? "Migrated from the existing config.json."
                : "Recovered from config.json after the active profile pointer became unavailable.",
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
            try
            {
                await using var stream = File.OpenRead(path);
                var document = await JsonSerializer.DeserializeAsync<StoredProfileDocument>(stream, JsonOptions, cancellationToken);
                if (TryNormalizeStoredProfileDocument(document, out var normalized))
                {
                    profiles.Add(ToProfile(normalized, activeId));
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
            {
                System.Diagnostics.Debug.WriteLine($"LocalProfileService: skipping malformed profile {path}: {ex.Message}");
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

        try
        {
            await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var document = await JsonSerializer.DeserializeAsync<StoredProfileDocument>(stream, JsonOptions, cancellationToken);
            return TryNormalizeStoredProfileDocument(document, out var normalized) ? normalized : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine($"LocalProfileService: profile {path} is unreadable: {ex.Message}");
            return null;
        }
    }

    private async Task WriteUserProfileDocumentAsync(StoredProfileDocument document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_profileDirectory);
        var path = UserProfilePath(document.Id);
        var tempPath = $"{path}.{Environment.ProcessId}.tmp";
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            throw;
        }
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

        try
        {
            await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var pointer = await JsonSerializer.DeserializeAsync<ProfilePointerDocument>(stream, JsonOptions, cancellationToken);
            if (pointer is null ||
                pointer.SchemaVersion != ProfileStoreSchemaVersion ||
                string.IsNullOrWhiteSpace(pointer.ProfileId) ||
                !string.Equals(pointer.ProfileId, Slugify(pointer.ProfileId), StringComparison.Ordinal))
            {
                return null;
            }

            return pointer;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine($"LocalProfileService: ignoring malformed pointer {path}: {ex.Message}");
            return null;
        }
    }

    private static async Task WritePointerAsync(string path, string profileId, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $"pointer.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, new ProfilePointerDocument(ProfileStoreSchemaVersion, profileId, DateTimeOffset.UtcNow), JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            throw;
        }
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

    private static bool TryNormalizeStoredProfileDocument(StoredProfileDocument? document, out StoredProfileDocument normalized)
    {
        normalized = default!;
        if (document is null || document.SchemaVersion != ProfileStoreSchemaVersion)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(document.Id) ||
            IsBuiltInId(document.Id) ||
            !string.Equals(document.Id, Slugify(document.Id), StringComparison.Ordinal))
        {
            return false;
        }

        if (document.Configuration is null)
        {
            return false;
        }

        normalized = document with
        {
            Name = NormalizeProfileName(document.Name),
            Description = (document.Description ?? string.Empty).Trim()
        };
        return true;
    }

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

    private static InstallConfiguration SanitizedConfiguration(InstallConfiguration configuration, bool redactShareProvenance = false)
    {
        var normalized = AppCatalog.NormalizeConfiguration(configuration).Clone();
        normalized.RiskAcknowledged = false;
        if (redactShareProvenance)
        {
            normalized.SpotX_CustomPatchesSourceUrl = RedactShareableUrl(normalized.SpotX_CustomPatchesSourceUrl);
        }

        return normalized;
    }

    private static string RedactShareableUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri.ToString();
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

    private static string NextRecoveryProfileName(IReadOnlyList<LocalProfile> profiles)
    {
        const string baseName = "Recovered Current";
        var names = profiles
            .Select(profile => profile.Summary.Name)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        if (!names.Contains(baseName))
        {
            return baseName;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseName} {suffix.ToString(CultureInfo.InvariantCulture)}";
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }
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

    // BuiltInProfiles() clones six full configurations per call; ID checks sit
    // on the profile hot path (uniqueness loops call this per suffix attempt),
    // so cache just the IDs once.
    private static readonly Lazy<HashSet<string>> BuiltInIds = new(() =>
        BuiltInProfiles().Select(profile => profile.Summary.Id).ToHashSet(StringComparer.OrdinalIgnoreCase));

    private static bool IsBuiltInId(string id) => BuiltInIds.Value.Contains(id);
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
        typeof(LocalProfileService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(LocalProfileService).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

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

    private sealed record ProfileActivationTransactionDocument(
        int SchemaVersion,
        string TransactionId,
        string OldProfileId,
        string NewProfileId,
        string? PreviousProfileId,
        bool OldConfigExisted,
        string OldConfigFingerprint,
        string NewConfigFingerprint,
        string OldConfigStageFile,
        string NewConfigStageFile,
        DateTimeOffset StartedAt);
}
