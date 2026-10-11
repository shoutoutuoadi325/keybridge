using System.Text.Json.Serialization;

namespace KeyBridge.Core;

public enum KeyCode
{
    None = 0, Backspace = 8, Tab = 9, Enter = 13, Escape = 27, Space = 32,
    PageUp = 33, PageDown = 34, End = 35, Home = 36, Left = 37, Up = 38, Right = 39, Down = 40,
    Insert = 45, Delete = 46,
    D0 = 48, D1 = 49, D2 = 50, D3 = 51, D4 = 52, D5 = 53, D6 = 54, D7 = 55, D8 = 56, D9 = 57,
    A = 65, B = 66, C = 67, D = 68, E = 69, F = 70, G = 71, H = 72, I = 73, J = 74,
    K = 75, L = 76, M = 77, N = 78, O = 79, P = 80, Q = 81, R = 82, S = 83, T = 84,
    U = 85, V = 86, W = 87, X = 88, Y = 89, Z = 90,
    LeftWindows = 91, RightWindows = 92,
    F1 = 112, F2 = 113, F3 = 114, F4 = 115, F5 = 116, F6 = 117, F7 = 118, F8 = 119,
    F9 = 120, F10 = 121, F11 = 122, F12 = 123,
    LeftShift = 160, RightShift = 161, LeftControl = 162, RightControl = 163,
    LeftAlt = 164, RightAlt = 165, OemPeriod = 190
}

[Flags]
public enum KeyModifiers
{
    None = 0, LeftShift = 1, RightShift = 2, LeftControl = 4, RightControl = 8,
    LeftAlt = 16, RightAlt = 32, LeftWindows = 64, RightWindows = 128,
    Command = 256, Shift = 512, Control = 1024, Alt = 2048, Windows = 4096
}

public sealed record KeyChord([property: JsonRequired] KeyModifiers Modifiers, [property: JsonRequired] KeyCode Key)
{
    public KeyChord ResolveCommand(KeyCode commandKey) => this with
    {
        Modifiers = (Modifiers & ~KeyModifiers.Command) |
            (Modifiers.HasFlag(KeyModifiers.Command) ? ModifierFor(commandKey) : KeyModifiers.None)
    };

    public bool Matches(KeyChord physical)
    {
        if (Key != physical.Key || Modifiers.HasFlag(KeyModifiers.Command) || (physical.Modifiers & ~(KeyModifiers)255) != 0) return false;
        return GroupMatches(Modifiers, physical.Modifiers, KeyModifiers.Shift, KeyModifiers.LeftShift, KeyModifiers.RightShift)
            && GroupMatches(Modifiers, physical.Modifiers, KeyModifiers.Control, KeyModifiers.LeftControl, KeyModifiers.RightControl)
            && GroupMatches(Modifiers, physical.Modifiers, KeyModifiers.Alt, KeyModifiers.LeftAlt, KeyModifiers.RightAlt)
            && GroupMatches(Modifiers, physical.Modifiers, KeyModifiers.Windows, KeyModifiers.LeftWindows, KeyModifiers.RightWindows);
    }

    public bool Overlaps(KeyChord other)
    {
        if (Key != other.Key) return false;
        for (var bits = 0; bits < 256; bits++)
            if (Matches(new((KeyModifiers)bits, Key)) && other.Matches(new((KeyModifiers)bits, Key))) return true;
        return false;
    }

    public IEnumerable<KeyCode> GetModifierKeys()
    {
        var modifiers = Modifiers;
        if (modifiers.HasFlag(KeyModifiers.Shift)) modifiers |= KeyModifiers.LeftShift;
        if (modifiers.HasFlag(KeyModifiers.Control)) modifiers |= KeyModifiers.LeftControl;
        if (modifiers.HasFlag(KeyModifiers.Alt)) modifiers |= KeyModifiers.LeftAlt;
        if (modifiers.HasFlag(KeyModifiers.Windows)) modifiers |= KeyModifiers.LeftWindows;
        foreach (var key in ModifierKeyCodes)
            if ((modifiers & ModifierFor(key)) != 0) yield return key;
    }

    public override string ToString()
    {
        var names = new List<string>();
        foreach (var modifier in Enum.GetValues<KeyModifiers>())
            if (modifier != KeyModifiers.None && Modifiers.HasFlag(modifier)) names.Add(modifier.ToString());
        names.Add(Key.ToString());
        return string.Join(" + ", names);
    }

    public static KeyModifiers ModifierFor(KeyCode key) => key switch
    {
        KeyCode.LeftShift => KeyModifiers.LeftShift, KeyCode.RightShift => KeyModifiers.RightShift,
        KeyCode.LeftControl => KeyModifiers.LeftControl, KeyCode.RightControl => KeyModifiers.RightControl,
        KeyCode.LeftAlt => KeyModifiers.LeftAlt, KeyCode.RightAlt => KeyModifiers.RightAlt,
        KeyCode.LeftWindows => KeyModifiers.LeftWindows, KeyCode.RightWindows => KeyModifiers.RightWindows,
        _ => KeyModifiers.None
    };

    public static IReadOnlyList<KeyCode> ModifierKeyCodes { get; } = Array.AsReadOnly(new[]
    {
        KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl,
        KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.LeftWindows, KeyCode.RightWindows
    });

    private static bool GroupMatches(KeyModifiers expected, KeyModifiers actual, KeyModifiers generic,
        KeyModifiers left, KeyModifiers right)
    {
        var sides = left | right;
        var actualSides = actual & sides;
        return expected.HasFlag(generic) ? actualSides != 0 : (expected & sides) == actualSides;
    }
}
