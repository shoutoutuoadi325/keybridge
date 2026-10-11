namespace KeyBridge.Core;

/// <summary>Suggested combinations, never a claim that an application has been tested.</summary>
public static class PresetCatalog
{
    public static AppProfile CreateDefault() => AppProfile.CreateDefault();
    public static IEnumerable<RuleDefinition> CreateSuggestedRules() =>
        CreateRules("keybridge.exe").Concat(CreateRules("msedge.exe")).Concat(CreateRules("chrome.exe")).Concat(CreateRules("Code.exe"));

    public static IReadOnlyList<RuleDefinition> CreateRules(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName) || !processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || processName.Contains('/') || processName.Contains('\\')) throw new ArgumentException("An executable file name is required.", nameof(processName));
        var rules = new List<RuleDefinition>();
        void Add(string intent, KeyCode key, KeyModifiers sourceExtra = KeyModifiers.None,
            KeyModifiers targetExtra = KeyModifiers.None, KeyCode? targetKey = null)
        {
            rules.Add(new()
            {
                Id = $"{processName.ToLowerInvariant()}-{intent.ToLowerInvariant()}", Intent = intent,
                Source = new(KeyModifiers.Command | sourceExtra, key), Target = new(KeyModifiers.LeftControl | targetExtra, targetKey ?? key),
                Scope = new() { ProcessName = processName }, Origin = RuleOrigin.ApplicationPreset,
                Enabled = false, Verification = new()
            });
        }
        Add("Copy", KeyCode.C); Add("Paste", KeyCode.V); Add("Cut", KeyCode.X);
        Add("Select all", KeyCode.A); Add("Undo", KeyCode.Z);
        if (string.Equals(processName, "keybridge.exe", StringComparison.OrdinalIgnoreCase))
        {
            Add("Redo", KeyCode.Z, KeyModifiers.Shift, targetKey: KeyCode.Y);
            return rules.AsReadOnly();
        }
        Add("Save", KeyCode.S); Add("Find", KeyCode.F);
        var editor = string.Equals(processName, "Code.exe", StringComparison.OrdinalIgnoreCase);
        Add("Redo", KeyCode.Z, KeyModifiers.Shift, editor ? KeyModifiers.LeftShift : KeyModifiers.None, editor ? KeyCode.Z : KeyCode.Y);
        if (string.Equals(processName, "msedge.exe", StringComparison.OrdinalIgnoreCase) || string.Equals(processName, "chrome.exe", StringComparison.OrdinalIgnoreCase))
        {
            Add("New tab", KeyCode.T); Add("Close tab", KeyCode.W);
            Add("Reopen tab", KeyCode.T, KeyModifiers.Shift, KeyModifiers.LeftShift);
            Add("Address bar", KeyCode.L);
        }
        return rules.AsReadOnly();
    }
}

public static class KeyChordParser
{
    public static bool TryParse(string? text, out KeyChord chord)
    {
        chord = new(KeyModifiers.None, KeyCode.None);
        if (string.IsNullOrWhiteSpace(text) || text.Length > 160) return false;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length > 9 || parts.Any(string.IsNullOrEmpty)) return false;
        var modifiers = KeyModifiers.None;
        for (var index = 0; index < parts.Length - 1; index++)
        {
            var token = parts[index].ToUpperInvariant() switch
            {
                "CTRL" => "Control", "WIN" => "Windows", "CMD" => "Command", _ => parts[index]
            };
            if (!Enum.TryParse<KeyModifiers>(token, true, out var modifier) || !Enum.IsDefined(modifier)
                || modifier == KeyModifiers.None || (modifiers & modifier) != 0) return false;
            modifiers |= modifier;
        }
        if (!Enum.TryParse<KeyCode>(parts[^1], true, out var key) || !Enum.IsDefined(key)) return false;
        chord = new(modifiers, key);
        return RuleCompiler.ValidChord(chord, true);
    }
}
