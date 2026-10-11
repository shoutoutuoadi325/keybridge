using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyBridge.Core;

public sealed record ProfileLoadResult(AppProfile Profile, bool Loaded, bool RecoveryRequired, string Message);
public sealed class ProfileValidationException(string message, IReadOnlyList<ValidationIssue>? issues = null) : Exception(message)
{
    public IReadOnlyList<ValidationIssue> Issues { get; } = issues ?? [];
}

/// <summary>Bounded, strictly typed local data storage. Imports never activate mapping.</summary>
public sealed class ProfileStore
{
    public const int MaximumFileBytes = 256 * 1024;
    public const int MaximumRejectedFileBytes = 1024 * 1024;
    public const int MaximumRejectedBackups = 3;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        MaxDepth = 16,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    private readonly SemaphoreSlim gate = new(1, 1);
    public string DirectoryPath { get; }
    public string ProfilePath { get; }

    public ProfileStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        DirectoryPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        ProfilePath = Path.Combine(DirectoryPath, "profile.json");
    }

    public async Task<ProfileLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(ProfilePath)) return new(AppProfile.CreateDefault(), false, false, "Welcome. Mapping remains paused until setup is complete.");
            var loaded = await ReadValidatedAsync(ProfilePath, cancellationToken);
            var profile = loaded.Profile;
            var recoveryMessage = loaded.EmergencyChanged
                ? $"The previous default recovery shortcut used F12, which Windows reserves for debugging. Your rules are preserved. Press {profile.EmergencyPause} to confirm the replacement before resuming. "
                : "";
            if (!profile.PreviousSessionClean)
                return new(profile with { Enabled = false, RecoveryTested = false }, true, true,
                    recoveryMessage + "The previous session did not exit cleanly. Release and press any affected modifier, then test recovery before resuming.");
            // Every launch requires a deliberate resume; a stored pause is never automatically discarded.
            return new(profile with { Enabled = false }, true, loaded.EmergencyChanged,
                recoveryMessage + "Settings loaded. Mapping is paused until you resume.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ProfileValidationException)
        {
            return new(AppProfile.CreateDefault(), false, true, "The saved configuration could not be validated. Mapping is safely paused; the original file is retained.");
        }
        finally { gate.Release(); }
    }

    public async Task SaveAsync(AppProfile profile, CancellationToken cancellationToken = default)
    {
        Validate(profile);
        await gate.WaitAsync(cancellationToken);
        try { await AtomicWriteAsync(ProfilePath, profile, keepBackup: true, cancellationToken); }
        finally { gate.Release(); }
    }

    /// <summary>
    /// Explicitly preserves rejected settings before a user-requested reset or import.
    /// This method never validates, activates, modifies, or deletes the original profile.
    /// </summary>
    public async Task<string?> PreserveRejectedAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(ProfilePath)) return null;
            var name = $"profile.rejected-{DateTime.UtcNow:yyyyMMddTHHmmssfffffffZ}-{Guid.NewGuid():N}.json";
            var preserved = Path.Combine(DirectoryPath, name);
            var temporary = preserved + ".tmp";
            try
            {
                await using (var source = new FileStream(ProfilePath, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 4096, FileOptions.Asynchronous))
                {
                    if (source.Length > MaximumRejectedFileBytes)
                        throw new ProfileValidationException("The rejected configuration exceeds the 1 MiB preservation limit. Copy or move the original file manually before replacing it.");
                    await using var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                        FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
                    await source.CopyToAsync(destination, 4096, cancellationToken);
                    if (destination.Length > MaximumRejectedFileBytes)
                        throw new ProfileValidationException("The rejected configuration changed or exceeded the preservation limit.");
                    await destination.FlushAsync(cancellationToken);
                    destination.Flush(flushToDisk: true);
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, preserved);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            foreach (var stale in Directory.EnumerateFiles(DirectoryPath, "profile.rejected-*.json", SearchOption.TopDirectoryOnly)
                .Where(path => IsRejectedBackupPath(path) && !string.Equals(path, preserved, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                .Skip(MaximumRejectedBackups - 1))
                File.Delete(stale);
            return preserved;
        }
        finally { gate.Release(); }
    }

    private bool IsRejectedBackupPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(fullPath), DirectoryPath, StringComparison.OrdinalIgnoreCase)) return false;
        var name = Path.GetFileName(fullPath);
        const string prefix = "profile.rejected-";
        const string suffix = ".json";
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.Ordinal)) return false;
        var identity = name[prefix.Length..^suffix.Length];
        var separator = identity.IndexOf('-');
        return separator == 23 && identity[..separator].All(c => char.IsAsciiDigit(c) || c is 'T' or 'Z')
            && Guid.TryParseExact(identity[(separator + 1)..], "N", out _);
    }

    public async Task<AppProfile> ImportAsync(string path, CancellationToken cancellationToken = default)
    {
        var imported = (await ReadValidatedAsync(path, cancellationToken)).Profile;
        return imported with
        {
            Enabled = false, StartAtLogin = false, ConsentGranted = false, RoleKeyConfirmed = false,
            RecoveryTested = false, PreviousSessionClean = true,
            Rules = imported.Rules.Select(rule => rule with
            {
                Enabled = false,
                Verification = rule.Verification with { VerifiedContexts = [] }
            }).ToList()
        };
    }

    public async Task ExportAsync(string path, AppProfile profile, CancellationToken cancellationToken = default)
    {
        Validate(profile);
        await gate.WaitAsync(cancellationToken);
        try { await AtomicWriteAsync(Path.GetFullPath(path), profile, keepBackup: false, cancellationToken); }
        finally { gate.Release(); }
    }

    public static string Serialize(AppProfile profile)
    {
        Validate(profile);
        var result = JsonSerializer.Serialize(profile, JsonOptions);
        if (System.Text.Encoding.UTF8.GetByteCount(result) > MaximumFileBytes)
            throw new ProfileValidationException("The configuration exceeds the file-size limit.");
        return result;
    }

    public static AppProfile Deserialize(ReadOnlySpan<byte> bytes) => DeserializeWithRecovery(bytes).Profile;

    private static (AppProfile Profile, bool EmergencyChanged) DeserializeWithRecovery(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is 0 or > MaximumFileBytes) throw new ProfileValidationException("The configuration file size is invalid.");
        RejectDuplicateProperties(bytes);
        var result = JsonSerializer.Deserialize<AppProfile>(bytes, JsonOptions)
            ?? throw new ProfileValidationException("The configuration must be a JSON object.");
        // Migrate only the exact previous default in an otherwise valid profile. Do
        // not use migration to soften unrelated validation errors or grant setup.
        var compilation = RuleCompiler.Compile(result);
        if (result.EmergencyPause == EmergencyShortcuts.LegacyDefault
            && compilation.Issues.Count == 1 && compilation.Issues[0].Code == "EmergencyPause")
        {
            foreach (var chord in EmergencyShortcuts.MigrationCandidates)
            {
                var migrated = result with { EmergencyPause = chord, Enabled = false, RecoveryTested = false };
                if (RuleCompiler.Compile(migrated).Success) return (migrated, true);
            }
        }
        ThrowIfInvalid(compilation);
        return (result, false);
    }

    private static async Task<(AppProfile Profile, bool EmergencyChanged)> ReadValidatedAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (stream.Length is 0 or > MaximumFileBytes) throw new ProfileValidationException("The configuration file size is invalid.");
        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        if (stream.ReadByte() != -1) throw new ProfileValidationException("The configuration changed while it was being read.");
        return DeserializeWithRecovery(bytes);
    }

    private static async Task AtomicWriteAsync(string path, AppProfile profile, bool keepBackup, CancellationToken cancellationToken)
    {
        var content = System.Text.Encoding.UTF8.GetBytes(Serialize(profile));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path)) File.Replace(temporary, path, keepBackup ? path + ".backup" : null, ignoreMetadataErrors: false);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void Validate(AppProfile profile) => ThrowIfInvalid(RuleCompiler.Compile(profile));

    private static void ThrowIfInvalid(CompilationResult compilation)
    {
        if (!compilation.Success) throw new ProfileValidationException("The configuration is invalid: "
            + string.Join(" ", compilation.Issues.Select(issue => issue.Message)), compilation.Issues);
    }

    private static void RejectDuplicateProperties(ReadOnlySpan<byte> bytes)
    {
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 16 });
        var objects = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject) objects.Push(new(StringComparer.Ordinal));
            else if (reader.TokenType == JsonTokenType.EndObject) objects.Pop();
            else if (reader.TokenType == JsonTokenType.PropertyName && !objects.Peek().Add(reader.GetString()!))
                throw new ProfileValidationException("Duplicate JSON properties are not accepted.");
        }
    }
}
