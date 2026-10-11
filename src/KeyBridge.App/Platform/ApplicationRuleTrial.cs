using System;
using System.Linq;
using KeyBridge.Core;

namespace KeyBridge.App.Platform;

/// <summary>Explicit, ephemeral testing of exactly one application rule, without compatibility evidence.</summary>
public sealed class ApplicationRuleTrial
{
    public KeyChord Source { get; }
    public KeyChord Target { get; }
    public string ProcessName { get; }
    public string ApplicationVersion { get; }
    private ApplicationRuleTrial(RuleDefinition rule, KeyCode command)
    {
        Source = rule.Source.ResolveCommand(command); Target = rule.Target;
        ProcessName = rule.Scope.ProcessName; ApplicationVersion = rule.Verification.ApplicationVersion;
    }
    public static ApplicationRuleTrial? Create(AppProfile profile, string ruleId)
    {
        if (!RuleCompiler.Compile(profile).Success) return null;
        var rule = profile.Rules.FirstOrDefault(item => string.Equals(item.Id, ruleId, StringComparison.Ordinal));
        if (rule is null || rule.Origin == RuleOrigin.UserGlobal || string.IsNullOrEmpty(rule.Scope.ProcessName)
            || profile.ExcludedApps.Contains(rule.Scope.ProcessName, StringComparer.OrdinalIgnoreCase)
            || !ShortcutSupport.IsSupportedTarget(rule.Target)) return null;
        return new(rule, profile.CommandKey);
    }
    public KeyChord? Match(KeyChord physical, ForegroundContext context)
    {
        if (!context.IsKnown || context.Permission != PermissionState.Allowed
            || context.Ime.Source != InputSource.EnglishKeyboard || context.Ime.Composition != CompositionState.NotComposing
            || !string.Equals(context.ProcessName, ProcessName, StringComparison.OrdinalIgnoreCase)
            || (ApplicationVersion.Length > 0 && !string.Equals(context.VersionToken, ApplicationVersion, StringComparison.Ordinal))
            || !Source.Matches(physical)) return null;
        return Target;
    }
}
