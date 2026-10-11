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
    private readonly ConcurrentQueue<(KeyChord Chord, TaskCompletionSource<bool> Completed)> _pending = new();
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private uint _threadId;
    private volatile bool _available;
    private int _lastNativeError;
    public bool Available => _available;
    public int LastNativeError => Volatile.Read(ref _lastNativeError);
    internal event Action? Triggered;
    internal event Action? Changed;
    internal EmergencyHotKey()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "KeyBridge recovery" };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(2));
        Configure(new(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F12));
    }
    internal void Configure(KeyChord chord)
    {
        _available = false;
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending.Enqueue((chord, completed));
        NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WmApp, 0, 0);
        // Registration is independent of the hook, and the UI must not enable mapping
        // against stale availability from a previously registered emergency shortcut.
        completed.Task.Wait(TimeSpan.FromSeconds(1));
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
                    while (_pending.TryDequeue(out var request))
                    {
                        NativeMethods.UnregisterHotKey(0, NativeMethods.EmergencyId);
                        _available = NativeMethods.RegisterHotKey(0, NativeMethods.EmergencyId,
                            ToNativeModifiers(request.Chord.Modifiers) | 0x4000, (uint)request.Chord.Key);
                        Volatile.Write(ref _lastNativeError, _available ? 0 : Marshal.GetLastWin32Error());
                        request.Completed.TrySetResult(_available);
                        Changed?.Invoke();
                    }
                }
                else if (message.Id == NativeMethods.WmHotKey && message.WParam == NativeMethods.EmergencyId)
                    Triggered?.Invoke();
            }
        }
        finally { NativeMethods.UnregisterHotKey(0, NativeMethods.EmergencyId); _available = false; }
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
        NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WmQuit, 0, 0);
        if (Thread.CurrentThread != _thread) _thread.Join(TimeSpan.FromSeconds(1));
        _ready.Dispose();
    }
}
