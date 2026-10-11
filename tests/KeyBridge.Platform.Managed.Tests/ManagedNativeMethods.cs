using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using KeyBridge.Core;

namespace KeyBridge.App.Platform;

// This test assembly deliberately excludes production NativeMethods and the real
// foreground provider. All service threads run only these managed replacements.
// There is no DllImport, hook installation, keyboard injection, or desktop query.
internal static class NativeMethods
{
    internal const uint WmQuit = 0x12, WmHotKey = 0x312, WmApp = 0x8000, Injected = 0x10, LowerInjected = 2;
    internal static readonly nuint InputTag = 0x4B425247;
    internal delegate nint HookProc(int code, nuint message, nint data);
    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardHookData { public uint VirtualKey, ScanCode, Flags; public nuint ExtraInfo; }
    internal struct Message { public uint Id; public nuint WParam; public nint LParam; }
    internal struct Input { public int Key; public bool Up; }
    internal sealed record Registration(uint ThreadId, int Id, uint Modifiers, uint Key);
    private static readonly ConcurrentDictionary<uint, BlockingCollection<Message>> Queues = new();
    private static readonly ConcurrentDictionary<int, bool> NativeDown = new();
    internal static readonly ConcurrentQueue<int> InsertCounts = new();
    internal static readonly ConcurrentQueue<Registration> Registrations = new();
    internal static int SendCalls, HookInstalls, Unhooks;
    internal static volatile bool RegistrationSucceeds = true;
    internal static Action? BeforeInstallHook;
    internal static Input Key(int key, bool up) => new() { Key = key, Up = up };
    internal static void SeedNative(KeyCode key, bool down) => NativeDown[(int)key] = down;
    internal static bool IsNativeDown(KeyCode key) => NativeDown.GetValueOrDefault((int)key);
    internal static uint SendInput(uint count, Input[] inputs, int size)
    {
        Interlocked.Increment(ref SendCalls);
        var inserted = InsertCounts.TryDequeue(out var requested) ? Math.Min(requested, (int)count) : (int)count;
        foreach (var input in inputs.Take(inserted)) NativeDown[input.Key] = !input.Up;
        Marshal.SetLastPInvokeError(inserted == count ? 0 : 5);
        return (uint)inserted;
    }
    internal static bool UnhookWindowsHookEx(nint hook) { Interlocked.Increment(ref Unhooks); return true; }
    internal static short GetAsyncKeyState(int key) => NativeDown.GetValueOrDefault(key) ? unchecked((short)0x8000) : (short)0;
    internal static nint SetWindowsHookExW(int type, HookProc proc, nint module, uint threadId)
    { BeforeInstallHook?.Invoke(); Interlocked.Increment(ref HookInstalls); return 42; }
    internal static nint GetModuleHandleW(string? name) => 0;
    internal static nint CallNextHookEx(nint hook, int code, nuint message, nint data)
    {
        var item = Marshal.PtrToStructure<KeyboardHookData>(data);
        NativeDown[(int)item.VirtualKey] = (item.Flags & 0x80) == 0;
        return 0;
    }
    internal static bool RegisterHotKey(nint window, int id, uint modifiers, uint key)
    {
        Registrations.Enqueue(new(GetCurrentThreadId(), id, modifiers, key));
        Marshal.SetLastPInvokeError(RegistrationSucceeds ? 0 : 1409);
        return RegistrationSucceeds;
    }
    internal static bool UnregisterHotKey(nint window, int id) => true;
    internal static bool PostThreadMessageW(uint threadId, uint message, nuint wParam, nint lParam)
    {
        if (!Queues.TryGetValue(threadId, out var queue)) return false;
        queue.Add(new() { Id = message, WParam = wParam, LParam = lParam });
        return true;
    }
    internal static uint GetCurrentThreadId() => (uint)Environment.CurrentManagedThreadId;
    internal static bool PeekMessageW(out Message message, nint window, uint min, uint max, uint remove)
    {
        Queues.GetOrAdd(GetCurrentThreadId(), _ => new());
        message = default; return false;
    }
    internal static int GetMessageW(out Message message, nint window, uint min, uint max)
    {
        message = Queues.GetOrAdd(GetCurrentThreadId(), _ => new()).Take();
        return message.Id == WmQuit ? 0 : 1;
    }
    internal static bool TranslateMessage(in Message message) => true;
    internal static nint DispatchMessageW(in Message message) => 0;
    internal static void ResetInput()
    {
        NativeDown.Clear();
        while (InsertCounts.TryDequeue(out _)) { }
    }
}

internal sealed record ContextSnapshot(nint Window, ForegroundContext Context, uint ProcessId = 0)
{
    internal static ContextSnapshot Unknown { get; } = new(0, new("", PermissionState.Unknown, new(), IsKnown: false));
    internal bool IsCurrent() => false;
}
internal sealed class ForegroundContextProvider { internal ContextSnapshot Capture() => ContextSnapshot.Unknown; }
