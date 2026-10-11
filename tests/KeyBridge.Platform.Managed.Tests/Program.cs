using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using KeyBridge.App.Platform;
using KeyBridge.Core;

var passed = 0;
void Check(bool assertion, string name)
{
    if (!assertion) throw new InvalidOperationException(name);
    passed++;
}
object? Field(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance);
object? Call(object instance, string name, params object[] arguments) => instance.GetType()
    .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, arguments);
void OnKeyboard(KeyboardService service, Action action)
{
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    Call(service, "Post", (Action)(() =>
    {
        try { action(); completed.TrySetResult(); }
        catch (Exception error) { completed.TrySetException(error); }
    }));
    if (!completed.Task.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Managed keyboard thread did not respond.");
    completed.Task.GetAwaiter().GetResult();
}
AppProfile Profile(KeyChord? emergency = null) => new()
{
    Enabled = true, ConsentGranted = true, RoleKeyConfirmed = true, RecoveryTested = true,
    EmergencyPause = emergency ?? EmergencyShortcuts.Default
};
KeyboardService RunningService()
{
    NativeMethods.ResetInput();
    var service = new KeyboardService(); // all thread/native dependencies are managed test stubs
    service.Configure(Profile());
    Check(service.Resume(), "Managed mapping starts after valid setup.");
    return service;
}
nint Physical(KeyboardService service, KeyCode key, bool up)
{
    var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.KeyboardHookData>());
    try
    {
        Marshal.StructureToPtr(new NativeMethods.KeyboardHookData { VirtualKey = (uint)key, Flags = up ? 0x80u : 0 }, pointer, false);
        return (nint)Call(service, "Hook", 0, (nuint)0, pointer)!;
    }
    finally { Marshal.FreeHGlobal(pointer); }
}
void PartialShortcut(KeyboardService service, KeyCode target, int initialCount, int recoveryCount)
{
    var keys = (ModifierLifecycle)Field(service, "_keys")!;
    var sources = (SourcePressLifecycle)Field(service, "_sourcePresses")!;
    keys.Seed(KeyCode.LeftAlt, true); NativeMethods.SeedNative(KeyCode.LeftAlt, true);
    keys.ObservePhysical(KeyCode.C, true, false);
    var plan = keys.Plan(new(KeyModifiers.Control, target))!;
    NativeMethods.InsertCounts.Enqueue(initialCount);
    Call(service, "Emit", plan);
    sources.Suppress(KeyCode.C);
    NativeMethods.InsertCounts.Enqueue(recoveryCount);
    Call(service, "PauseFromHook", "Paused after an incomplete shortcut.");
}

using (var service = RunningService())
{
    OnKeyboard(service, () => PartialShortcut(service, KeyCode.C, 1, 0));
    OnKeyboard(service, () => { }); // drain the queued stop, without retrying failed recovery
    var sends = NativeMethods.SendCalls;
    OnKeyboard(service, () => Check(Physical(service, KeyCode.C, up: true) == 1, "Converted physical source-up stays suppressed."));
    OnKeyboard(service, () =>
    {
        var keys = (ModifierLifecycle)Field(service, "_keys")!;
        Check(keys.HasOwnedKeys && keys.IsNativeDown(KeyCode.LeftControl) && keys.PlanRecovery().Count == 1,
            "Failed Ctrl release retains native state and ownership after source-up.");
        Check((bool)Field(service, "_draining")! && !service.IsRunning && service.Status.Contains("LeftControl", StringComparison.Ordinal),
            "Paused recovery stays blocked with specific physical recovery guidance.");
        Check(NativeMethods.SendCalls == sends, "Source-up never automatically retries failed injected cleanup.");
        Check(service.LastNativeError == 5, "Failed recovery retains its native failure diagnostic.");
    });
    var installs = NativeMethods.HookInstalls;
    NativeMethods.InsertCounts.Enqueue(0);
    Check(!service.Resume() && NativeMethods.HookInstalls == installs, "Explicit resume cannot replace a hook while cleanup remains unfinished.");
    sends = NativeMethods.SendCalls;
    OnKeyboard(service, () => Physical(service, KeyCode.LeftControl, up: false));
    OnKeyboard(service, () => Physical(service, KeyCode.LeftControl, up: true));
    OnKeyboard(service, () =>
    {
        Check(!(bool)Field(service, "_draining")! && !NativeMethods.IsNativeDown(KeyCode.LeftControl),
            "Physical Ctrl press/release safely resolves failed cleanup and completes drain.");
        Check(NativeMethods.SendCalls == sends, "Physical recovery takeover emits no additional synthetic input.");
    });
    Check(service.Resume(), "Resume works after physical recovery is complete.");
}

using (var service = RunningService())
{
    OnKeyboard(service, () => PartialShortcut(service, KeyCode.C, 2, 1));
    OnKeyboard(service, () => { });
    OnKeyboard(service, () => Physical(service, KeyCode.C, up: true));
    OnKeyboard(service, () =>
    {
        var keys = (ModifierLifecycle)Field(service, "_keys")!;
        Check(!keys.HasOwnedKeys && keys.PlanRecovery().SequenceEqual([new SyntheticKeyEvent(KeyCode.LeftAlt, false)]),
            "Partial cleanup retains suspended Alt restoration even with no owned keys.");
        Check((bool)Field(service, "_draining")! && service.Status.Contains("LeftAlt", StringComparison.Ordinal),
            "Suspended restoration blocks resume and identifies the affected modifier.");
    });
    var sends = NativeMethods.SendCalls;
    OnKeyboard(service, () => Physical(service, KeyCode.LeftAlt, up: true));
    OnKeyboard(service, () =>
    {
        Check(!(bool)Field(service, "_draining")! && !((ModifierLifecycle)Field(service, "_keys")!).HasOwnedKeys,
            "A real release cancels a pending restore of the physically released modifier.");
        Check(NativeMethods.SendCalls == sends, "Physical Alt release resolves restoration without synthetic replay.");
    });
}

foreach (var target in new[] { KeyCode.C, KeyCode.Y })
using (var service = RunningService())
{
    OnKeyboard(service, () => PartialShortcut(service, target, 3, 0));
    OnKeyboard(service, () => { });
    OnKeyboard(service, () => Physical(service, KeyCode.C, up: true));
    OnKeyboard(service, () =>
    {
        var keys = (ModifierLifecycle)Field(service, "_keys")!;
        Check(keys.HasOwnedKeys && keys.IsNativeDown(target) && keys.PlanRecovery().Any(item => item.Key == target && item.IsUp),
            $"Target {target}: source-up cannot discard an unreleased synthetic letter.");
    });
    OnKeyboard(service, () => Physical(service, target, up: false));
    OnKeyboard(service, () => Physical(service, target, up: true));
    OnKeyboard(service, () =>
        Check(!((ModifierLifecycle)Field(service, "_keys")!).IsNativeDown(target), $"Target {target}: a new physical letter press/release takes over native state."));
    // Dispose gets one explicit best-effort cleanup attempt for any remaining modifier.
}

using (var service = RunningService())
{
    OnKeyboard(service, () => PartialShortcut(service, KeyCode.C, 1, 0));
    OnKeyboard(service, () => { });
    OnKeyboard(service, () => Physical(service, KeyCode.C, up: true));
    var unhooks = NativeMethods.Unhooks;
    OnKeyboard(service, () =>
    {
        NativeMethods.InsertCounts.Enqueue(0);
        Call(service, "StopHook", true, true);
        Check(NativeMethods.Unhooks == unhooks + 1 && service.Status.Contains("unfinished keyboard recovery", StringComparison.Ordinal)
            && service.Status.Contains("LeftControl", StringComparison.Ordinal),
            "Force exit stops the hook but explicitly reports unsuccessful recovery and the affected key.");
        Check(NativeMethods.IsNativeDown(KeyCode.LeftControl), "Force exit does not falsely claim the failed native Ctrl-up succeeded.");
    });
}

using (var service = RunningService())
{
    var sends = NativeMethods.SendCalls;
    Check(service.PrepareForExit(), "Exit preparation acknowledges clean state after the keyboard thread stops mapping.");
    Check(!service.IsRunning && !(bool)Field(service, "_draining")! && NativeMethods.SendCalls == sends,
        "Clean exit preparation does not generate unnecessary recovery input.");
}

using (var service = RunningService())
{
    OnKeyboard(service, () => PartialShortcut(service, KeyCode.C, 1, 0));
    OnKeyboard(service, () => { });
    OnKeyboard(service, () => Physical(service, KeyCode.C, up: true));
    NativeMethods.InsertCounts.Enqueue(0);
    Check(!service.PrepareForExit(), "Exit preparation reports unresolved Ctrl ownership rather than marking the session clean.");
    OnKeyboard(service, () => Check(((ModifierLifecycle)Field(service, "_keys")!).HasOwnedKeys,
        "Unsuccessful exit acknowledgement retains recovery state until forced shutdown."));
    var sends = NativeMethods.SendCalls;
    service.Dispose();
    Check(NativeMethods.SendCalls == sends && service.Status.Contains("unfinished keyboard recovery", StringComparison.Ordinal),
        "Dispose after exit preparation does not silently retry injection and keeps unfinished recovery guidance.");
}

using (var service = RunningService())
{
    OnKeyboard(service, () => PartialShortcut(service, KeyCode.C, 1, 1));
    OnKeyboard(service, () => { });
    Check(!service.PrepareForExit(), "A still-held suppressed source prevents a clean exit acknowledgement even after owned Ctrl is released.");
    service.Dispose();
    Check(service.Status.Contains("Release all held shortcut keys", StringComparison.Ordinal),
        "Forced exit with a held mapped source gives explicit release guidance.");
}

var chordA = EmergencyShortcuts.Default;
var chordB = new KeyChord(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F11);
NativeMethods.Message HotKey(NativeMethods.Registration registration, KeyChord? chord = null) => new()
{
    Id = NativeMethods.WmHotKey, WParam = (nuint)registration.Id,
    LParam = (nint)(((uint)(chord?.Key ?? (KeyCode)registration.Key) << 16) | (registration.Modifiers & 0xF))
};
using (var emergency = new EmergencyHotKey())
{
    var events = new List<EmergencyPauseRegistration>();
    emergency.Triggered += events.Add;
    emergency.Configure(chordA);
    var nativeA = NativeMethods.Registrations.Last();
    Call(emergency, "HandleHotKey", HotKey(nativeA));
    var tokenA = events.Single();
    Check(tokenA.Chord == chordA && emergency.IsCurrent(tokenA), "A native hotkey event carries the active immutable registration/chord token.");
    var registrations = NativeMethods.Registrations.Count;
    emergency.Configure(chordA);
    Check(emergency.IsCurrent(tokenA) && NativeMethods.Registrations.Count == registrations,
        "Saving an unchanged successful recovery shortcut preserves its registration and confirmation.");
    emergency.Configure(chordB);
    var nativeB = NativeMethods.Registrations.Last();
    Check(!emergency.IsCurrent(tokenA) && nativeA.Id != nativeB.Id, "Changing the shortcut invalidates earlier confirmation and never reuses its native ID.");
    Call(emergency, "HandleHotKey", HotKey(nativeA));
    Call(emergency, "HandleHotKey", HotKey(nativeB, chordA));
    Check(events.Count == 1, "Queued old-ID events and wrong-chord lParam events cannot confirm the new shortcut.");
    Call(emergency, "HandleHotKey", HotKey(nativeB));
    var tokenB = events.Last();
    Check(tokenB.Id != tokenA.Id && tokenB.Chord == chordB && emergency.IsCurrent(tokenB), "The replacement shortcut produces its own valid token when actually exercised.");
    emergency.Configure(chordA);
    var secondA = NativeMethods.Registrations.Last();
    Call(emergency, "HandleHotKey", HotKey(nativeA));
    Check(secondA.Id != nativeA.Id && events.Count == 2 && !emergency.IsCurrent(tokenA),
        "Returning to a previous chord cannot revive its queued messages or previously captured token.");
    NativeMethods.RegistrationSucceeds = false;
    emergency.Configure(chordB);
    Check(!emergency.Available && !emergency.IsCurrent(tokenB) && emergency.LastNativeError == 1409,
        "Failed replacement registration leaves no available shortcut or valid old confirmation.");
    NativeMethods.RegistrationSucceeds = true;
}

using (var service = new KeyboardService())
{
    var events = new ConcurrentQueue<EmergencyPauseRegistration>();
    using var received = new ManualResetEventSlim();
    service.EmergencyPauseTriggered += registration => { events.Enqueue(registration); received.Set(); };
    service.Configure(Profile(chordA));
    var emergency = (EmergencyHotKey)Field(service, "_emergency")!;
    Call(emergency, "HandleHotKey", HotKey(NativeMethods.Registrations.Last()));
    Check(received.Wait(TimeSpan.FromSeconds(3)), "KeyboardService delivers a qualified recovery token across its asynchronous event hop.");
    var token = events.Single();
    Check(service.IsCurrentEmergencyRegistration(token), "The public validator accepts the token of the currently registered shortcut.");
    service.Configure(Profile(chordA) with { Enabled = false });
    Check(service.IsCurrentEmergencyRegistration(token), "A same-chord profile save preserves legitimate emergency confirmation.");
    service.Configure(Profile(chordB));
    Check(!service.IsCurrentEmergencyRegistration(token), "A delayed UI event cannot certify a different current emergency shortcut.");
}

using (var service = RunningService())
{
    var emergency = (EmergencyHotKey)Field(service, "_emergency")!;
    var oldGeneration = service.SafetyPauseGeneration;
    var native = NativeMethods.Registrations.Last();
    Call(emergency, "HandleHotKey", HotKey(native));
    Check(service.SafetyPauseGeneration == oldGeneration + 1 && !service.IsRunning,
        "Emergency pause advances safety generation synchronously, before asynchronous UI confirmation.");
    service.Configure(Profile(chordA));
    Check(service.SafetyPauseGeneration == oldGeneration + 1,
        "Normal same-chord configuration cannot erase or manufacture a keyboard safety generation.");
    Check(!service.Resume(oldGeneration), "A delayed enable continuation cannot resume across an emergency pause.");
    var trialProfile = Profile(chordA) with
    {
        Enabled = false,
        Rules = [new()
        {
            Id = "copy", Intent = "Copy", Source = new(KeyModifiers.Command, KeyCode.C),
            Target = new(KeyModifiers.Control, KeyCode.C), Scope = new() { ProcessName = "keybridge.exe" }
        }]
    };
    Check(!service.BeginLocalTrial(trialProfile, oldGeneration), "A delayed local trial cannot restart across an emergency pause.");
    Check(!service.BeginRuleTrial(trialProfile, "copy", oldGeneration), "A delayed application trial cannot restart across an emergency pause.");
    Check(service.Resume(service.SafetyPauseGeneration), "A new deliberate resume uses the current safety generation.");
    var currentGeneration = service.SafetyPauseGeneration;
    OnKeyboard(service, () => Call(service, "PauseFromHook", "Paused for an engine safety failure."));
    Check(service.SafetyPauseGeneration == currentGeneration + 1 && !service.Resume(currentGeneration),
        "Engine safety pauses synchronously invalidate pending activation requests too.");
}

using (var service = RunningService())
{
    service.Pause();
    OnKeyboard(service, () => { });
    var emergency = (EmergencyHotKey)Field(service, "_emergency")!;
    var native = NativeMethods.Registrations.Last();
    var generation = service.SafetyPauseGeneration;
    NativeMethods.BeforeInstallHook = () => Call(emergency, "HandleHotKey", HotKey(native));
    try
    {
        Check(!service.Resume(generation) && !service.IsRunning,
            "An emergency arriving during hook installation cannot be overwritten by final engine activation.");
        OnKeyboard(service, () => Check((nint)Field(service, "_hook")! == 0,
            "Canceled activation removes its newly installed managed hook."));
    }
    finally { NativeMethods.BeforeInstallHook = null; }
}

using (var service = RunningService())
{
    service.Pause();
    OnKeyboard(service, () => { });
    var generation = service.SafetyPauseGeneration;
    NativeMethods.BeforeInstallHook = service.Dispose;
    try
    {
        Check(!service.Resume(generation) && !service.IsRunning && !service.IsLocalTrial,
            "Disposal during hook installation cannot be overwritten by final activation.");
        Check((nint)Field(service, "_hook")! == 0, "Canceled disposed activation removes its newly installed managed hook.");
    }
    finally { NativeMethods.BeforeInstallHook = null; }
}

Console.WriteLine($"Passed {passed} managed keyboard recovery and emergency registration checks. No native hooks or input APIs were called.");
