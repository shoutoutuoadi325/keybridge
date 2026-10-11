# Shortcut and recovery fixes

Recorded October 11, 2026 for the working-tree fixes following commit `3342c7a`. This record covers source changes and deterministic checks; it is not a new Store submission or a fresh physical keyboard session.

## Changes

- Emergency pause validation now checks built-in and user-defined protected input-method shortcuts independently of the rule list. Save, import and runtime configuration use the same validator.
- The default recovery shortcut is Ctrl+Alt+F10. F12 cannot be registered as a recovery shortcut. The recovery thread waits for a validated profile before registering a chord, avoiding temporary registration of a default that conflicts with a user's settings.
- The exact previous Ctrl+Alt+F12 default migrates only in an otherwise valid profile. Replacement candidates are checked against the complete configuration. Rules and preferences are retained, mapping stays paused, and recovery confirmation must be repeated. Reading leaves the original file intact; the next normal save uses the existing rotating backup.
- Editing a suggested rule fills blank Windows build evidence with the current build. Nonblank historical evidence is preserved. Confirmation reports missing version/build data directly.
- All-applications mode now produces an empty-scope, disabled global proposal without application confirmation, regardless of any populated application fields or previous enabled state. Mode changes and edits to shortcut/context fields clear the current confirmation.
- Paused source presses stay suppressed until an observed non-injected key-up. Synthetic target releases, injected events and native-state polling cannot prematurely complete the drain. Resume guidance explains how to recover a release missed during sleep or lock.

## Observed verification

- `scripts/Build.ps1`: .NET SDK 10.0.401, Release build, zero warnings and zero errors.
- Core harness: 44/44 tests passed, including protected emergency chords with zero rules, custom sided modifiers, read/import/save rejection, legacy migration and collision fallback, original preservation, migration rejection, preset confirmation and global-mode behavior.
- Platform harness: 75 checks passed, including 17 added source-lifecycle checks with the actual modifier planner for matching and different source/target keys, physical repeats, injected releases, multiple sources and missed-release recovery.
- Application smoke rendering: all seven pages constructed and rendered, exit code zero. This mode installs no keyboard hook, registers no shortcut, writes no profile and processes no global input.
- `git diff --check`: passed.

Rendering output is under `artifacts/fix-verification-2026-10-11/artifacts/ui-smoke`. Home and Settings renders were visually inspected. No interactive keyboard test was performed, following AGENTS.md.

## Remaining verification boundaries

Deterministic checks and page rendering do not establish real SendInput timing, native hotkey availability, packaged startup/uninstall behavior or compatibility in every application. The new drain deliberately requires physical release evidence. If a release is missed, press and release the affected source key again before resuming. If Windows has removed the hook on timeout, restart KeyBridge. Exiting ends the hook; release held keys before continuing.

The submitted 0.1.4.0 package, screenshots and historical session records remain described by their existing release records. A new release must be built from these fixes and verified separately.

## Second audit repairs

The six additional findings recorded in `artifacts/review2/review.md` are repaired in the current source:

- Failed synthetic key release or native modifier restoration retains its recovery plan and hook drain. Resume is blocked while recovery remains unfinished. Forwarded physical key events can resolve that state without replaying a shortcut or automatically retrying input insertion. Exit acknowledges cleanup before recording a clean session; incomplete or timed-out cleanup leaves the next launch requiring a recovery rehearsal.
- Complete profile operations are serialized after initialization. Each edit reads current state inside the operation, including rule toggles by ID. Exit waits for loading and active work, rejects queued edits, and does not save defaults over a failed load. Explicit import/reset preserves rejected storage before replacement.
- Emergency events carry the live registration identity and chord through the service/UI queues. Old IDs, wrong chords, replacement failures and registrations superseded then selected again cannot certify a new shortcut. Saving the same live shortcut preserves a legitimate test. Synchronous safety generations prevent delayed enable/trial continuations and hook activation from undoing an emergency pause.
- The editor retains existing composition policy and derives `RequiresNoComposition` for sensitive keys using the compiler's shared predicate. Unchanged imported rules can be saved without losing that policy.
- The UI uses the shared shortcut parser, accepts case-insensitive Ctrl/Win/Cmd aliases, and rejects malformed separators and duplicate modifiers.
- Pending trials check their page/editor or dialog lifetime before activation. Leaving the page or closing the dialog cannot start an abandoned preview.

Observed final verification:

- `scripts/Build.ps1`: Release build passed, zero warnings/errors; 54/54 Core tests, 75 Platform checks, and 59 managed service checks passed (188 checks total).
- The new managed service project is included in the solution and the standard build script. It compiles the actual keyboard/recovery service source with managed native and foreground substitutes, covering partial and failed cleanup, physical takeover, restoration with no owned keys, exit acknowledgement, stale hotkey identity, delayed safety events, and cancellation during hook installation/disposal. It calls no production P/Invoke or desktop input APIs.
- Core queue checks cover overlapping changes, exit during loading, skipped queued edits, shared initialization, failed operations, single shutdown and failed initialization. Editor checks cover imported/stricter policies, all sensitive keys and shared parser syntax.
- Seven application pages rendered successfully in smoke mode, exit code zero. Home and Settings were visually inspected. The render mode installs no hook, registers no hotkey, writes no profile and processes no global input.
- `git diff --check` passed.

Final repair build output is `artifacts/review2/fix-build.txt`; renders are under `artifacts/review2/fix-render/artifacts/ui-smoke`. This repair verification preceded packaging and publication. The subsequent portable 0.1.5 release is recorded in `RELEASE_0.1.5.md`; no physical keyboard session or new Store submission was performed during that repair/portable-release verification. The later Store package and submission work is recorded separately in `DEVELOPMENT_VERIFICATION.md` and `STORE_SUBMISSION.md`. Native API failure can still require the user to press/release the affected keys; the repaired behavior retains that failure state and prevents an ordinary resume from discarding it.
