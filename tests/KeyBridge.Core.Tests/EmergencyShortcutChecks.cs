using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KeyBridge.Core;

internal static class EmergencyShortcutChecks
{
    public static void Register(Action<string, Action> test)
    {
        test("Recovery default avoids F12 without prohibiting F12 rule sources", () =>
        {
            var profile = AppProfile.CreateDefault();
            Check(profile.EmergencyPause == new KeyChord(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F10));
            Check(RuleCompiler.Compile(profile).Success);
            foreach (var modifiers in new[] { KeyModifiers.Control, KeyModifiers.Control | KeyModifiers.Alt, KeyModifiers.Shift })
                Reject(profile with { EmergencyPause = new(modifiers, KeyCode.F12) }, "EmergencyPause");
            Check(RuleCompiler.Compile(new AppProfile { Rules = [Rule() with { Source = new(KeyModifiers.Command, KeyCode.F12) }] }).Success);
        });
        test("Built-in IME shortcuts cannot become recovery hotkeys even with zero rules", () =>
        {
            foreach (var chord in new KeyChord[] { new(KeyModifiers.Control, KeyCode.Space),
                new(KeyModifiers.Shift, KeyCode.Space), new(KeyModifiers.Control, KeyCode.OemPeriod), new(KeyModifiers.Windows, KeyCode.Space) })
            foreach (var rules in new List<RuleDefinition>[] { [], [Rule()] })
                Reject(new AppProfile { EmergencyPause = chord, Rules = rules }, "EmergencyImeConflict");
        });
        test("Recovery protection handles sided custom chords and later protection changes", () =>
        {
            var profile = new AppProfile
            {
                EmergencyPause = new(KeyModifiers.Control, KeyCode.Q),
                ImePolicy = new() { AdditionalProtectedShortcuts = [new(KeyModifiers.RightControl, KeyCode.Q)] }
            };
            Reject(profile, "EmergencyImeConflict");
            Check(RuleCompiler.Compile(profile with { ImePolicy = new() }).Success);
            Reject(new AppProfile { ImePolicy = new() { AdditionalProtectedShortcuts = [EmergencyShortcuts.Default] } }, "EmergencyImeConflict");
        });
        test("Protected emergency imports are rejected and saved originals remain intact", () => WithStore((store, directory) =>
        {
            var unsafeProfile = new AppProfile { EmergencyPause = new(KeyModifiers.Control, KeyCode.Space) };
            var json = RawJson(unsafeProfile);
            Throws(() => ProfileStore.Deserialize(Encoding.UTF8.GetBytes(json)));
            File.WriteAllText(store.ProfilePath, json);
            var loaded = store.LoadAsync().GetAwaiter().GetResult();
            Check(!loaded.Loaded && loaded.RecoveryRequired && !loaded.Profile.Enabled);
            Check(File.ReadAllText(store.ProfilePath) == json);
            Throws(() => store.ImportAsync(store.ProfilePath).GetAwaiter().GetResult());
            Throws(() => store.SaveAsync(unsafeProfile).GetAwaiter().GetResult());
            Check(File.ReadAllText(store.ProfilePath) == json && Directory.GetFiles(directory, "*.tmp").Length == 0);
        }));
        test("Old default recovery migrates paused while preserving rules and the original backup", () => WithStore((store, _) =>
        {
            var original = new AppProfile
            {
                Enabled = true, ConsentGranted = true, RoleKeyConfirmed = true, RecoveryTested = true, StartAtLogin = true,
                EmergencyPause = new(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F12), Rules = [Rule()], ExcludedApps = ["other.exe"]
            };
            var json = RawJson(original);
            File.WriteAllText(store.ProfilePath, json);
            var loaded = store.LoadAsync().GetAwaiter().GetResult();
            Check(loaded.Loaded && loaded.RecoveryRequired && !loaded.Profile.Enabled && !loaded.Profile.RecoveryTested);
            Check(loaded.Profile.ConsentGranted && loaded.Profile.RoleKeyConfirmed && loaded.Profile.StartAtLogin);
            Check(loaded.Profile.EmergencyPause == EmergencyShortcuts.Default && loaded.Profile.ExcludedApps.SequenceEqual(original.ExcludedApps));
            Check(loaded.Profile.Rules.Single().Enabled && loaded.Profile.Rules.Single().Verification.VerifiedContexts.SequenceEqual([VerifiedContext.EnglishKeyboard]));
            Check(loaded.Message.Contains("F12", StringComparison.Ordinal) && loaded.Message.Contains("F10", StringComparison.Ordinal));
            Check(File.ReadAllText(store.ProfilePath) == json);
            store.SaveAsync(loaded.Profile).GetAwaiter().GetResult();
            Check(File.ReadAllText(store.ProfilePath + ".backup") == json);
            var imported = store.ImportAsync(store.ProfilePath + ".backup").GetAwaiter().GetResult();
            Check(!imported.Enabled && !imported.RecoveryTested && !imported.ConsentGranted && !imported.RoleKeyConfirmed);
            Check(imported.Rules.All(rule => !rule.Enabled && rule.Verification.VerifiedContexts.Count == 0));
        }));
        test("Legacy migration chooses a shortcut compatible with rules and protected input", () =>
        {
            var original = new AppProfile
            {
                EmergencyPause = new(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F12),
                Rules = [Rule() with { Source = new(KeyModifiers.LeftControl | KeyModifiers.LeftAlt, KeyCode.F10) }],
                ImePolicy = new() { AdditionalProtectedShortcuts = [new(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F11)] }
            };
            var migrated = ProfileStore.Deserialize(Encoding.UTF8.GetBytes(RawJson(original)));
            Check(migrated.EmergencyPause == new KeyChord(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F9));
            Check(!migrated.Enabled && !migrated.RecoveryTested && migrated.Rules.Single().Source == original.Rules.Single().Source);
            Check(RuleCompiler.Compile(migrated).Success);
        });
        test("Legacy migration cannot bypass unrelated validation or exhausted recovery choices", () =>
        {
            var original = new AppProfile { EmergencyPause = new(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F12) };
            Throws(() => ProfileStore.Deserialize(Encoding.UTF8.GetBytes(RawJson(original with { SchemaVersion = 99 }))));
            Throws(() => ProfileStore.Deserialize(Encoding.UTF8.GetBytes(RawJson(original with { Enabled = true }))));
            Throws(() => ProfileStore.Deserialize(Encoding.UTF8.GetBytes(RawJson(original with { EmergencyPause = new(KeyModifiers.Control, KeyCode.F12) }))));
            var blocked = original with
            {
                ImePolicy = new() { AdditionalProtectedShortcuts = Enumerable.Range((int)KeyCode.F1, 11)
                    .Select(key => new KeyChord(KeyModifiers.Control | KeyModifiers.Alt, (KeyCode)key)).ToList() }
            };
            Throws(() => ProfileStore.Deserialize(Encoding.UTF8.GetBytes(RawJson(blocked))));
        });
    }

    private static RuleDefinition Rule() => new()
    {
        Id = "copy", Intent = "Copy", Source = new(KeyModifiers.Command, KeyCode.C),
        Target = new(KeyModifiers.Control, KeyCode.C), Scope = new() { ProcessName = "editor.exe" }, Enabled = true,
        Verification = new() { ApplicationVersion = "1.0", WindowsBuild = "10.0.26100.0", VerifiedContexts = [VerifiedContext.EnglishKeyboard] }
    };

    private static string RawJson(AppProfile profile)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Serialize(profile, options);
    }

    private static void Reject(AppProfile profile, string code) =>
        Check(RuleCompiler.Compile(profile).Issues.Any(issue => issue.Code == code), "Expected " + code);
    private static void Check(bool condition, string message = "Assertion failed.")
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Throws(Action action)
    {
        try { action(); }
        catch (ProfileValidationException) { return; }
        throw new InvalidOperationException("Expected configuration rejection.");
    }
    private static void WithStore(Action<ProfileStore, string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "KeyBridgeRecoveryTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            action(new ProfileStore(directory), directory);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
