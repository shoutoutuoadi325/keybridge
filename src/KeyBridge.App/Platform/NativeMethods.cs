using System.Runtime.InteropServices;

namespace KeyBridge.App.Platform;

internal static class NativeMethods
{
    internal const uint WmQuit = 0x12, WmHotKey = 0x312, WmApp = 0x8000;
    internal const uint Injected = 0x10, LowerInjected = 0x2, KeyUp = 0x2, Extended = 0x1;
    internal const int EmergencyId = 0x4B42;
    internal static readonly nuint InputTag = 0x4B425247;
    internal delegate nint HookProc(int code, nuint message, nint data);
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardHookData
    { public uint VirtualKey, ScanCode, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct Message
    { public nint Window; public uint Id; public nuint WParam; public nint LParam; public uint Time; public int X, Y; public uint Private; }
    [StructLayout(LayoutKind.Sequential)] internal struct Input
    { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput
    { public ushort VirtualKey, Scan; public uint Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct MouseInput
    { public int X, Y; public uint Data, Flags, Time; public nuint ExtraInfo; }
    internal static Input Key(int key, bool up) => new()
    {
        Type = 1,
        Data = new InputUnion { Keyboard = new KeyboardInput
        {
            VirtualKey = (ushort)key,
            Flags = (up ? KeyUp : 0) | (key is 0xA3 or 0xA5 or 0x5B or 0x5C or 0x25 or 0x26 or 0x27 or 0x28 or 0x24 or 0x23 or 0x2D or 0x2E ? Extended : 0),
            ExtraInfo = InputTag
        } }
    };
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetWindowsHookExW(int type, HookProc proc, nint module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nuint message, nint data);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, [In] Input[] inputs, int size);
    [DllImport("user32.dll")] internal static extern int GetMessageW(out Message message, nint window, uint min, uint max);
    [DllImport("user32.dll")] internal static extern bool PeekMessageW(out Message message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] internal static extern bool TranslateMessage(in Message message);
    [DllImport("user32.dll")] internal static extern nint DispatchMessageW(in Message message);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool PostThreadMessageW(uint threadId, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] internal static extern nint GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandleW(string? name);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern nint OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(nint handle);
    [DllImport("advapi32.dll", SetLastError = true)] internal static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32.dll", SetLastError = true)] internal static extern bool GetTokenInformation(nint token, int informationClass, nint information, uint length, out uint returnedLength);
    [DllImport("advapi32.dll")] internal static extern nint GetSidSubAuthorityCount(nint sid);
    [DllImport("advapi32.dll")] internal static extern nint GetSidSubAuthority(nint sid, uint index);
}
