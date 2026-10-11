namespace KeyBridge.Core;

public static class RuleEditorChanges
{
    public static string InitialWindowsBuild(VerificationEvidence? evidence, string currentBuild) =>
        string.IsNullOrWhiteSpace(evidence?.WindowsBuild) ? currentBuild : evidence.WindowsBuild;

    /// <summary>
    /// Application confirmation applies to one process. A global proposal stays
    /// disabled and cannot inherit that application's confirmation or enabled state.
    /// </summary>
    public static RuleDefinition FromProposal(RuleDefinition proposal, bool allApplications,
        bool wasEnabled, bool confirmApplication, RuleDefinition? existing = null)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var processName = allApplications ? "" : proposal.Scope.ProcessName.Trim().ToLowerInvariant();
        if (!allApplications && processName.Length == 0)
            throw new InvalidOperationException("Choose an application scope or explicitly opt into a disabled global proposal.");
        var evidence = proposal.Verification with
        {
            ApplicationVersion = proposal.Verification.ApplicationVersion.Trim(),
            WindowsBuild = proposal.Verification.WindowsBuild.Trim(),
            Notes = proposal.Verification.Notes.Trim(),
            VerifiedContexts = !allApplications && confirmApplication ? [VerifiedContext.EnglishKeyboard] : []
        };
        if (!allApplications && confirmApplication)
        {
            if (evidence.ApplicationVersion.Length == 0)
                throw new InvalidOperationException("Enter the application file version before confirming this shortcut.");
            if (evidence.WindowsBuild.Length == 0)
                throw new InvalidOperationException("Enter the Windows build before confirming this shortcut.");
        }
        return proposal with
        {
            Scope = new RuleScope { ProcessName = processName },
            Origin = allApplications ? RuleOrigin.UserGlobal : RuleOrigin.UserApplication,
            Enabled = !allApplications && wasEnabled && confirmApplication,
            // The editor does not expose a policy selector. Retain imported restrictions,
            // and apply the same idle-composition safeguard required by the compiler
            // when an edited or new source/target uses a sensitive key.
            ContextPolicy = ShortcutSupport.RequiresNoComposition(proposal.Source.Key)
                || ShortcutSupport.RequiresNoComposition(proposal.Target.Key)
                ? ContextPolicy.RequiresNoComposition : existing?.ContextPolicy ?? proposal.ContextPolicy,
            Verification = evidence
        };
    }
}
