using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using KeyBridge.Core;

namespace KeyBridge.App.Platform;

internal sealed record ContextSnapshot(nint Window, uint ThreadId, nint Layout, long CapturedAt,
    ForegroundContext Context, uint ProcessId = 0)
{
    internal static ContextSnapshot Unknown { get; } = new(0, 0, 0, 0,
        new("", PermissionState.Unknown, new ImeState(), IsKnown: false));
    internal bool IsCurrent()
    {
        if (!Context.IsKnown || Stopwatch.GetElapsedTime(CapturedAt).TotalMilliseconds > 600) return false;
        // Fast identity/layout guards only. PID/name/version/token lookup never runs in a hook.
        var window = NativeMethods.GetForegroundWindow();
        return window != 0 && window == Window
            && NativeMethods.GetWindowThreadProcessId(window, out _) == ThreadId
            && NativeMethods.GetKeyboardLayout(ThreadId) == Layout;
    }
}

/// <summary>Reads public window/layout/token data only, without window titles or input content.</summary>
internal sealed class ForegroundContextProvider
{
    private readonly int? _ownIntegrity = ReadIntegrity((uint)Environment.ProcessId);
    private uint _lastPid;
    private DateTime _lastStart;
    private string _processName = "", _version = "";

    internal ContextSnapshot Capture()
    {
        try
        {
            var window = NativeMethods.GetForegroundWindow();
            var thread = NativeMethods.GetWindowThreadProcessId(window, out var pid);
            if (window == 0 || thread == 0 || pid == 0) return ContextSnapshot.Unknown;
            var layout = NativeMethods.GetKeyboardLayout(thread);
            using var process = Process.GetProcessById((int)pid);
            var start = process.StartTime;
            if (pid != _lastPid || start != _lastStart)
            {
                _lastPid = 0;
                _processName = process.ProcessName + ".exe";
                // File version, when accessible, is used to expire recorded application evidence.
                try { _version = process.MainModule?.FileVersionInfo.FileVersion ?? ""; }
                catch (System.ComponentModel.Win32Exception) { _version = ""; }
                _lastPid = pid;
                _lastStart = start;
            }
            var integrity = ReadIntegrity(pid);
            var permission = integrity is null || _ownIntegrity is null ? PermissionState.Unknown
                : integrity >= 0x3000 || integrity > _ownIntegrity ? PermissionState.Denied : PermissionState.Allowed;
            var raw = unchecked((uint)layout.ToInt64());
            // Only the standard US keyboard has a first-release adapter. Language ID alone
            // cannot prove a layout is plain ENG, or prove any Pinyin internal state.
            var ime = raw == 0x04090409
                ? new ImeState(InputSource.EnglishKeyboard, ImeMode.NotApplicable,
                    CompositionState.NotComposing, InputWidth.HalfWidth, PunctuationMode.English)
                : new ImeState(InputSource.Unknown);
            if (window != NativeMethods.GetForegroundWindow() || layout != NativeMethods.GetKeyboardLayout(thread))
                return ContextSnapshot.Unknown;
            return new(window, thread, layout, Stopwatch.GetTimestamp(),
                new(_processName, permission, ime, _version, WindowsBuild: Environment.OSVersion.Version.ToString()), pid);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException
            or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            _lastPid = 0;
            return ContextSnapshot.Unknown;
        }
    }

    private static int? ReadIntegrity(uint processId)
    {
        var process = NativeMethods.OpenProcess(0x1000, false, processId);
        if (process == 0) return null;
        nint token = 0, buffer = 0;
        try
        {
            if (!NativeMethods.OpenProcessToken(process, 0x8, out token)) return null;
            NativeMethods.GetTokenInformation(token, 25, 0, 0, out var length);
            if (length == 0 || length > 65536) return null;
            buffer = Marshal.AllocHGlobal((int)length);
            if (!NativeMethods.GetTokenInformation(token, 25, buffer, length, out _)) return null;
            var sid = Marshal.ReadIntPtr(buffer);
            if (sid == 0) return null;
            var count = Marshal.ReadByte(NativeMethods.GetSidSubAuthorityCount(sid));
            return count == 0 ? null : Marshal.ReadInt32(NativeMethods.GetSidSubAuthority(sid, (uint)count - 1));
        }
        finally
        {
            if (buffer != 0) Marshal.FreeHGlobal(buffer);
            if (token != 0) NativeMethods.CloseHandle(token);
            NativeMethods.CloseHandle(process);
        }
    }
}
