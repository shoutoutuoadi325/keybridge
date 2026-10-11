using KeyBridge.Core;

internal static class RuleEditorChecks
{
    public static void Register(Action<string, Action> test)
    {
        test("Editor supplies the current Windows build for blank preset evidence", () =>
        {
            const string currentBuild = "10.0.26100.0";
            var preset = PresetCatalog.CreateRules("editor.exe").First();
            foreach (var stored in new[] { "", "  " })
            {
                var proposal = preset with
                {
                    Verification = new()
                    {
                        ApplicationVersion = "1.0",
                        WindowsBuild = RuleEditorChanges.InitialWindowsBuild(new() { WindowsBuild = stored }, currentBuild)
                    }
                };
                var saved = RuleEditorChanges.FromProposal(proposal, false, preset.Enabled, true);
                Check(!saved.Enabled && saved.Verification.WindowsBuild == currentBuild);
                var compiled = RuleCompiler.Compile(new AppProfile
                {
                    Enabled = true, ConsentGranted = true, RoleKeyConfirmed = true, RecoveryTested = true,
                    Rules = [saved with { Enabled = true }]
                });
                Check(compiled.Success, string.Join("; ", compiled.Issues.Select(issue => issue.Message)));
                var context = new ForegroundContext("editor.exe", PermissionState.Allowed,
                    new(InputSource.EnglishKeyboard, ImeMode.NotApplicable, CompositionState.NotComposing), "1.0", WindowsBuild: currentBuild);
                Check(compiled.Snapshot!.Match(new(KeyModifiers.LeftAlt, KeyCode.C), context).ShouldTransform);
            }
            Check(RuleEditorChanges.InitialWindowsBuild(null, currentBuild) == currentBuild);
        });
        test("Editor preserves the Windows build of existing confirmation", () =>
        {
            var existing = Proposal();
            Check(RuleEditorChanges.InitialWindowsBuild(existing.Verification, "10.0.26200.0") == "10.0.26100.0");
        });
        test("All-applications mode overrides populated process and existing enabled state", () =>
        {
            var existing = Proposal();
            foreach (var confirm in new[] { false, true })
            {
                var saved = RuleEditorChanges.FromProposal(existing, true, true, confirm);
                Check(saved.Scope.ProcessName == "" && saved.Origin == RuleOrigin.UserGlobal && !saved.Enabled);
                Check(saved.Verification.VerifiedContexts.Count == 0);
                Check(RuleCompiler.Compile(new AppProfile { Rules = [saved] }).Success);
                Check(existing.Enabled && existing.Scope.ProcessName == "Editor.EXE" && existing.Verification.VerifiedContexts.Count == 1);
            }
            var blank = existing with { Scope = new() };
            Check(RuleEditorChanges.FromProposal(blank, true, true, true).Origin == RuleOrigin.UserGlobal);
        });
        test("Application mode requires a process and respects explicit reconfirmation", () =>
        {
            var existing = Proposal();
            var saved = RuleEditorChanges.FromProposal(existing, false, true, true);
            Check(saved.Scope.ProcessName == "editor.exe" && saved.Origin == RuleOrigin.UserApplication && saved.Enabled);
            Check(saved.Verification.VerifiedContexts.SequenceEqual([VerifiedContext.EnglishKeyboard]));
            var unconfirmed = RuleEditorChanges.FromProposal(existing, false, true, false);
            Check(!unconfirmed.Enabled && unconfirmed.Verification.VerifiedContexts.Count == 0);
            ThrowsWithMessage(() => RuleEditorChanges.FromProposal(existing with { Scope = new() }, false, true, true), "application scope");
        });
        test("Confirmed application saves report missing metadata directly", () =>
        {
            var proposal = Proposal();
            ThrowsWithMessage(() => RuleEditorChanges.FromProposal(proposal with
            {
                Verification = proposal.Verification with { ApplicationVersion = " " }
            }, false, false, true), "application file version");
            ThrowsWithMessage(() => RuleEditorChanges.FromProposal(proposal with
            {
                Verification = proposal.Verification with { WindowsBuild = " " }
            }, false, false, true), "Windows build");
            var unconfirmed = RuleEditorChanges.FromProposal(proposal with { Verification = new() }, false, true, false);
            Check(!unconfirmed.Enabled && unconfirmed.Verification.VerifiedContexts.Count == 0);
        });
        test("Editor preserves imported composition policy with unchanged visible chords", () =>
        {
            foreach (var sourceKey in new[] { KeyCode.Left, KeyCode.C })
            {
                var existing = Proposal() with
                {
                    Source = new(KeyModifiers.Command, sourceKey), ContextPolicy = ContextPolicy.RequiresNoComposition
                };
                var draft = existing with { ContextPolicy = ContextPolicy.VerifiedImeCompatibility };
                var saved = RuleEditorChanges.FromProposal(draft, false, true, true, existing: existing);
                Check(saved.ContextPolicy == ContextPolicy.RequiresNoComposition && saved.Enabled);
                Check(RuleCompiler.Compile(new AppProfile { Rules = [saved] }).Success);
            }
            var strict = Proposal() with { ContextPolicy = ContextPolicy.RequiresNoComposition };
            var changed = strict with { Source = new(KeyModifiers.Command, KeyCode.V), ContextPolicy = ContextPolicy.VerifiedImeCompatibility };
            Check(RuleEditorChanges.FromProposal(changed, false, true, true, existing: strict).ContextPolicy == ContextPolicy.RequiresNoComposition);
        });
        test("Editor derives compiler composition protection for new or changed sensitive keys", () =>
        {
            var original = Proposal();
            var sensitive = new[] { KeyCode.Backspace, KeyCode.Delete, KeyCode.Left, KeyCode.Right,
                KeyCode.Up, KeyCode.Down, KeyCode.Home, KeyCode.End, KeyCode.PageUp, KeyCode.PageDown,
                KeyCode.Enter, KeyCode.Escape, KeyCode.Space };
            foreach (var key in sensitive)
            {
                var draft = original with { Source = new(KeyModifiers.Command, key) };
                foreach (var existing in new RuleDefinition?[] { null, original })
                {
                    var saved = RuleEditorChanges.FromProposal(draft, false, false, true, existing: existing);
                    Check(saved.ContextPolicy == ContextPolicy.RequiresNoComposition);
                    Check(RuleCompiler.Compile(new AppProfile { Rules = [saved] }).Success);
                    var withoutGuard = saved with { ContextPolicy = ContextPolicy.VerifiedImeCompatibility };
                    Check(RuleCompiler.Compile(new AppProfile { Rules = [withoutGuard] }).Issues.Any(issue => issue.Code == "CompositionProtection"));
                }
                // Disabled future targets retain the same core safeguard even though the
                // current application editor only accepts Ctrl + letter targets.
                var targetDraft = original with { Target = new(KeyModifiers.Control, key) };
                var targetSaved = RuleEditorChanges.FromProposal(targetDraft, true, false, false);
                Check(targetSaved.ContextPolicy == ContextPolicy.RequiresNoComposition);
            }
            Check(RuleEditorChanges.FromProposal(original, false, false, true).ContextPolicy == ContextPolicy.VerifiedImeCompatibility);
        });
        test("Shared parser accepts shortcut aliases regardless of letter case", () =>
        {
            foreach (var text in new[] { "Ctrl + C", "ctrl + c", "CTRL + C", "cTrL + Shift + z" })
            {
                Check(KeyChordParser.TryParse(text, out var chord));
                Check(chord.Modifiers.HasFlag(KeyModifiers.Control));
            }
            foreach (var text in new[] { "Win + Space", "win + space", "WIN + SPACE", "wIn + Space" })
            {
                Check(KeyChordParser.TryParse(text, out var chord));
                Check(chord == new KeyChord(KeyModifiers.Windows, KeyCode.Space));
            }
            foreach (var text in new[] { "Cmd + C", "cmd + c", "CMD + C", "cMd + C" })
            {
                Check(KeyChordParser.TryParse(text, out var chord));
                Check(chord == new KeyChord(KeyModifiers.Command, KeyCode.C));
            }
        });
        test("Shared parser rejects malformed separators and duplicate aliases", () =>
        {
            foreach (var text in new[] { "Ctrl + + C", "+ Ctrl + C", "Ctrl + C +", "Ctrl + Ctrl + C",
                "ctrl + Control + C", "Win + win + L", "Cmd + Command + C", "Ctrl + LeftControl + C" })
                Check(!KeyChordParser.TryParse(text, out _), "Unexpectedly accepted " + text);
        });
    }

    private static RuleDefinition Proposal() => new()
    {
        Id = "copy", Intent = "Copy", Source = new(KeyModifiers.Command, KeyCode.C),
        Target = new(KeyModifiers.LeftControl, KeyCode.C), Scope = new() { ProcessName = "Editor.EXE" },
        Enabled = true, Verification = new()
        {
            ApplicationVersion = "1.0", WindowsBuild = "10.0.26100.0", VerifiedContexts = [VerifiedContext.EnglishKeyboard]
        }
    };

    private static void Check(bool condition, string message = "Assertion failed.")
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void ThrowsWithMessage(Action action, string expected)
    {
        try { action(); }
        catch (InvalidOperationException exception) when (exception.Message.Contains(expected, StringComparison.Ordinal)) { return; }
        throw new InvalidOperationException("Expected editor validation mentioning " + expected);
    }
}
