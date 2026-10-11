namespace KeyBridge.Core;

public static class EmergencyShortcuts
{
    public static KeyChord Default { get; } = new(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F10);
    internal static KeyChord LegacyDefault { get; } = new(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F12);

    // Alternatives are checked against the complete profile before migrating old settings.
    internal static IEnumerable<KeyChord> MigrationCandidates =>
        new[] { KeyCode.F10, KeyCode.F11, KeyCode.F9, KeyCode.F8, KeyCode.F7, KeyCode.F6,
            KeyCode.F5, KeyCode.F4, KeyCode.F3, KeyCode.F2, KeyCode.F1 }
        .Select(key => new KeyChord(KeyModifiers.Control | KeyModifiers.Alt, key));
}
