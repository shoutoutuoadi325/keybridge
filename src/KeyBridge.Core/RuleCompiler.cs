using System.Collections.ObjectModel;

namespace KeyBridge.Core;

public sealed record ValidationIssue(string Code, string Message, string? RuleId = null);
public sealed record CompilationResult(CompiledProfile? Snapshot, IReadOnlyList<ValidationIssue> Issues)
{
    public bool Success => Snapshot is not null && Issues.Count == 0;
}
public sealed record CompiledRule(string Id, string Intent, KeyChord Source, KeyChord Target,
    string ProcessName, ContextPolicy ContextPolicy, RuleOrigin Origin, string ApplicationVersion,
    string WindowsBuild, IReadOnlyList<VerifiedContext> VerifiedContexts);
public sealed record MatchResult(bool ShouldTransform, CompiledRule? Rule, KeyChord? Target, string Reason)
{
    public static MatchResult Pass(string reason) => new(false, null, null, reason);
}

public sealed class CompiledProfile
{
    private readonly HashSet<string> excluded;
    public KeyCode CommandKey { get; }
    public KeyChord EmergencyPause { get; }
    public IReadOnlyList<CompiledRule> Rules { get; }
    public bool Enabled { get; }
    internal CompiledProfile(AppProfile profile)
    {
        CommandKey = profile.CommandKey;
        EmergencyPause = profile.EmergencyPause;
        Enabled = profile.Enabled && profile.ConsentGranted && profile.RoleKeyConfirmed && profile.RecoveryTested;
        excluded = new(profile.ExcludedApps, StringComparer.OrdinalIgnoreCase);
        Rules = Array.AsReadOnly(profile.Rules.Where(r => r.Enabled).OrderBy(r => r.Origin)
            .Select(r => new CompiledRule(r.Id, r.Intent, r.Source.ResolveCommand(profile.CommandKey), r.Target,
                r.Scope.ProcessName, r.ContextPolicy, r.Origin, r.Verification.ApplicationVersion,
                r.Verification.WindowsBuild, Array.AsReadOnly(r.Verification.VerifiedContexts.ToArray()))).ToArray());
    }

    public MatchResult Match(KeyChord physical, ForegroundContext context)
    {
        if (EmergencyPause.Matches(physical)) return MatchResult.Pass("Emergency pause is reserved for recovery.");
        if (!Enabled) return MatchResult.Pass("Mapping is paused.");
        if (!context.IsKnown || string.IsNullOrWhiteSpace(context.ProcessName)) return MatchResult.Pass("The foreground application is unknown.");
        if (context.Permission != PermissionState.Allowed) return MatchResult.Pass("The foreground permission boundary could not be confirmed.");
        if (excluded.Contains(context.ProcessName)) return MatchResult.Pass("The application is excluded.");
        if (context.Ime.Source == InputSource.Unknown) return MatchResult.Pass("The foreground input source is unknown.");
        var rule = Rules.FirstOrDefault(r => r.Source.Matches(physical)
            && (r.ProcessName.Length == 0 || string.Equals(r.ProcessName, context.ProcessName, StringComparison.OrdinalIgnoreCase)));
        if (rule is null) return MatchResult.Pass("No rule matches this exact shortcut.");
        if (!string.Equals(rule.ApplicationVersion, context.VersionToken, StringComparison.Ordinal))
            return MatchResult.Pass("This application version has not been verified for this rule.");
        if (!string.Equals(rule.WindowsBuild, context.WindowsBuild, StringComparison.Ordinal))
            return MatchResult.Pass("This Windows build has not been verified for this rule.");
        if (rule.ContextPolicy == ContextPolicy.RequiresNoComposition && context.Ime.Composition != CompositionState.NotComposing)
            return MatchResult.Pass("Navigation is protected while composition is active or unknown.");
        var verifiedContext = context.Ime.Source switch
        {
            InputSource.EnglishKeyboard when context.Ime.Composition == CompositionState.NotComposing => VerifiedContext.EnglishKeyboard,
            InputSource.MicrosoftPinyin when context.Ime.Composition == CompositionState.Composing => VerifiedContext.PinyinComposing,
            InputSource.MicrosoftPinyin when context.Ime.Composition == CompositionState.NotComposing && context.Ime.Mode == ImeMode.Chinese => VerifiedContext.PinyinChineseIdle,
            InputSource.MicrosoftPinyin when context.Ime.Composition == CompositionState.NotComposing && context.Ime.Mode == ImeMode.English => VerifiedContext.PinyinEnglishIdle,
            InputSource.MicrosoftPinyin => VerifiedContext.PinyinUnknown,
            InputSource.Other => VerifiedContext.OtherIme,
            _ => (VerifiedContext?)null
        };
        if (verifiedContext is null || !rule.VerifiedContexts.Contains(verifiedContext.Value))
            return MatchResult.Pass("This application and input-method state have not been verified for this rule.");
        return new(true, rule, rule.Target, "The shortcut matches a locally verified rule.");
    }
}

public static class RuleCompiler
{
    public const int MaximumRules = 256;
    public const int MaximumApplications = 128;
    public const int MaximumTextLength = 512;
    private const KeyModifiers AllModifiers = (KeyModifiers)8191;
    private static readonly KeyChord[] ProtectedImeShortcuts =
    [
        new(KeyModifiers.Control, KeyCode.Space), new(KeyModifiers.Shift, KeyCode.Space),
        new(KeyModifiers.Control, KeyCode.OemPeriod), new(KeyModifiers.Windows, KeyCode.Space)
    ];

    public static CompilationResult Compile(AppProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var issues = new List<ValidationIssue>();
        void Error(string code, string message, string? id = null) => issues.Add(new(code, message, id));
        if (profile.SchemaVersion != 1) Error("UnsupportedVersion", "Only configuration schema version 1 is supported.");
        if (!ValidText(profile.ProfileName, 80, required: true)) Error("ProfileName", "The profile name must contain 1 to 80 printable characters.");
        if (profile.CommandKey is not (KeyCode.LeftAlt or KeyCode.LeftControl or KeyCode.RightControl or KeyCode.LeftWindows or KeyCode.RightWindows))
            Error("CommandKey", "Select a supported side-specific role key. Right Alt is protected because it may represent AltGr.");
        if (!ValidChord(profile.EmergencyPause, false) || profile.EmergencyPause.Modifiers == KeyModifiers.None
            || (profile.EmergencyPause.Modifiers & ~(KeyModifiers.Shift | KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Windows)) != 0
            || IsReserved(profile.EmergencyPause))
            Error("EmergencyPause", "The emergency shortcut must be a supported modified key and must not be reserved by Windows.");
        if (profile.Enabled && (!profile.ConsentGranted || !profile.RoleKeyConfirmed || !profile.RecoveryTested))
            Error("SetupIncomplete", "Consent, role-key confirmation, and recovery testing are required before enabling mapping.");
        if (profile.ImePolicy is null || profile.Rules is null || profile.ExcludedApps is null)
        {
            Error("MissingData", "Policy, rules, and exclusions must not be null.");
            return new(null, issues.AsReadOnly());
        }
        if (!profile.ImePolicy.PreserveNativeSwitching || profile.ImePolicy.SwitchAction != SwitchAction.None)
            Error("SwitchingUnavailable", "Use Windows input-method controls to switch languages.");
        if (profile.Rules.Count > MaximumRules || profile.ExcludedApps.Count > MaximumApplications)
            Error("CollectionLimit", "The configuration exceeds the rule or application limit.");
        if (profile.ImePolicy.AdditionalProtectedShortcuts is null || profile.ImePolicy.AdditionalProtectedShortcuts.Count > 32)
            Error("ImeProtection", "The IME protection list must contain at most 32 shortcuts.");
        else foreach (var chord in profile.ImePolicy.AdditionalProtectedShortcuts)
            if (!ValidChord(chord, false)) Error("ImeProtection", "An additional protected shortcut is invalid.");
        foreach (var app in profile.ExcludedApps)
            if (!ValidProcessName(app, false)) Error("ExcludedApplication", "Excluded applications must be executable file names without paths.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var validated = new List<RuleDefinition>();
        foreach (var rule in profile.Rules.Take(MaximumRules + 1))
        {
            if (rule is null) { Error("MissingRule", "Rules must not contain null entries."); continue; }
            if (!ValidText(rule.Id, 80, true) || !ids.Add(rule.Id)) Error("RuleId", "Rule identifiers must be nonempty and unique.", rule.Id);
            if (!ValidText(rule.Intent, 80, true)) Error("Intent", "An operation name must contain 1 to 80 printable characters.", rule.Id);
            if (!Enum.IsDefined(rule.ContextPolicy) || !Enum.IsDefined(rule.Origin)) Error("RulePolicy", "An unsupported rule policy or origin was supplied.", rule.Id);
            if (rule.Enabled && rule.Origin == RuleOrigin.UserGlobal)
                Error("GlobalVerification", "Choose an application for each enabled shortcut.", rule.Id);
            if (rule.Scope is null || !ValidProcessName(rule.Scope.ProcessName, rule.Origin == RuleOrigin.UserGlobal))
                Error("RuleScope", "Application rules require an executable file name; global rules require an empty scope.", rule.Id);
            else if (rule.Origin == RuleOrigin.UserGlobal && rule.Scope.ProcessName.Length > 0)
                Error("RuleScope", "Global rules must use an empty scope.", rule.Id);
            if (!ValidChord(rule.Source, true) || !ValidChord(rule.Target, false))
            { Error("InvalidChord", "Rules require a supported main key, valid modifier flags, and no target Command alias.", rule.Id); continue; }
            if (rule.Enabled && !ShortcutSupport.IsSupportedTarget(rule.Target))
                Error("UnsupportedTarget", "Choose a Ctrl + letter target, optionally with Shift, for each enabled shortcut.", rule.Id);
            var source = rule.Source.ResolveCommand(profile.CommandKey);
            if (!ValidChord(source, false)) Error("RoleModifierConflict", "The Command role duplicates an explicit modifier family in this source.", rule.Id);
            if (source.Modifiers == KeyModifiers.None) Error("UnmodifiedSource", "Single-key remapping is not supported.", rule.Id);
            if ((source.Modifiers & KeyModifiers.RightAlt) != 0 || (rule.Target.Modifiers & KeyModifiers.RightAlt) != 0
                || source.Modifiers.HasFlag(KeyModifiers.Alt) || rule.Target.Modifiers.HasFlag(KeyModifiers.Alt))
                Error("AltGrProtection", "Right Alt and generic Alt rules are protected against AltGr ambiguity. Use explicit Left Alt.", rule.Id);
            if (IsReserved(source) || IsReserved(rule.Target)) Error("ReservedShortcut", "Windows reserved shortcuts cannot be used as a source or target.", rule.Id);
            if (profile.EmergencyPause is not null && (source.Overlaps(profile.EmergencyPause) || rule.Target.Overlaps(profile.EmergencyPause)))
                Error("RecoveryConflict", "A rule cannot consume or emit the emergency pause shortcut.", rule.Id);
            var protectedShortcuts = ProtectedImeShortcuts.Concat(profile.ImePolicy.AdditionalProtectedShortcuts ?? []);
            if (protectedShortcuts.Any(c => c is not null && (source.Overlaps(c) || rule.Target.Overlaps(c))))
                Error("ImeConflict", "A source or target conflicts with a protected input-method shortcut.", rule.Id);
            if (SensitiveKeys.Contains(source.Key) || SensitiveKeys.Contains(rule.Target.Key))
                if (rule.ContextPolicy != ContextPolicy.RequiresNoComposition) Error("CompositionProtection", "Navigation and deletion rules must require confirmed idle composition.", rule.Id);
            var evidence = rule.Verification;
            if (evidence is null || !ValidText(evidence.ApplicationVersion, 128) || !ValidText(evidence.WindowsBuild, 128)
                || !ValidText(evidence.Notes, MaximumTextLength) || evidence.VerifiedContexts is null
                || evidence.VerifiedContexts.Count > 6 || evidence.VerifiedContexts.Any(c => !Enum.IsDefined(c))
                || evidence.VerifiedContexts.Distinct().Count() != evidence.VerifiedContexts.Count)
                Error("Verification", "Shortcut confirmation details are invalid or exceed their allowed length.", rule.Id);
            else if (rule.Enabled && (string.IsNullOrWhiteSpace(evidence.ApplicationVersion) || string.IsNullOrWhiteSpace(evidence.WindowsBuild)
                || evidence.VerifiedContexts.Count == 0))
                Error("UnverifiedRule", "Confirm the application version, Windows build and input source before enabling this shortcut.", rule.Id);
            validated.Add(rule);
        }
        for (var i = 0; i < validated.Count; i++)
        for (var j = i + 1; j < validated.Count; j++)
        {
            var a = validated[i]; var b = validated[j];
            if (a.Enabled && b.Enabled && a.Origin == b.Origin && a.Scope is not null && b.Scope is not null
                && string.Equals(a.Scope.ProcessName, b.Scope.ProcessName, StringComparison.OrdinalIgnoreCase)
                && a.Source.ResolveCommand(profile.CommandKey).Overlaps(b.Source.ResolveCommand(profile.CommandKey)))
                Error("AmbiguousRules", $"Rules '{a.Id}' and '{b.Id}' match the same source at the same priority.", a.Id);
        }
        return new(issues.Count == 0 ? new CompiledProfile(profile) : null, issues.AsReadOnly());
    }

    private static readonly HashSet<KeyCode> SensitiveKeys = [KeyCode.Backspace, KeyCode.Delete, KeyCode.Left, KeyCode.Right,
        KeyCode.Up, KeyCode.Down, KeyCode.Home, KeyCode.End, KeyCode.PageUp, KeyCode.PageDown, KeyCode.Enter, KeyCode.Escape, KeyCode.Space];

    public static bool IsReserved(KeyChord chord)
    {
        if (chord is null) return false;
        var m = chord.Modifiers;
        var win = (m & (KeyModifiers.Windows | KeyModifiers.LeftWindows | KeyModifiers.RightWindows)) != 0;
        var ctrl = (m & (KeyModifiers.Control | KeyModifiers.LeftControl | KeyModifiers.RightControl)) != 0;
        var alt = (m & (KeyModifiers.Alt | KeyModifiers.LeftAlt | KeyModifiers.RightAlt)) != 0;
        var shift = (m & (KeyModifiers.Shift | KeyModifiers.LeftShift | KeyModifiers.RightShift)) != 0;
        return (win && chord.Key == KeyCode.L) || (ctrl && alt && chord.Key == KeyCode.Delete)
            || (ctrl && shift && chord.Key == KeyCode.Escape);
    }

    public static bool ValidChord(KeyChord? chord, bool allowCommand)
    {
        if (chord is null || !Enum.IsDefined(chord.Key) || chord.Key == KeyCode.None || KeyChord.ModifierFor(chord.Key) != KeyModifiers.None
            || (chord.Modifiers & ~AllModifiers) != 0 || (!allowCommand && chord.Modifiers.HasFlag(KeyModifiers.Command))) return false;
        return !MixedGeneric(chord.Modifiers, KeyModifiers.Shift, KeyModifiers.LeftShift | KeyModifiers.RightShift)
            && !MixedGeneric(chord.Modifiers, KeyModifiers.Control, KeyModifiers.LeftControl | KeyModifiers.RightControl)
            && !MixedGeneric(chord.Modifiers, KeyModifiers.Alt, KeyModifiers.LeftAlt | KeyModifiers.RightAlt)
            && !MixedGeneric(chord.Modifiers, KeyModifiers.Windows, KeyModifiers.LeftWindows | KeyModifiers.RightWindows);
    }
    private static bool MixedGeneric(KeyModifiers value, KeyModifiers generic, KeyModifiers sides) => value.HasFlag(generic) && (value & sides) != 0;
    private static bool ValidText(string? value, int max, bool required = false) => value is not null && value.Length <= max
        && (!required || !string.IsNullOrWhiteSpace(value)) && !value.Any(char.IsControl);
    private static bool ValidProcessName(string? value, bool allowEmpty) => value is not null && ((allowEmpty && value.Length == 0)
        || (value.Length is > 4 and <= 128 && value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or ' ')));
}
