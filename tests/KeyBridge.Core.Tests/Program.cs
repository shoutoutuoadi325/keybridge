using System.Text;
using KeyBridge.Core;

var tests = new List<(string Name, Action Run)>();
void Test(string name, Action run) => tests.Add((name, run));
void Check(bool value, string message = "Assertion failed.") { if (!value) throw new InvalidOperationException(message); }
void Reject(AppProfile profile, string code) => Check(RuleCompiler.Compile(profile).Issues.Any(i => i.Code == code), "Expected " + code);
void Throws(Action run) { try { run(); } catch (Exception ex) when (ex is ProfileValidationException or System.Text.Json.JsonException) { return; } throw new InvalidOperationException("Expected configuration rejection."); }
RuleDefinition Rule(string id = "copy", KeyChord? source = null, KeyChord? target = null) => new()
{
    Id = id, Intent = "Copy", Source = source ?? new(KeyModifiers.Command, KeyCode.C),
    Target = target ?? new(KeyModifiers.LeftControl, KeyCode.C), Scope = new() { ProcessName = "editor.exe" },
    Origin = RuleOrigin.UserApplication, Enabled = true,
    Verification = new() { ApplicationVersion = "1.0", WindowsBuild = "10.0.26100.0", VerifiedContexts = [VerifiedContext.EnglishKeyboard] }
};
AppProfile Profile(params RuleDefinition[] rules) => new()
{
    Enabled = true, ConsentGranted = true, RoleKeyConfirmed = true, RecoveryTested = true, Rules = rules.ToList()
};
ForegroundContext Context(ImeState? ime = null) => new("editor.exe", PermissionState.Allowed,
    ime ?? new(InputSource.EnglishKeyboard, ImeMode.NotApplicable, CompositionState.NotComposing),
    "1.0", WindowsBuild: "10.0.26100.0");
KeyChord Physical() => new(KeyModifiers.LeftAlt, KeyCode.C);
MatchResult Match(AppProfile profile, ForegroundContext? context = null, KeyChord? physical = null)
{
    var compilation = RuleCompiler.Compile(profile);
    Check(compilation.Success, string.Join("; ", compilation.Issues.Select(i => i.Message)));
    return compilation.Snapshot!.Match(physical ?? Physical(), context ?? Context());
}

Test("Default presets are disabled and unverified", () =>
{
    var p = PresetCatalog.CreateDefault();
    Check(!p.Enabled && !p.StartAtLogin && !p.ConsentGranted);
    Check(p.Rules.Count > 20 && p.Rules.All(r => !r.Enabled && r.Verification.VerifiedContexts.Count == 0));
    Check(RuleCompiler.Compile(p).Success);
});
Test("Exact physical modifier matching", () =>
{
    var p = Profile(Rule()); Check(Match(p).ShouldTransform);
    Check(!Match(p, physical: new(KeyModifiers.LeftAlt | KeyModifiers.LeftShift, KeyCode.C)).ShouldTransform);
    Check(!Match(p, physical: new(KeyModifiers.RightAlt, KeyCode.C)).ShouldTransform);
    Check(!Match(p, physical: new(KeyModifiers.LeftControl, KeyCode.C)).ShouldTransform);
});
Test("Generic Shift accepts either side without accepting extra modifiers", () =>
{
    var rule = Rule(source: new(KeyModifiers.Command | KeyModifiers.Shift, KeyCode.Z), target: new(KeyModifiers.LeftControl, KeyCode.Y));
    Check(Match(Profile(rule), physical: new(KeyModifiers.LeftAlt | KeyModifiers.RightShift, KeyCode.Z)).ShouldTransform);
    Check(!Match(Profile(rule), physical: new(KeyModifiers.LeftAlt | KeyModifiers.RightShift | KeyModifiers.LeftControl, KeyCode.Z)).ShouldTransform);
});
Test("Unknown permissions and elevated windows pass through", () =>
{
    Check(!Match(Profile(Rule()), Context() with { Permission = PermissionState.Unknown }).ShouldTransform);
    Check(!Match(Profile(Rule()), Context() with { Permission = PermissionState.Denied }).ShouldTransform);
});
Test("Unknown foreground and input sources pass through", () =>
{
    Check(!Match(Profile(Rule()), Context() with { IsKnown = false }).ShouldTransform);
    Check(!Match(Profile(Rule()), Context(new())).ShouldTransform);
});
Test("Microsoft Pinyin states require distinct verification", () =>
{
    var states = new[] { new ImeState(InputSource.MicrosoftPinyin, ImeMode.Chinese, CompositionState.NotComposing),
        new(InputSource.MicrosoftPinyin, ImeMode.English, CompositionState.NotComposing),
        new(InputSource.MicrosoftPinyin, ImeMode.Unknown, CompositionState.Unknown),
        new(InputSource.MicrosoftPinyin, ImeMode.Chinese, CompositionState.Composing) };
    foreach (var state in states) Check(!Match(Profile(Rule()), Context(state)).ShouldTransform);
    var pinyinRule = Rule() with { Verification = Rule().Verification with { VerifiedContexts = [VerifiedContext.PinyinEnglishIdle] } };
    Check(Match(Profile(pinyinRule), Context(states[1])).ShouldTransform);
    Check(!Match(Profile(pinyinRule), Context(states[0])).ShouldTransform);
});
Test("Composition-dependent sources never execute during active or unknown composition", () =>
{
    var rule = Rule(source: new(KeyModifiers.Command, KeyCode.Left), target: new(KeyModifiers.LeftControl, KeyCode.A)) with
    {
        ContextPolicy = ContextPolicy.RequiresNoComposition,
        Verification = Rule().Verification with { VerifiedContexts = [VerifiedContext.PinyinUnknown, VerifiedContext.PinyinComposing] }
    };
    Check(!Match(Profile(rule), Context(new(InputSource.MicrosoftPinyin)), new(KeyModifiers.LeftAlt, KeyCode.Left)).ShouldTransform);
    Check(!Match(Profile(rule), Context(new(InputSource.MicrosoftPinyin, ImeMode.Chinese, CompositionState.Composing)), new(KeyModifiers.LeftAlt, KeyCode.Left)).ShouldTransform);
});
Test("Excluded applications are matched without case sensitivity", () => Check(!Match(Profile(Rule()) with { ExcludedApps = ["EDITOR.EXE"] }).ShouldTransform));
Test("Application and Windows version changes invalidate local evidence", () =>
{
    Check(!Match(Profile(Rule()), Context() with { VersionToken = "2.0" }).ShouldTransform);
    Check(!Match(Profile(Rule()), Context() with { WindowsBuild = "10.0.26200.0" }).ShouldTransform);
});
Test("Changing the physical Command role invalidates affected rule evidence", () =>
{
    var original = Profile(Rule() with { Verification = Rule().Verification with { Notes = "Previous Alt source trial." } });
    Check(Match(original).ShouldTransform);
    foreach (var key in new[] { KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftWindows, KeyCode.RightWindows })
    {
        var changed = ProfileChanges.WithCommandKey(original, key);
        Check(changed.CommandKey == key && !changed.Enabled && !changed.RoleKeyConfirmed);
        Check(changed.ConsentGranted && changed.RecoveryTested);
        var rule = changed.Rules.Single();
        Check(!rule.Enabled && !ReferenceEquals(rule.Verification, original.Rules[0].Verification));
        Check(rule.Verification.VerifiedContexts.Count == 0 && rule.Verification.Notes.Length == 0
            && rule.Verification.ApplicationVersion.Length == 0 && rule.Verification.WindowsBuild.Length == 0);
        // Physical role confirmation alone cannot revive a rule tested with a different source.
        var resumed = changed with { Enabled = true, RoleKeyConfirmed = true };
        Check(!Match(resumed, physical: new(KeyChord.ModifierFor(key), KeyCode.C)).ShouldTransform);
        Reject(resumed with { Rules = [rule with { Enabled = true }] }, "UnverifiedRule");
    }
    Check(original.CommandKey == KeyCode.LeftAlt && original.Enabled && original.RoleKeyConfirmed
        && original.Rules[0].Enabled && original.Rules[0].Verification.Notes == "Previous Alt source trial.");
});
Test("Changing Command role preserves unrelated explicit physical sources", () =>
{
    var explicitSource = Rule("explicit", source: new(KeyModifiers.RightControl, KeyCode.V), target: new(KeyModifiers.Control, KeyCode.C));
    var original = Profile(Rule(), explicitSource);
    var changed = ProfileChanges.WithCommandKey(original, KeyCode.LeftWindows);
    Check(!changed.Rules[0].Enabled && changed.Rules[1] == explicitSource && changed.Rules[1].Enabled);
    Check(changed.Rules[1].Verification == explicitSource.Verification);
    Check(Match(changed with { Enabled = true, RoleKeyConfirmed = true }, physical: explicitSource.Source).ShouldTransform);
});
Test("Changing back cannot restore stale Command-alias evidence", () =>
{
    var original = Profile(Rule());
    var changed = ProfileChanges.WithCommandKey(original, KeyCode.LeftControl);
    var restoredRole = ProfileChanges.WithCommandKey(changed, KeyCode.LeftAlt);
    Check(restoredRole.CommandKey == KeyCode.LeftAlt && !restoredRole.Enabled && !restoredRole.RoleKeyConfirmed);
    Check(!restoredRole.Rules[0].Enabled && restoredRole.Rules[0].Verification.VerifiedContexts.Count == 0);
    Check(!Match(restoredRole with { Enabled = true, RoleKeyConfirmed = true }).ShouldTransform);
    Check(ProfileChanges.WithCommandKey(original, original.CommandKey) == original);
});
Test("User application overrides outrank preset rules", () =>
{
    var user = Rule("user", target: new(KeyModifiers.LeftControl, KeyCode.V));
    var preset = Rule("preset") with { Origin = RuleOrigin.ApplicationPreset };
    Check(Match(Profile(preset, user)).Rule!.Id == "user");
});
Test("Duplicate equal priority rules are rejected", () => Reject(Profile(Rule("one"), Rule("two")), "AmbiguousRules"));
Test("Generic and sided overlapping sources are rejected", () =>
{
    var a = Rule("one", new(KeyModifiers.Command | KeyModifiers.Shift, KeyCode.Z));
    var b = Rule("two", new(KeyModifiers.Command | KeyModifiers.LeftShift, KeyCode.Z));
    Reject(Profile(a, b), "AmbiguousRules");
});
Test("System reserved source and target shortcuts are rejected", () =>
{
    Reject(Profile(Rule(source: new(KeyModifiers.LeftWindows, KeyCode.L))), "ReservedShortcut");
    Reject(Profile(Rule(target: new(KeyModifiers.LeftControl | KeyModifiers.LeftAlt, KeyCode.Delete))), "ReservedShortcut");
    Reject(Profile(Rule(target: new(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.Escape))), "ReservedShortcut");
});
Test("Recovery source and target shortcuts are rejected", () =>
{
    Reject(Profile(Rule(source: new(KeyModifiers.LeftControl | KeyModifiers.LeftAlt, KeyCode.F10))), "RecoveryConflict");
    Reject(Profile(Rule(target: new(KeyModifiers.Control | KeyModifiers.LeftAlt, KeyCode.F10))), "RecoveryConflict");
});
Test("Emergency registration cannot claim side-specific modifier behavior", () =>
    Reject(Profile(Rule()) with { EmergencyPause = new(KeyModifiers.LeftControl | KeyModifiers.LeftAlt, KeyCode.F10) }, "EmergencyPause"));
Test("Known and user IME shortcut conflicts check both sides", () =>
{
    Reject(Profile(Rule(target: new(KeyModifiers.LeftControl, KeyCode.Space))), "ImeConflict");
    Reject(Profile(Rule(source: new(KeyModifiers.LeftWindows, KeyCode.Space))), "ImeConflict");
    Reject(Profile(Rule()) with { ImePolicy = new() { AdditionalProtectedShortcuts = [new(KeyModifiers.LeftAlt, KeyCode.C)] } }, "ImeConflict");
});
Test("AltGr, modifier aliases and single keys are protected", () =>
{
    Reject(Profile(Rule(source: new(KeyModifiers.RightAlt, KeyCode.C))), "AltGrProtection");
    Reject(Profile(Rule(source: new(KeyModifiers.Alt | KeyModifiers.Command, KeyCode.C))), "RoleModifierConflict");
    Reject(Profile(Rule(source: new(KeyModifiers.None, KeyCode.A))), "UnmodifiedSource");
    Reject(Profile(Rule(target: new(KeyModifiers.Command, KeyCode.C))), "InvalidChord");
});
Test("Enabled unverified or global rules are rejected", () =>
{
    Reject(Profile(Rule() with { Verification = new() }), "UnverifiedRule");
    Reject(Profile(Rule() with { Origin = RuleOrigin.UserGlobal, Scope = new() }), "GlobalVerification");
});
Test("Consent, role confirmation and recovery trial are required", () =>
{
    Reject(Profile(Rule()) with { ConsentGranted = false }, "SetupIncomplete");
    Reject(Profile(Rule()) with { RoleKeyConfirmed = false }, "SetupIncomplete");
    Reject(Profile(Rule()) with { RecoveryTested = false }, "SetupIncomplete");
});
Test("Rule snapshot does not change when editable profile lists change", () =>
{
    var p = Profile(Rule()); var snapshot = RuleCompiler.Compile(p).Snapshot!;
    p.Rules[0].Verification.VerifiedContexts.Clear(); p.Rules.Clear(); p.ExcludedApps.Add("editor.exe");
    Check(snapshot.Match(Physical(), Context()).ShouldTransform);
});
Test("Configuration limits and illegal key codes are enforced", () =>
{
    Reject(Profile(Rule(target: new(KeyModifiers.LeftControl, (KeyCode)999))), "InvalidChord");
    Reject(Profile(Rule()) with { ProfileName = new string('x', 81) }, "ProfileName");
    Reject(Profile(Rule()) with { Rules = Enumerable.Range(0, 257).Select(i => Rule("r" + i)).ToList() }, "CollectionLimit");
});
Test("Enabled targets must match the runtime Ctrl letter scope", () =>
{
    var unsupported = new[]
    {
        new KeyChord(KeyModifiers.None, KeyCode.Home), new(KeyModifiers.LeftControl, KeyCode.F1),
        new(KeyModifiers.LeftShift, KeyCode.C), new(KeyModifiers.LeftControl | KeyModifiers.LeftAlt, KeyCode.C),
        new(KeyModifiers.LeftControl | KeyModifiers.LeftWindows, KeyCode.D)
    };
    var options = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
    options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    foreach (var target in unsupported)
    {
        var rule = Rule(target: target) with { ContextPolicy = ContextPolicy.RequiresNoComposition };
        Reject(Profile(rule), "UnsupportedTarget");
        Check(!ShortcutSupport.IsSupportedTarget(target));
        // A hand-edited current profile is validated on load; valid-looking evidence
        // cannot make a target executable when the actual engine does not support it.
        var json = System.Text.Json.JsonSerializer.Serialize(Profile(rule), options);
        Throws(() => ProfileStore.Deserialize(Encoding.UTF8.GetBytes(json)));
    }
    foreach (var target in new[] { new KeyChord(KeyModifiers.Control, KeyCode.C),
        new(KeyModifiers.RightControl | KeyModifiers.RightShift, KeyCode.Z),
        new(KeyModifiers.LeftControl | KeyModifiers.Shift, KeyCode.Y) })
    {
        Check(ShortcutSupport.IsSupportedTarget(target));
        Check(Match(Profile(Rule(target: target))).ShouldTransform);
    }
});
Test("Disabled future targets remain representable without being eligible", () =>
{
    var future = Rule(target: new(KeyModifiers.None, KeyCode.Home)) with
    { Enabled = false, ContextPolicy = ContextPolicy.RequiresNoComposition, Verification = new() };
    var proposal = Profile(future) with { Enabled = false };
    Check(RuleCompiler.Compile(proposal).Success);
    var roundtrip = ProfileStore.Deserialize(Encoding.UTF8.GetBytes(ProfileStore.Serialize(proposal)));
    Check(!roundtrip.Rules[0].Enabled && roundtrip.Rules[0].Target == future.Target);
    Check(!Match(roundtrip).ShouldTransform);
    var defaults = PresetCatalog.CreateDefault();
    Check(RuleCompiler.Compile(defaults).Success && defaults.Rules.All(rule => !rule.Enabled));
});
Test("JSON schema rejects unknown, duplicate and missing properties", () =>
{
    var json = ProfileStore.Serialize(PresetCatalog.CreateDefault());
    Throws(() => ProfileStore.Deserialize(Encoding.UTF8.GetBytes(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"command\": \"execute\""))));
    Throws(() => ProfileStore.Deserialize(Encoding.UTF8.GetBytes(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1"))));
    Throws(() => ProfileStore.Deserialize(Encoding.UTF8.GetBytes(json.Replace("\"schemaVersion\": 1,", ""))));
    Throws(() => ProfileStore.Deserialize(Encoding.UTF8.GetBytes(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"))));
});
Test("JSON rejects null members, invalid enums and excess bytes", () =>
{
    var json = ProfileStore.Serialize(new AppProfile());
    Throws(() => ProfileStore.Deserialize(Encoding.UTF8.GetBytes(json.Replace("\"rules\": []", "\"rules\": null"))));
    Throws(() => ProfileStore.Deserialize(Encoding.UTF8.GetBytes(json.Replace("\"LeftAlt\"", "999"))));
    Throws(() => ProfileStore.Deserialize(new byte[ProfileStore.MaximumFileBytes + 1]));
});
Test("Parser round trips supported shortcuts and rejects ambiguous input", () =>
{
    Check(KeyChordParser.TryParse("Command + Shift + Z", out var chord));
    Check(chord == new KeyChord(KeyModifiers.Command | KeyModifiers.Shift, KeyCode.Z));
    Check(!KeyChordParser.TryParse("Control + LeftControl + C", out _));
    Check(!KeyChordParser.TryParse("Ctrl + + C", out _));
    Check(!KeyChordParser.TryParse("Ctrl + LeftShift", out _));
});
Test("Atomic stores keep prior settings and safely load after crash", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "KeyBridgeCoreTests-" + Guid.NewGuid().ToString("N"));
    try
    {
        var store = new ProfileStore(dir);
        store.SaveAsync(Profile(Rule())).GetAwaiter().GetResult();
        store.SaveAsync(Profile(Rule()) with { PreviousSessionClean = false }).GetAwaiter().GetResult();
        Check(File.Exists(store.ProfilePath + ".backup"));
        var load = store.LoadAsync().GetAwaiter().GetResult();
        Check(load.Loaded && load.RecoveryRequired && !load.Profile.Enabled && !load.Profile.RecoveryTested);
        var original = File.ReadAllText(store.ProfilePath);
        Throws(() => store.SaveAsync(Profile(Rule()) with { SchemaVersion = 99 }).GetAwaiter().GetResult());
        Check(File.ReadAllText(store.ProfilePath) == original);
        Check(Directory.GetFiles(dir, "*.tmp").Length == 0);
        File.WriteAllText(store.ProfilePath, "broken");
        Check(store.LoadAsync().GetAwaiter().GetResult().RecoveryRequired);
        Check(File.ReadAllText(store.ProfilePath) == "broken");
    }
    finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
});
Test("Imports reset active rules and require local re-verification", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "KeyBridgeCoreTests-" + Guid.NewGuid().ToString("N"));
    try
    {
        var store = new ProfileStore(dir); var path = Path.Combine(dir, "export.json");
        store.ExportAsync(path, Profile(Rule())).GetAwaiter().GetResult();
        var p = store.ImportAsync(path).GetAwaiter().GetResult();
        Check(!p.Enabled && !p.StartAtLogin && !p.ConsentGranted && !p.RecoveryTested);
        Check(p.Rules.All(r => !r.Enabled && r.Verification.VerifiedContexts.Count == 0));
        Check(RuleCompiler.Compile(p).Success);
    }
    finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
});
Test("Rejected configuration preservation is explicit, faithful and bounded", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "KeyBridgeCoreTests-" + Guid.NewGuid().ToString("N"));
    try
    {
        Directory.CreateDirectory(dir);
        var store = new ProfileStore(dir + Path.DirectorySeparatorChar);
        const string rejected = "{\"schemaVersion\":99,\"unrecognized\":\"original must remain intact\"}";
        File.WriteAllText(store.ProfilePath, rejected);
        Check(!store.LoadAsync().GetAwaiter().GetResult().Loaded);
        Check(Directory.GetFiles(dir, "profile.rejected-*.json").Length == 0);
        var unrelated = Path.Combine(dir, "profile.rejected-unrelated.json");
        File.WriteAllText(unrelated, "User file; not a managed snapshot.");
        var copies = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var preserved = store.PreserveRejectedAsync().GetAwaiter().GetResult();
            Check(preserved is not null && File.ReadAllText(preserved) == rejected);
            copies.Add(preserved!);
            Check(File.ReadAllText(store.ProfilePath) == rejected);
        }
        Check(copies.Count(File.Exists) == ProfileStore.MaximumRejectedBackups);
        Check(File.Exists(unrelated));
        Check(Directory.GetFiles(dir, "*.tmp").Length == 0);
        using (var oversized = new FileStream(store.ProfilePath, FileMode.Create, FileAccess.Write))
            oversized.SetLength(ProfileStore.MaximumRejectedFileBytes + 1);
        Throws(() => store.PreserveRejectedAsync().GetAwaiter().GetResult());
        Check(new FileInfo(store.ProfilePath).Length == ProfileStore.MaximumRejectedFileBytes + 1);
        Check(copies.Count(File.Exists) == ProfileStore.MaximumRejectedBackups);
    }
    finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
});

RuleEditorChecks.Register(Test);
EmergencyShortcutChecks.Register(Test);
ProfileOperationChecks.Register(Test);

var failures = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
}
Console.WriteLine($"{tests.Count - failures}/{tests.Count} core tests passed.");
return failures == 0 ? 0 : 1;
