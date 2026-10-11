using System.Text.Json.Serialization;

namespace KeyBridge.Core;

public enum RuleOrigin { UserApplication, ApplicationPreset, UserGlobal }
public enum ContextPolicy { VerifiedImeCompatibility, RequiresNoComposition }
public enum InputSource { Unknown, EnglishKeyboard, MicrosoftPinyin, Other }
public enum ImeMode { Unknown, NotApplicable, Chinese, English }
public enum CompositionState { Unknown, NotComposing, Composing }
public enum InputWidth { Unknown, HalfWidth, FullWidth }
public enum PunctuationMode { Unknown, English, Chinese }
public enum PermissionState { Unknown, Allowed, Denied }
public enum SwitchAction { None, CycleInputSource, TogglePinyinMode }
public enum VerifiedContext { EnglishKeyboard, PinyinChineseIdle, PinyinEnglishIdle, PinyinUnknown, PinyinComposing, OtherIme }

public sealed record ImeState(InputSource Source = InputSource.Unknown, ImeMode Mode = ImeMode.Unknown,
    CompositionState Composition = CompositionState.Unknown, InputWidth Width = InputWidth.Unknown,
    PunctuationMode Punctuation = PunctuationMode.Unknown);

public sealed record ForegroundContext(string ProcessName, PermissionState Permission, ImeState Ime,
    string VersionToken = "", bool IsKnown = true, string WindowsBuild = "");

public sealed record RuleScope
{
    public string ProcessName { get; init; } = "";
}

public sealed record VerificationEvidence
{
    public string ApplicationVersion { get; init; } = "";
    public string WindowsBuild { get; init; } = "";
    public string Notes { get; init; } = "";
    public List<VerifiedContext> VerifiedContexts { get; init; } = [];
}

public sealed record RuleDefinition
{
    [JsonRequired] public string Id { get; init; } = "";
    [JsonRequired] public string Intent { get; init; } = "";
    [JsonRequired] public KeyChord Source { get; init; } = new(KeyModifiers.None, KeyCode.None);
    [JsonRequired] public KeyChord Target { get; init; } = new(KeyModifiers.None, KeyCode.None);
    public RuleScope Scope { get; init; } = new();
    public ContextPolicy ContextPolicy { get; init; } = ContextPolicy.VerifiedImeCompatibility;
    public RuleOrigin Origin { get; init; } = RuleOrigin.UserApplication;
    public bool Enabled { get; init; }
    public VerificationEvidence Verification { get; init; } = new();
}

public sealed record ImePolicy
{
    public bool PreserveNativeSwitching { get; init; } = true;
    public SwitchAction SwitchAction { get; init; } = SwitchAction.None;
    public List<KeyChord> AdditionalProtectedShortcuts { get; init; } = [];
}

public sealed record AppProfile
{
    [JsonRequired] public int SchemaVersion { get; init; } = 1;
    [JsonRequired] public string ProfileName { get; init; } = "Windows keyboard";
    [JsonRequired] public KeyCode CommandKey { get; init; } = KeyCode.LeftAlt;
    [JsonRequired] public bool Enabled { get; init; }
    [JsonRequired] public bool StartAtLogin { get; init; }
    [JsonRequired] public KeyChord EmergencyPause { get; init; } = EmergencyShortcuts.Default;
    public bool ConsentGranted { get; init; }
    public bool RoleKeyConfirmed { get; init; }
    public bool RecoveryTested { get; init; }
    public bool PreviousSessionClean { get; init; } = true;
    [JsonRequired] public ImePolicy ImePolicy { get; init; } = new();
    [JsonRequired] public List<RuleDefinition> Rules { get; init; } = [];
    [JsonRequired] public List<string> ExcludedApps { get; init; } = [];

    public static AppProfile CreateDefault() => new() { Rules = PresetCatalog.CreateSuggestedRules().ToList() };
}
