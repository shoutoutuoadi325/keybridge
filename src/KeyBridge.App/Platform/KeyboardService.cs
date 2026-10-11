using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using KeyBridge.Core;

namespace KeyBridge.App.Platform;

/// <summary>
/// User-level public Win32 hooks and SendInput; no elevation, injection into processes,
/// clipboard access, text collection, or permanent system remapping.
/// </summary>
public sealed class KeyboardService : IDisposable
{
    private readonly EmergencyHotKey _emergency = new();
    private readonly ForegroundContextProvider _contextProvider = new();
    private readonly ModifierLifecycle _keys = new();
    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private readonly System.Threading.Timer _contextTimer;
    private readonly NativeMethods.HookProc _hookProc;
    private readonly bool[] _swallowed = new bool[256];
    private CompiledProfile? _compiled;
    private AppProfile? _profile;
    private LocalTrialRules? _trialRules;
    private ApplicationRuleTrial? _applicationTrial;
    private ContextSnapshot _context = ContextSnapshot.Unknown;
    private nint _hook;
    private uint _threadId;
    private int _polling, _disposed, _operation, _lastNativeError;
    private volatile bool _running, _draining, _localTrial;
    private string _status = "Paused. Complete setup and explicitly enable mapping.";
    public bool IsRunning => _running;
    public bool IsLocalTrial => _localTrial && _running;
    public bool IsTrial => _running && (_localTrial || Volatile.Read(ref _applicationTrial) is not null);
    public string Status => Volatile.Read(ref _status);
    public ForegroundContext CurrentContext => Volatile.Read(ref _context).Context;
    public bool EmergencyAvailable => _emergency.Available;
    public int LastNativeError => Volatile.Read(ref _lastNativeError);
    public int EmergencyNativeError => _emergency.LastNativeError;
    public event Action? Changed;
    public event Action? EmergencyPauseTriggered;

    public KeyboardService()
    {
        _hookProc = Hook;
        _emergency.Changed += Publish;
        _emergency.Triggered += () =>
        {
            Pause("Emergency pause. Resume must be chosen explicitly.");
            ThreadPool.QueueUserWorkItem(_ => EmergencyPauseTriggered?.Invoke());
        };
        _thread = new Thread(Run) { IsBackground = true, Name = "KeyBridge keyboard" };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(2));
        _contextTimer = new System.Threading.Timer(PollContext, null, 0, 250);
    }

    public void Configure(AppProfile profile)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        var compilation = RuleCompiler.Compile(profile);
        if (!compilation.Success) throw new ArgumentException(string.Join(" ", compilation.Issues.Select(issue => issue.Message)), nameof(profile));
        // Changing a profile always pauses. Compilation and disk work are outside the hook.
        Pause("Profile updated. Resume explicitly after reviewing the rules.");
        _profile = profile;
        Volatile.Write(ref _compiled, compilation.Snapshot);
        _emergency.Configure(profile.EmergencyPause);
    }

    public bool Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        _localTrial = false;
        Volatile.Write(ref _trialRules, null);
        Volatile.Write(ref _applicationTrial, null);
        var profile = _profile;
        if (profile is null || !profile.Enabled) { SetStatus("Paused. Enable the profile explicitly before resuming."); return false; }
        return StartEngine(profile, temporaryLocalTrial: false);
    }

    public bool BeginLocalTrial(AppProfile profile)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        Pause("Preparing a temporary local trial.");
        var trial = LocalTrialRules.Create(profile);
        if (trial is null || trial.Count == 0)
        { SetStatus("Paused. Add unambiguous basic editing rules scoped to keybridge.exe before trying them."); return false; }
        _emergency.Configure(profile.EmergencyPause);
        Volatile.Write(ref _trialRules, trial);
        return StartEngine(profile, temporaryLocalTrial: true);
    }

    public bool BeginRuleTrial(AppProfile profile, string ruleId)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        Pause("Preparing a temporary application trial.");
        var trial = ApplicationRuleTrial.Create(profile, ruleId);
        if (trial is null)
        { SetStatus("Paused. Select one valid, non-excluded application rule with a basic Ctrl letter target."); return false; }
        _emergency.Configure(profile.EmergencyPause);
        Volatile.Write(ref _applicationTrial, trial);
        return StartEngine(profile, temporaryLocalTrial: false);
    }

    private bool StartEngine(AppProfile profile, bool temporaryLocalTrial)
    {
        if (!profile.ConsentGranted || !profile.RoleKeyConfirmed || !profile.RecoveryTested)
        { SetStatus("Paused. Consent, role-key confirmation, and emergency recovery testing are required."); return false; }
        if (!EmergencyAvailable)
        { SetStatus("Paused. Emergency pause could not be registered. Select another recovery shortcut."); return false; }
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = Interlocked.Increment(ref _operation);
        Post(() =>
        {
            try
            {
                if (operation != Volatile.Read(ref _operation)) return;
                StopHook();
                if (_draining)
                {
                    SetStatus("Paused. Release the mapped shortcut keys before resuming.");
                    return;
                }
                _keys.Reset(); Array.Clear(_swallowed);
                for (var code = 0; code < 256; code++)
                    _keys.Seed((KeyCode)code, (NativeMethods.GetAsyncKeyState(code) & 0x8000) != 0);
                _hook = NativeMethods.SetWindowsHookExW(13, _hookProc, NativeMethods.GetModuleHandleW(null), 0);
                Volatile.Write(ref _lastNativeError, _hook != 0 ? 0 : Marshal.GetLastWin32Error());
                if (operation != Volatile.Read(ref _operation) || _disposed != 0)
                { StopHook(force: true); return; }
                _running = _hook != 0;
                _localTrial = _running && temporaryLocalTrial;
                var applicationTrial = Volatile.Read(ref _applicationTrial);
                SetStatus(_running ? temporaryLocalTrial ? "Shortcut preview is active in the KeyBridge editor."
                    : applicationTrial is not null ? $"Shortcut preview is active in {applicationTrial.ProcessName}."
                    : "Mapping enabled for your confirmed ENG US shortcuts."
                    : "Paused. Windows could not install the keyboard hook.");
            }
            finally { completed.TrySetResult(_running); }
        });
        if (!completed.Task.Wait(TimeSpan.FromSeconds(1)))
        { Pause("Paused. The keyboard thread did not respond in time."); return false; }
        return _running;
    }

    public void Pause(string reason = "Paused by user.")
    {
        Interlocked.Increment(ref _operation);
        _running = false; _localTrial = false;
        Volatile.Write(ref _trialRules, null);
        Volatile.Write(ref _applicationTrial, null);
        SetStatus(reason);
        if (_disposed == 0) Post(() => StopHook());
    }

    private void PollContext(object? state)
    {
        if (Interlocked.Exchange(ref _polling, 1) != 0) return;
        try
        {
            var previous = Volatile.Read(ref _context);
            var current = _contextProvider.Capture();
            Volatile.Write(ref _context, current);
            if (previous.Context != current.Context || previous.Window != current.Window) Publish();
            if (_draining) Post(ReconcilePausedKeys);
        }
        finally { Volatile.Write(ref _polling, 0); }
    }

    private nint Hook(int code, nuint message, nint dataPointer)
    {
        if (code < 0) return NativeMethods.CallNextHookEx(_hook, code, message, dataPointer);
        var handlingKey = -1;
        var handlingUp = false;
        try
        {
            var data = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(dataPointer);
            if ((data.Flags & (NativeMethods.Injected | NativeMethods.LowerInjected)) != 0 || data.ExtraInfo == NativeMethods.InputTag)
            {
                // External synthetic input passes through without remapping. An external
                // modifier invalidates ownership assumptions until the user resumes.
                if (data.ExtraInfo != NativeMethods.InputTag && IsModifier((int)data.VirtualKey) && _running)
                    PauseFromHook("Paused because another tool injected a modifier. Review concurrent keyboard tools.");
                if (data.ExtraInfo != NativeMethods.InputTag)
                {
                    var externalKey = NormalizeModifier(data);
                    if (externalKey is >= 0 and < 256)
                        _keys.ObserveExternalNative((KeyCode)externalKey, (data.Flags & 0x80) == 0);
                }
                return NativeMethods.CallNextHookEx(_hook, code, message, dataPointer);
            }
            var keyNumber = NormalizeModifier(data);
            if (keyNumber is < 0 or >= 256) return NativeMethods.CallNextHookEx(_hook, code, message, dataPointer);
            var key = (KeyCode)keyNumber;
            var isUp = (data.Flags & 0x80) != 0;
            handlingKey = keyNumber; handlingUp = isUp;
            if (IsModifier(keyNumber))
            {
                _keys.ObservePhysical(key, !isUp, true);
                return NativeMethods.CallNextHookEx(_hook, code, message, dataPointer);
            }
            var alreadySwallowed = _swallowed[keyNumber];
            var alreadyNative = _keys.IsNativeDown(key);
            _keys.ObservePhysical(key, !isUp, false);
            nint Pass()
            {
                _keys.ObservePhysical(key, !isUp, true);
                return NativeMethods.CallNextHookEx(_hook, code, message, dataPointer);
            }
            if (isUp)
            {
                _swallowed[keyNumber] = false;
                if (alreadySwallowed && !_running) Post(() => StopHook());
                return alreadySwallowed ? 1 : Pass();
            }
            // A key already delivered natively must remain native for its entire press.
            if (alreadyNative && !alreadySwallowed) return Pass();
            if (!_running) return alreadySwallowed ? 1 : Pass();
            var operation = Volatile.Read(ref _operation);
            var localTrial = _localTrial;
            var trialRules = Volatile.Read(ref _trialRules);
            var applicationTrial = Volatile.Read(ref _applicationTrial);
            var context = Volatile.Read(ref _context);
            var compiled = Volatile.Read(ref _compiled);
            if ((!localTrial && applicationTrial is null && compiled is null) || context.Context.Permission != PermissionState.Allowed
                || context.Context.Ime.Source != InputSource.EnglishKeyboard || !context.IsCurrent())
                return alreadySwallowed ? 1 : Pass();
            var physicalChord = new KeyChord(_keys.PhysicalModifiers, key);
            var target = localTrial
                ? trialRules?.Match(physicalChord, context.ProcessId == (uint)Environment.ProcessId, context.Context.Ime.Source)
                : applicationTrial is not null ? applicationTrial.Match(physicalChord, context.Context)
                    : compiled!.Match(physicalChord, context.Context).Target;
            if (target is null)
                return alreadySwallowed ? 1 : Pass();
            // Before deciding to swallow, check the target against actual native state.
            var targetPlan = _keys.Plan(target);
            if (targetPlan is null) return alreadySwallowed ? 1 : Pass();
            if (!_running || operation != Volatile.Read(ref _operation) || !context.IsCurrent())
                return alreadySwallowed ? 1 : Pass();
            var inserted = Emit(targetPlan);
            if (inserted > 0) _swallowed[keyNumber] = true;
            if (inserted != targetPlan.Count)
            {
                PauseFromHook("Paused. Windows did not insert the complete shortcut. No shortcut replay was attempted.");
                return inserted > 0 || alreadySwallowed ? 1 : Pass();
            }
            _swallowed[keyNumber] = true;
            if (!context.IsCurrent()) PauseFromHook("Paused. The foreground window changed during a shortcut. Review the target application.");
            return 1;
        }
        catch
        {
            // No event data is logged. An unexpected callback failure disables further mappings.
            var consumed = handlingKey is >= 0 and < 256 && !IsModifier(handlingKey)
                && (_swallowed[handlingKey] || _keys.HasOwnedKeys);
            if (consumed && !handlingUp) _swallowed[handlingKey] = true;
            PauseFromHook("Paused. The keyboard engine encountered an unexpected error.");
            return consumed ? 1 : NativeMethods.CallNextHookEx(_hook, code, message, dataPointer);
        }
    }

    private int Emit(IReadOnlyList<SyntheticKeyEvent> plan)
    {
        if (plan.Count == 0) return 0;
        var inputs = plan.Select(item => NativeMethods.Key((int)item.Key, item.IsUp)).ToArray();
        var inserted = (int)NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.Input>());
        _keys.AcceptInserted(plan.Take(Math.Min(inserted, plan.Count)));
        return inserted;
    }
    private void PauseFromHook(string reason)
    {
        Interlocked.Increment(ref _operation);
        _running = false; _localTrial = false;
        Volatile.Write(ref _trialRules, null);
        Volatile.Write(ref _applicationTrial, null);
        SetStatus(reason);
        // Attempt only owned-key releases/native restoration; never repeat the operation.
        var recovery = _keys.PlanRecovery();
        if (recovery.Count > 0) Emit(recovery);
        Post(() => StopHook());
    }
    private void StopHook(bool force = false)
    {
        _running = false;
        var recovery = _keys.PlanRecovery();
        if (recovery.Count > 0) Emit(recovery);
        // A converted source down was never delivered to the target. During a pause,
        // briefly drain only its repeats/up so a held source cannot become a native
        // action mid-press. The hook is removed as soon as those keys are released.
        if (!force && Enumerable.Range(0, 256).Any(key => _swallowed[key] && _keys.IsPhysicalDown((KeyCode)key)))
        { _draining = true; return; }
        if (_hook != 0) { NativeMethods.UnhookWindowsHookEx(_hook); _hook = 0; }
        _draining = false;
        _keys.Reset(); Array.Clear(_swallowed);
    }
    private void ReconcilePausedKeys()
    {
        if (!_draining || _running) return;
        // Off-callback state checks handle releases missed during sleep/lock. No probing
        // input is generated, and this does not claim to detect hook timeout removal.
        for (var key = 0; key < 256; key++)
            if (_swallowed[key] && (NativeMethods.GetAsyncKeyState(key) & 0x8000) == 0)
            {
                _swallowed[key] = false;
                _keys.ObservePhysical((KeyCode)key, false, false);
            }
        StopHook();
    }
    private void Post(Action command)
    {
        _commands.Enqueue(command);
        NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WmApp, 0, 0);
    }
    private void Run()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        NativeMethods.PeekMessageW(out _, 0, 0, 0, 0); _ready.Set();
        try
        {
            while (NativeMethods.GetMessageW(out var message, 0, 0, 0) > 0)
            {
                if (message.Id == NativeMethods.WmApp)
                { while (_commands.TryDequeue(out var command)) command(); }
                else { NativeMethods.TranslateMessage(in message); NativeMethods.DispatchMessageW(in message); }
            }
        }
        finally { StopHook(force: true); }
    }
    private void SetStatus(string value) { Volatile.Write(ref _status, value); Publish(); }
    private void Publish() => ThreadPool.QueueUserWorkItem(_ => { if (_disposed == 0) Changed?.Invoke(); });
    private static bool IsModifier(int key) => key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5;
    private static int NormalizeModifier(NativeMethods.KeyboardHookData data) => data.VirtualKey switch
    {
        0x10 => data.ScanCode == 0x36 ? 0xA1 : 0xA0,
        0x11 => (data.Flags & 1) != 0 ? 0xA3 : 0xA2,
        0x12 => (data.Flags & 1) != 0 ? 0xA5 : 0xA4,
        _ => (int)data.VirtualKey
    };
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _running = false;
        _contextTimer.Dispose();
        _emergency.Dispose();
        Post(() => StopHook(force: true));
        NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WmQuit, 0, 0);
        if (Thread.CurrentThread != _thread) _thread.Join(TimeSpan.FromSeconds(1));
        _ready.Dispose();
    }
}
