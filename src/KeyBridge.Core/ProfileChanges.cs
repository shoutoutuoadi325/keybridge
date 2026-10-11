namespace KeyBridge.Core;

public static class ProfileChanges
{
    /// <summary>
    /// A different physical Command role changes the source event lifecycle. Previous
    /// Command-alias trial evidence cannot authorize that newly resolved combination.
    /// Explicit sources retain their evidence because their physical combination is unchanged.
    /// </summary>
    public static AppProfile WithCommandKey(AppProfile profile, KeyCode commandKey)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.CommandKey == commandKey) return profile;
        return profile with
        {
            CommandKey = commandKey,
            Enabled = false,
            RoleKeyConfirmed = false,
            Rules = profile.Rules.Select(rule => rule.Source.Modifiers.HasFlag(KeyModifiers.Command)
                ? rule with { Enabled = false, Verification = new VerificationEvidence() }
                : rule).ToList()
        };
    }
}
