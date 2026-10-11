using System;
using System.Linq;
using KeyBridge.Core;

namespace KeyBridge.App.Platform;

/// <summary>
/// Keeps a converted physical source press suppressed through its repeats and release.
/// Native state and injected events cannot establish that the physical key was released.
/// </summary>
public sealed class SourcePressLifecycle
{
    private readonly bool[] _suppressed = new bool[256];

    public bool HasPendingRelease => _suppressed.Any(value => value);
    public bool IsSuppressed(KeyCode key) => _suppressed[(int)key];
    public void Suppress(KeyCode key) => _suppressed[(int)key] = true;

    /// <returns>Whether this event belongs to a suppressed physical press.</returns>
    public bool Observe(KeyCode key, bool isUp, bool isInjected)
    {
        if (isInjected) return false;
        var suppressed = _suppressed[(int)key];
        if (isUp) _suppressed[(int)key] = false;
        return suppressed;
    }

    public void Reset() => Array.Clear(_suppressed);
}
