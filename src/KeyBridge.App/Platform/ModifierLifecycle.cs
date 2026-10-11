using System;
using System.Collections.Generic;
using System.Linq;
using KeyBridge.Core;

namespace KeyBridge.App.Platform;

/// <summary>Transient physical/native/synthetic state. Never stores input text or event history.</summary>
public sealed class ModifierLifecycle
{
    private readonly bool[] _physical = new bool[256], _native = new bool[256], _owned = new bool[256], _suspended = new bool[256];
    public KeyModifiers PhysicalModifiers => KeyChord.ModifierKeyCodes.Aggregate(KeyModifiers.None,
        (value, key) => value | (_physical[(int)key] ? KeyChord.ModifierFor(key) : KeyModifiers.None));
    public bool IsPhysicalDown(KeyCode key) => _physical[(int)key];
    public bool IsNativeDown(KeyCode key) => _native[(int)key];
    public bool HasOwnedKeys => _owned.Any(value => value);
    public void Seed(KeyCode key, bool down) => _physical[(int)key] = _native[(int)key] = down;
    public void ObservePhysical(KeyCode key, bool down, bool forwarded)
    {
        _physical[(int)key] = down;
        if (forwarded) { _native[(int)key] = down; _suspended[(int)key] = false; }
    }
    public void ObserveExternalNative(KeyCode key, bool down)
    { _native[(int)key] = down; _owned[(int)key] = false; }

    /// <summary>
    /// Adds Ctrl before suspending a native Alt/Win, preserves borrowed physical keys, and
    /// restores native modifiers before releasing owned Ctrl. Supported first-release targets
    /// are Ctrl letter chords only; optional navigation and switch actions remain gated.
    /// </summary>
    public IReadOnlyList<SyntheticKeyEvent>? Plan(KeyChord target)
    {
        if (!ShortcutSupport.IsSupportedTarget(target) || _native[(int)target.Key]) return null;
        var desired = ResolveModifiers(target.Modifiers);
        if (!desired.Any(key => key is KeyCode.LeftControl or KeyCode.RightControl)) return null;
        var added = desired.Where(key => !_native[(int)key]).ToArray();
        var suspended = KeyChord.ModifierKeyCodes.Where(key => _native[(int)key] && !desired.Contains(key)).ToArray();
        var events = new List<SyntheticKeyEvent>(22);
        // Ctrl goes first, before changing an already-forwarded Alt or Win key.
        foreach (var key in added.OrderBy(key => key is KeyCode.LeftControl or KeyCode.RightControl ? 0 : 1))
            events.Add(new(key, false));
        foreach (var key in suspended) events.Add(new(key, true));
        events.Add(new(target.Key, false));
        events.Add(new(target.Key, true));
        foreach (var key in suspended.Reverse()) events.Add(new(key, false));
        foreach (var key in added.Reverse()) events.Add(new(key, true));
        return events;
    }

    public void AcceptInserted(IEnumerable<SyntheticKeyEvent> events)
    {
        foreach (var item in events)
        {
            var key = (int)item.Key;
            _native[key] = !item.IsUp;
            _suspended[key] = item.IsUp && _physical[key] && KeyChord.ModifierFor(item.Key) != KeyModifiers.None;
            // A restored physical key is borrowed, never app-owned.
            _owned[key] = !item.IsUp && (KeyChord.ModifierFor(item.Key) == KeyModifiers.None || !_physical[key]);
        }
    }

    /// <summary>Release only owned keys, and restore physical native modifiers suspended by a partial batch.</summary>
    public IReadOnlyList<SyntheticKeyEvent> PlanRecovery()
    {
        var events = new List<SyntheticKeyEvent>();
        for (var key = 0; key < 256; key++)
            if (_owned[key] && (KeyChord.ModifierFor((KeyCode)key) == KeyModifiers.None || !_physical[key]))
                events.Add(new((KeyCode)key, true));
        foreach (var key in KeyChord.ModifierKeyCodes)
            if (_suspended[(int)key] && _physical[(int)key] && !_native[(int)key]) events.Add(new(key, false));
        return events;
    }

    public void Reset()
    {
        Array.Clear(_physical); Array.Clear(_native); Array.Clear(_owned); Array.Clear(_suspended);
    }

    private HashSet<KeyCode> ResolveModifiers(KeyModifiers modifiers)
    {
        var desired = new HashSet<KeyCode>();
        foreach (var pair in new[]
        {
            (KeyModifiers.Control, KeyCode.LeftControl, KeyCode.RightControl),
            (KeyModifiers.Shift, KeyCode.LeftShift, KeyCode.RightShift),
            (KeyModifiers.Alt, KeyCode.LeftAlt, KeyCode.RightAlt),
            (KeyModifiers.Windows, KeyCode.LeftWindows, KeyCode.RightWindows)
        })
        {
            if ((modifiers & pair.Item1) != 0)
            {
                if (_native[(int)pair.Item2]) desired.Add(pair.Item2);
                if (_native[(int)pair.Item3]) desired.Add(pair.Item3);
                if (!desired.Contains(pair.Item2) && !desired.Contains(pair.Item3)) desired.Add(pair.Item2);
            }
            if ((modifiers & KeyChord.ModifierFor(pair.Item2)) != 0) desired.Add(pair.Item2);
            if ((modifiers & KeyChord.ModifierFor(pair.Item3)) != 0) desired.Add(pair.Item3);
        }
        return desired;
    }
}

public readonly record struct SyntheticKeyEvent(KeyCode Key, bool IsUp);
