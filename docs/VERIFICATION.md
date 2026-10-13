# Release verification record

Current candidate: KeyBridge 0.1.5.0, source commit `c5902a2`, recorded October 11, 2026. The Release build passed 54 Core tests, 75 Platform checks and 59 managed service checks (188 total), with zero warnings/errors. Store package validation and rendering evidence are recorded below and in `STORE_SUBMISSION.md`.

The owner completed the earlier October 11 physical-keyboard local-editor actions and emergency pause on 0.1.3.0; see `WINDOWS_TEST_SESSION.md`. Those observations are historical and are not a new physical session for 0.1.5.0. The earlier 0.1.4.0 build had 32 Core tests and 58 Platform checks. The real Windows tables below remain reference procedures, not unobserved passes.

## Evidence to record

For each run: date/time, tester, commit, package SHA-256, Windows edition/build/update, CPU/RAM, physical keyboard and its actual virtual-key identity, keyboard layout, application exact version, Pinyin compatibility mode and customized switching/full-width/punctuation settings, engine profile, expected/actual result and reproducible steps. Use synthetic test text. Screenshots and diagnostic exports must not contain personal input, passwords or document content.

## Automated checks

`scripts/Build.ps1` builds the solution and runs the deterministic core, platform and managed service console harnesses. Record its actual command, SDK version, count and output in the candidate evidence file. Automated tests verify rule/configuration/state logic and simulated platform transitions; they cannot establish external app, IME, real SendInput, hook timing, startup or uninstall behavior.

| Check | Record | Evidence |
| --- | --- | --- |
| Build and deterministic harnesses | Passed | Release build; 54 Core tests, 75 Platform checks and 59 managed service checks; zero warnings/errors |
| Self-contained x64 publish | Passed | .NET 10.0.12; win-x64 |
| MakeAppx schema/payload validation | Passed | Microsoft Windows SDK BuildTools 10.0.28000.2705; package version 0.1.5.0 |
| Store package acceptance | Validated | Final 0.1.5.0 MSIX accepted with Zhiqi Zhang; runFullTrust approval remains part of certification |
| UI rendering | Passed | Seven pages from the final extracted 0.1.5.0 MSIX; exit code zero; no hook, hotkey or profile writes |
| English Store listing screenshots | Saved | Four 2740 x 1761 renders of Home, Shortcuts, Try it and Settings from the final package |
| Store submission | Published | October 11, 2026 final receipt captured the in-certification state (Submission Complete, Pre-processing In progress). KeyBridge is now published on Microsoft Store: https://apps.microsoft.com/detail/9nk4xxk1jh04?ocid=webpdpshare |
| Physical keyboard, packaged startup/update/uninstall and WACK | Not run for 0.1.5.0 | No additional interactive keyboard session or installed-package certification-kit run |

## Required real Windows cases

| ID | Procedure | Expected behavior | Record |
| --- | --- | --- | --- |
| T01 | First launch; type in another app before consent | No global conversion | Reference case |
| T02 | ENG basic edits in each enabled browser/editor; repeat native Ctrl actions | Target action correct; native input preserved | Reference case |
| T03 | Pinyin Chinese, Pinyin English, Cmd+Shift+Z in every declared compatible app | Correct verified redo; no mode change/stuck Shift; contexts outside confirmed settings pass through | Reference case |
| T04 | During Pinyin candidate entry press space, digits, arrows, Enter, Esc | Candidate selection, confirmation and cancellation unchanged | Reference case |
| T05 | Composition plus optional navigation | No conversion in composition/unknown state; no text loss | Reference case |
| T06 | Unknown/unsupported mode, composition, layout or foreground | Accurate unknown/blocked status; state-dependent rules pass through | Reference case |
| T07 | At least three input sources, optional rotation | Only if implemented/verified: next source in system order; never claim binary Chinese/English | Reference case |
| T08 | Return to Pinyin that was in English mode | Known actual status or Unknown; never falsely claim Chinese | Reference case |
| T09 | Modify Pinyin Shift/full-width/punctuation switching settings | Conflict warning; no unintended isolated modifier events | Reference case |
| T10 | Role-key holds, repeat, left/right modifiers, all release orders, simultaneous keys, unmatched combos | No loss, stuck key, duplicate action or unexpected Alt/Win menu | Reference case |
| T11 | Change foreground during a synthesized shortcut | No remaining shortcut sent to the wrong application; clear safe pause on uncertainty | Reference case |
| T12 | Elevated/unknown-integrity window, locked screen and return | No conversion/elevation; native input and security behavior preserved | Reference case |
| T13 | Edit/import Win+L and Ctrl+Alt+Delete overrides and emergency-channel collision | Configuration rejected; reserved combinations remain native | Reference case |
| T14 | Main-window pause, tray pause, emergency pause, registration collision, Resume and Exit | Under one second pause; user-driven recovery; Exit ends hook; own synthesized state cleaned | Reference case |
| T15 | Terminate during input, restart; release and press affected modifier | Safe paused restart with truthful crash recovery guidance | Reference case |
| T16 | Packaged opt-in startup; disable via Windows; sign out/in after explicit pause | Off by default; Windows disablement respected; explicit pause remains saved | Reference case |
| T17 | Oversized, malformed, future-schema and invalid-rule imports; corrupt local save | Reject safely; preserve previous config; safe pause where integrity is lost | Reference case |
| T18 | Update presets and installed package after user edits/disablements/pause | User intent retained; changed conflicts shown | Reference case |
| T19 | Review local data, diagnostic files, process network behavior across all UI flows | No input/sequence/window-title history or automatic upload | Reference case |
| T20 | Exit and uninstall; continue typing, inspect startup/services/permanent mappings | No engine, driver, service or permanent keyboard mapping remains | Reference case |
| T21 | Sleep/wake, lock/unlock, long input session and quick app switching | Safe state transitions and no input loss/stuck keys | Reference case |
| T22 | Narrator, keyboard-only navigation, high contrast, Windows text scaling | Named controls, visible focus, usable layout, status not color-only | Reference case |
| T23 | Other remapper on/off, external injected input, AltGr, dead keys, non-US layout | External injection not remapped; unsupported layouts conservative; no AltGr breakage | Reference case |

T07 may be explicitly recorded as disabled/not shipped after confirming the UI and metadata contain no enabled claim. A disabled conditional feature cannot be marked as a passed compatibility test.

Cover at least one real Windows keyboard and an Apple keyboard before making an Apple keyboard claim. Cover ordinary/elevated/unknown permissions, all declared Windows 11 versions, Edge/browser and code editor exact versions, ENG and both Pinyin modes, Pinyin legacy compatibility and modern mode, customized switching keys, composing/candidate/not composing, every declared layout, and coexisting remappers. Terminal presets remain off unless separately proven.

## Performance record

| Measurement | Acceptance target | Actual / method |
| --- | --- | --- |
| Hook callback duration | p99 < 2 ms on reference hardware; no blocking work | Reference case |
| Added conversion latency | p95 < 10 ms for verified simple combos | Reference case; define physical-event to final-emission interval |
| Tray idle CPU | Average < 0.5% over sustained idle sample | Reference case; define sample duration and CPU normalization |
| Tray working set | <= 120 MB target | Reference case; separately report settings-window open |
| Pause feedback | <= 1 second in normal use | Reference case |
| Reliability | Long input, focus, sleep/lock without loss/stuck keys | Reference case |
| Hook invalidation | Truthful unavailable/uncertain status; no visible probe keystrokes | Reference case; document detection limits |

Create `artifacts/verification/<candidate-version>.md` with observed outcomes and links to evidence. Exclude personal data. Only rules/contexts listed with real passing evidence may be described as supported in release metadata.
