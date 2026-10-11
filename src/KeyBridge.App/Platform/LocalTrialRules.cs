using System;
using System.Collections.Generic;
using System.Linq;
using KeyBridge.Core;

namespace KeyBridge.App.Platform;

/// <summary>An ephemeral own-editor trial. Creates no verification evidence or persistent profile.</summary>
public sealed class LocalTrialRules
{
    private readonly IReadOnlyList<(KeyChord Source, KeyChord Target)> _rules;
    private LocalTrialRules(IReadOnlyList<(KeyChord, KeyChord)> rules) => _rules = rules;
    public int Count => _rules.Count;
    public static LocalTrialRules? Create(AppProfile profile)
    {
        if (!RuleCompiler.Compile(profile).Success || profile.ExcludedApps.Contains("keybridge.exe", StringComparer.OrdinalIgnoreCase)) return null;
        var rules = profile.Rules
            .Where(rule => string.Equals(rule.Scope.ProcessName, "keybridge.exe", StringComparison.OrdinalIgnoreCase)
                && rule.Target.Key is KeyCode.A or KeyCode.C or KeyCode.V or KeyCode.X or KeyCode.Z or KeyCode.Y or KeyCode.S or KeyCode.F
                && ShortcutSupport.IsSupportedTarget(rule.Target))
            .Select(rule => (Source: rule.Source.ResolveCommand(profile.CommandKey), Target: rule.Target))
            .Distinct().ToArray();
        for (var first = 0; first < rules.Length; first++)
        for (var second = first + 1; second < rules.Length; second++)
            if (rules[first].Source.Overlaps(rules[second].Source)) return null;
        return new(Array.AsReadOnly(rules));
    }
    public KeyChord? Match(KeyChord physical, bool isOwnForeground, InputSource source)
    {
        if (!isOwnForeground || source != InputSource.EnglishKeyboard) return null;
        return _rules.FirstOrDefault(rule => rule.Source.Matches(physical)).Target;
    }
}
