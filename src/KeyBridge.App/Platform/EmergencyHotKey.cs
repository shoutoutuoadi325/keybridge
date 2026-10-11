using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using KeyBridge.Core;

namespace KeyBridge.App.Platform;

/// <summary>Recovery uses its own message thread, independent of the low-level hook thread.</summary>
internal sealed class EmergencyHotKey : IDisposable
{
    private sealed record RegistrationRequest(KeyChord Chord, long Generation, TaskCompletionSource<bool> Completed);
    private readonly ConcurrentQueue<RegistrationRequest> _pending = new();
    private readonly object _registrationGate = new();
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private uint _threadId;
    private volatile bool _available;
    private int _lastNativeError;
    private int _nativeId, _nextNativeId = 1, _disposed;
    private long _generation;
    private EmergencyPauseRegistration? _registration;
    public bool Available => _available;
    public int LastNativeError => Volatile.Read(ref _lastNativeError);
    internal event Action<EmergencyPauseRegistration>? Triggered;
    internal event Action? Changed;
    internal EmergencyHotKey()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "KeyBridge recovery" };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(2));
        // Register only after KeyboardService receives a validated profile, so startup
        // never temporarily takes a default chord protected by the user's settings.
    }
    internal void Configure(KeyChord chord)
    {
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_registrationGate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            // Saving other settings must not invalidate a test of the same live hotkey.
            if (_available && _registration?.Chord == chord) return;
            _available = false;
            _registration = null;
            _pending.Enqueue(new(chord, ++_generation, completed));
        }
        NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WmApp, 0, 0);
        // Registration is independent of the hook, and the UI must not enable mapping
        // against stale availability from a previously registered emergency shortcut.
        completed.Task.Wait(TimeSpan.FromSeconds(1));
    }

    internal bool IsCurrent(EmergencyPauseRegistration registration)
    {
        lock (_registrationGate)
            return _disposed == 0 && _available && _registration == registration;
    }

    private void Register(RegistrationRequest request)
    {
        lock (_registrationGate)
        {
            if (_disposed != 0 || request.Generation != _generation)
            { request.Completed.TrySetResult(false); return; }
            if (_nativeId != 0) NativeMethods.UnregisterHotKey(0, _nativeId);
            _nativeId = 0;
            // Never reuse an ID: an already queued WM_HOTKEY may refer to an old chord,
            // including a chord selected again later. Fail closed if IDs are exhausted.
            if (_nextNativeId <= 0xBFFF)
            {
                var id = _nextNativeId++;
                _available = NativeMethods.RegisterHotKey(0, id,
                    ToNativeModifiers(request.Chord.Modifiers) | 0x4000, (uint)request.Chord.Key);
                Volatile.Write(ref _lastNativeError, _available ? 0 : Marshal.GetLastWin32Error());
                if (_available)
                {
                    _nativeId = id;
                    _registration = new(Guid.NewGuid(), request.Chord);
                }
            }
            else { _available = false; Volatile.Write(ref _lastNativeError, 1450); }
            request.Completed.TrySetResult(_available);
        }
        Changed?.Invoke();
    }

    private void HandleHotKey(NativeMethods.Message message)
    {
        EmergencyPauseRegistration? registration;
        lock (_registrationGate)
        {
            registration = _registration;
            var parameter = unchecked((ulong)message.LParam.ToInt64());
            if (_disposed != 0 || !_available || registration is null || message.WParam != (nuint)_nativeId
                || (parameter & 0xFFFF) != ToNativeModifiers(registration.Chord.Modifiers)
                || ((parameter >> 16) & 0xFFFF) != (uint)registration.Chord.Key) return;
        }
        // The token is checked again by KeyboardService and the UI after each queue hop.
        Triggered?.Invoke(registration);
    }
    private void Run()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        NativeMethods.PeekMessageW(out _, 0, 0, 0, 0);
        _ready.Set();
        try
        {
            while (NativeMethods.GetMessageW(out var message, 0, 0, 0) > 0)
            {
                if (message.Id == NativeMethods.WmApp)
                {
                    while (_pending.TryDequeue(out var request)) Register(request);
                }
                else if (message.Id == NativeMethods.WmHotKey) HandleHotKey(message);
            }
        }
        finally
        {
            lock (_registrationGate)
            {
                if (_nativeId != 0) NativeMethods.UnregisterHotKey(0, _nativeId);
                _nativeId = 0; _available = false; _registration = null;
                while (_pending.TryDequeue(out var request)) request.Completed.TrySetResult(false);
            }
        }
    }
    private static uint ToNativeModifiers(KeyModifiers value)
    {
        uint flags = 0;
        if ((value & (KeyModifiers.Alt | KeyModifiers.LeftAlt | KeyModifiers.RightAlt)) != 0) flags |= 1;
        if ((value & (KeyModifiers.Control | KeyModifiers.LeftControl | KeyModifiers.RightControl)) != 0) flags |= 2;
        if ((value & (KeyModifiers.Shift | KeyModifiers.LeftShift | KeyModifiers.RightShift)) != 0) flags |= 4;
        if ((value & (KeyModifiers.Windows | KeyModifiers.LeftWindows | KeyModifiers.RightWindows)) != 0) flags |= 8;
        return flags;
    }
    public void Dispose()
    {
        lock (_registrationGate)
        {
            if (_disposed != 0) return;
            _disposed = 1; _available = false; _registration = null;
        }
        NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WmQuit, 0, 0);
        if (Thread.CurrentThread != _thread) _thread.Join(TimeSpan.FromSeconds(1));
        _ready.Dispose();
    }
}
