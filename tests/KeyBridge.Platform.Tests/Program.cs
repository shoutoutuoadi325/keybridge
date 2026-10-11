using System.Runtime.InteropServices;
using KeyBridge.App.Platform;
using KeyBridge.Core;

// No hook installation or SendInput invocation: these tests exercise the real pure planner.
var passed = 0;
void Check(bool assertion, string name)
{
    if (!assertion) throw new InvalidOperationException(name);
    passed++;
}

ModifierLifecycle AltShift()
{
    var state = new ModifierLifecycle();
    state.Seed(KeyCode.LeftAlt, true);
    state.Seed(KeyCode.RightShift, true);
    state.ObservePhysical(KeyCode.Z, true, false);
    return state;
}

var simple = new ModifierLifecycle();
simple.Seed(KeyCode.LeftAlt, true);
simple.ObservePhysical(KeyCode.C, true, false);
var copy = simple.Plan(new(KeyModifiers.Control, KeyCode.C))!;
Check(copy.SequenceEqual(new SyntheticKeyEvent[]
{
    new(KeyCode.LeftControl, false), new(KeyCode.LeftAlt, true), new(KeyCode.C, false),
    new(KeyCode.C, true), new(KeyCode.LeftAlt, false), new(KeyCode.LeftControl, true)
}), "Ctrl is added before native Alt release; native Alt restored before owned Ctrl release.");
simple.AcceptInserted(copy);
Check(!simple.HasOwnedKeys && simple.IsNativeDown(KeyCode.LeftAlt), "Complete batch leaves native Alt and no synthetic ownership.");
Check(simple.PlanRecovery().Count == 0, "Pause after complete batch never releases native Alt.");

var shift = AltShift();
var shifted = shift.Plan(new(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.Z))!;
Check(!shifted.Any(e => e.Key is KeyCode.LeftShift or KeyCode.RightShift), "Borrowed right Shift is preserved for generic Shift target.");
shift.AcceptInserted(shifted);
Check(shift.IsNativeDown(KeyCode.RightShift) && !shift.HasOwnedKeys, "Shift is still physical/native after complete repeat.");
shift.AcceptInserted(shift.Plan(new(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.Z))!);
Check(shift.PlanRecovery().Count == 0, "Autorepeat creates no retained synthetic keys.");

var redo = AltShift().Plan(new(KeyModifiers.Control, KeyCode.Y))!;
Check(redo.Contains(new(KeyCode.RightShift, true)) && redo.Contains(new(KeyCode.RightShift, false)), "Unwanted source Shift is suspended and restored for Ctrl Y.");
for (var inserted = 0; inserted <= redo.Count; inserted++)
{
    var state = AltShift();
    state.AcceptInserted(redo.Take(inserted));
    var cleanup = state.PlanRecovery();
    Check(!cleanup.Any(e => e.IsUp && e.Key is KeyCode.LeftAlt or KeyCode.RightShift), $"Partial prefix {inserted}: recovery never releases physical modifiers.");
    state.AcceptInserted(cleanup);
    Check(!state.HasOwnedKeys && state.IsNativeDown(KeyCode.LeftAlt) && state.IsNativeDown(KeyCode.RightShift),
        $"Partial prefix {inserted}: cleanup restores borrowed modifiers and releases owned keys.");
}

var borrowedControl = new ModifierLifecycle();
borrowedControl.Seed(KeyCode.RightControl, true);
borrowedControl.ObservePhysical(KeyCode.C, true, false);
var borrowedPlan = borrowedControl.Plan(new(KeyModifiers.Control, KeyCode.C))!;
Check(borrowedPlan.Count == 2 && borrowedPlan.All(e => e.Key == KeyCode.C), "Generic Ctrl borrows physically held right Ctrl without a synthetic release.");
borrowedControl.AcceptInserted(borrowedPlan.Take(1));
Check(borrowedControl.PlanRecovery().SequenceEqual(new[] { new SyntheticKeyEvent(KeyCode.C, true) }),
    "A swallowed physical trigger is still app-owned when its synthetic target down is inserted.");
borrowedControl.AcceptInserted(borrowedControl.PlanRecovery());
Check(borrowedControl.IsNativeDown(KeyCode.RightControl), "Partial cleanup does not release borrowed physical Ctrl.");

var released = AltShift();
released.AcceptInserted(redo.Take(3));
released.ObservePhysical(KeyCode.LeftAlt, false, true);
released.ObservePhysical(KeyCode.RightShift, false, true);
var releaseCleanup = released.PlanRecovery();
Check(!releaseCleanup.Any(e => !e.IsUp && e.Key is KeyCode.LeftAlt or KeyCode.RightShift),
    "Recovery does not restore modifiers already released physically.");
released.AcceptInserted(releaseCleanup);
Check(!released.HasOwnedKeys, "Ownership clears after release-order recovery.");

var heldTarget = new ModifierLifecycle();
heldTarget.Seed(KeyCode.Y, true);
Check(heldTarget.Plan(new(KeyModifiers.Control, KeyCode.Y)) is null, "A physically forwarded target key is never synthetically released.");
Check(simple.Plan(new(KeyModifiers.None, KeyCode.Home)) is null, "Unverified optional navigation target is rejected by first-release planner.");
Check(simple.Plan(new(KeyModifiers.Windows, KeyCode.Space)) is null, "Input-source switching stays unavailable.");
Check(simple.Plan(new(KeyModifiers.Control | KeyModifiers.LeftAlt, KeyCode.C)) is null,
    "The emission planner rejects Alt-bearing targets consistently with Core validation.");
Check(simple.Plan(new(KeyModifiers.Control | KeyModifiers.LeftWindows, KeyCode.C)) is null,
    "The emission planner rejects Windows-bearing targets consistently with Core validation.");
Check(Marshal.SizeOf<NativeMethods.Input>() == (IntPtr.Size == 8 ? 40 : 28), "Native INPUT union has documented x64/x86 size.");
var ownInput = NativeMethods.Key((int)KeyCode.RightControl, true);
Check(ownInput.Data.Keyboard.ExtraInfo == NativeMethods.InputTag && ownInput.Data.Keyboard.Flags == 3,
    "Own injections are tagged and right Ctrl release is extended.");
var unverifiedCopy = new RuleDefinition
{
    Id = "trial-copy", Intent = "Copy", Scope = new() { ProcessName = "keybridge.exe" },
    Source = new(KeyModifiers.Command, KeyCode.C), Target = new(KeyModifiers.Control, KeyCode.C)
};
var trialProfile = new AppProfile
{
    ConsentGranted = true, RoleKeyConfirmed = true, RecoveryTested = true,
    Rules = [unverifiedCopy, unverifiedCopy with { Id = "external-copy", Scope = new() { ProcessName = "msedge.exe" } }]
};
var localTrial = LocalTrialRules.Create(trialProfile)!;
Check(localTrial.Count == 1, "Temporary trial accepts only rules scoped to KeyBridge.");
Check(localTrial.Match(new(KeyModifiers.LeftAlt, KeyCode.C), true, InputSource.EnglishKeyboard) is not null,
    "Explicit local trial can test a disabled rule without fabricated evidence.");
Check(localTrial.Match(new(KeyModifiers.LeftAlt, KeyCode.C), false, InputSource.EnglishKeyboard) is null,
    "Trial never transforms outside the exact owning foreground process.");
Check(localTrial.Match(new(KeyModifiers.LeftAlt, KeyCode.C), true, InputSource.MicrosoftPinyin) is null,
    "Trial preserves Pinyin and unknown composition.");
Check(!trialProfile.Enabled && !unverifiedCopy.Enabled && unverifiedCopy.Verification.VerifiedContexts.Count == 0,
    "Creating a trial never marks rules enabled or creates verification evidence.");
var ambiguousTrial = trialProfile with { Rules = [unverifiedCopy, unverifiedCopy with
    { Id = "ambiguous", Target = new(KeyModifiers.Control, KeyCode.V) }] };
Check(LocalTrialRules.Create(ambiguousTrial) is null, "Even disabled ambiguous local-trial sources are rejected.");
var protectedTrial = trialProfile with { CommandKey = KeyCode.LeftWindows,
    Rules = [unverifiedCopy with { Source = new(KeyModifiers.Command, KeyCode.L) }] };
Check(LocalTrialRules.Create(protectedTrial) is null, "Local trial cannot bypass Windows reserved shortcut validation.");
var externalTrial = ApplicationRuleTrial.Create(trialProfile, "external-copy")!;
var edgeContext = new ForegroundContext("msedge.exe", PermissionState.Allowed,
    new(InputSource.EnglishKeyboard, ImeMode.NotApplicable, CompositionState.NotComposing));
Check(externalTrial.Match(new(KeyModifiers.LeftAlt, KeyCode.C), edgeContext) is not null,
    "One deliberate external trial works without fabricated version or verification evidence.");
Check(externalTrial.Match(new(KeyModifiers.LeftAlt, KeyCode.V), edgeContext) is null,
    "External trial transforms only the explicitly selected source rule.");
Check(externalTrial.Match(new(KeyModifiers.LeftAlt, KeyCode.C), edgeContext with { ProcessName = "code.exe" }) is null,
    "External trial never matches a different executable scope.");
Check(externalTrial.Match(new(KeyModifiers.LeftAlt, KeyCode.C), edgeContext with { Permission = PermissionState.Denied }) is null,
    "External trial cannot bypass the higher-integrity window boundary.");
Check(externalTrial.Match(new(KeyModifiers.LeftAlt, KeyCode.C), edgeContext with { Ime = new(InputSource.MicrosoftPinyin) }) is null,
    "External trial preserves unknown Pinyin mode and composition.");
var versionedProfile = trialProfile with { Rules = [trialProfile.Rules[1] with
    { Verification = new() { ApplicationVersion = "1.2.3" } }] };
var versionedTrial = ApplicationRuleTrial.Create(versionedProfile, "external-copy")!;
Check(versionedTrial.Match(new(KeyModifiers.LeftAlt, KeyCode.C), edgeContext with { VersionToken = "1.2.3" }) is not null
    && versionedTrial.Match(new(KeyModifiers.LeftAlt, KeyCode.C), edgeContext with { VersionToken = "1.2.4" }) is null,
    "External trial honors an explicitly supplied application version.");
Check(ApplicationRuleTrial.Create(trialProfile, "missing") is null, "A missing selection cannot fall back to another rule.");
Check(ApplicationRuleTrial.Create(trialProfile with { ExcludedApps = ["msedge.exe"] }, "external-copy") is null,
    "External trial cannot override an excluded app.");
var globalProfile = trialProfile with { Rules = [unverifiedCopy with
    { Id = "global", Scope = new(), Origin = RuleOrigin.UserGlobal }] };
Check(ApplicationRuleTrial.Create(globalProfile, "global") is null, "Global application trials are rejected.");
Check(LocalTrialRules.Create(trialProfile with { ExcludedApps = ["KeyBridge.exe"] }) is null,
    "Even an own-editor trial preserves explicit application exclusions.");
Check(!trialProfile.Enabled && trialProfile.Rules.All(rule => !rule.Enabled && rule.Verification.VerifiedContexts.Count == 0),
    "Selecting an external trial leaves profile enabled state and verification evidence unchanged.");
var injected = new ModifierLifecycle();
injected.ObserveExternalNative(KeyCode.Y, true);
Check(injected.Plan(new(KeyModifiers.Control, KeyCode.Y)) is null && !injected.IsPhysicalDown(KeyCode.Y),
    "External injected target down is native-only and cannot be released by a mapped chord.");
injected.ObserveExternalNative(KeyCode.Y, false);
Check(injected.Plan(new(KeyModifiers.Control, KeyCode.Y)) is not null,
    "An external injected target up clears transient native state without permanently blocking future rules.");
injected.Seed(KeyCode.LeftAlt, true);
injected.ObserveExternalNative(KeyCode.LeftAlt, false);
Check(injected.PlanRecovery().Count == 0, "Recovery never restores a modifier released by another tool; it restores only app-suspended native modifiers.");

// Exercise source suppression with the actual planner, including the case where
// the synthetic target up clears native state for the still-held source key.
foreach (var targetKey in new[] { KeyCode.C, KeyCode.Y })
{
    var physical = new ModifierLifecycle();
    var sourcePress = new SourcePressLifecycle();
    physical.Seed(KeyCode.LeftAlt, true);
    physical.ObservePhysical(KeyCode.C, true, false);
    sourcePress.Suppress(KeyCode.C);
    physical.AcceptInserted(physical.Plan(new(KeyModifiers.Control, targetKey))!);
    Check(physical.IsPhysicalDown(KeyCode.C) && !physical.IsNativeDown(KeyCode.C)
        && sourcePress.HasPendingRelease,
        $"Target {targetKey}: native source up after conversion cannot finish physical drain.");
    Check(sourcePress.Observe(KeyCode.C, isUp: false, isInjected: false)
        && sourcePress.HasPendingRelease,
        $"Target {targetKey}: a held physical source repeat stays suppressed during pause.");
    physical.ObserveExternalNative(KeyCode.C, false);
    Check(!sourcePress.Observe(KeyCode.C, isUp: true, isInjected: true)
        && sourcePress.IsSuppressed(KeyCode.C) && physical.IsPhysicalDown(KeyCode.C),
        $"Target {targetKey}: external injected up passes through without releasing physical suppression.");
    Check(!sourcePress.Observe(targetKey, isUp: true, isInjected: true)
        && sourcePress.HasPendingRelease,
        $"Target {targetKey}: own synthetic target up cannot finish physical drain.");
    Check(sourcePress.Observe(KeyCode.C, isUp: false, isInjected: false),
        $"Target {targetKey}: source repeat remains suppressed after injected releases.");
    Check(sourcePress.Observe(KeyCode.C, isUp: true, isInjected: false)
        && !sourcePress.HasPendingRelease,
        $"Target {targetKey}: a real physical up is consumed and completes the drain.");
    Check(!sourcePress.Observe(KeyCode.C, isUp: false, isInjected: false),
        $"Target {targetKey}: the next physical press can pass natively after pause.");
}
var multipleSources = new SourcePressLifecycle();
multipleSources.Suppress(KeyCode.C);
multipleSources.Suppress(KeyCode.Z);
Check(multipleSources.Observe(KeyCode.C, isUp: true, isInjected: false)
    && multipleSources.HasPendingRelease && multipleSources.IsSuppressed(KeyCode.Z),
    "Releasing one mapped source keeps the drain active for the other source.");
Check(!multipleSources.Observe(KeyCode.Y, isUp: true, isInjected: false)
    && multipleSources.HasPendingRelease,
    "An unrelated physical release cannot unblock a pending mapped source.");
Check(multipleSources.Observe(KeyCode.Z, isUp: false, isInjected: false)
    && multipleSources.Observe(KeyCode.Z, isUp: true, isInjected: false)
    && !multipleSources.HasPendingRelease,
    "Pressing and releasing a source after a missed release completes paused recovery.");
Console.WriteLine($"Passed {passed} keyboard lifecycle and native structure checks. Real Windows compatibility is not claimed.");
