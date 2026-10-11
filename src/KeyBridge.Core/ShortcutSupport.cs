namespace KeyBridge.Core;

/// <summary>The current engine's target scope, shared by validation, trials, and emission planning.</summary>
public static class ShortcutSupport
{
    private const KeyModifiers SupportedModifiers = KeyModifiers.Control | KeyModifiers.LeftControl | KeyModifiers.RightControl
        | KeyModifiers.Shift | KeyModifiers.LeftShift | KeyModifiers.RightShift;
    private const KeyModifiers ControlModifiers = KeyModifiers.Control | KeyModifiers.LeftControl | KeyModifiers.RightControl;

    public static bool RequiresNoComposition(KeyCode key) => key is KeyCode.Backspace or KeyCode.Delete
        or KeyCode.Left or KeyCode.Right or KeyCode.Up or KeyCode.Down or KeyCode.Home or KeyCode.End
        or KeyCode.PageUp or KeyCode.PageDown or KeyCode.Enter or KeyCode.Escape or KeyCode.Space;

    public static bool IsSupportedTarget(KeyChord? target) => target is not null
        && RuleCompiler.ValidChord(target, allowCommand: false)
        && target.Key is >= KeyCode.A and <= KeyCode.Z
        && (target.Modifiers & ~SupportedModifiers) == 0
        && (target.Modifiers & ControlModifiers) != 0;
}
