# Release verification record

Status: real-device release verification pending. No row below is marked passed by source creation, a successful build or simulated tests. Record automated results separately from real Windows results. Any input loss, unintended IME switch, unrecoverable stuck modifier, impaired native security shortcut or privacy failure blocks release.

## Evidence to record

For each run: date/time, tester, commit, package SHA-256, Windows edition/build/update, CPU/RAM, physical keyboard and its actual virtual-key identity, keyboard layout, application exact version, Pinyin compatibility mode and customized switching/full-width/punctuation settings, engine profile, expected/actual result and reproducible steps. Use synthetic test text. Screenshots and diagnostic exports must not contain personal input, passwords or document content.

## Automated checks

`scripts/Build.ps1` builds the solution and runs the deterministic core and platform console harnesses. Record its actual command, SDK version, count and output in the candidate evidence file. Automated tests verify rule/configuration/state logic and simulated platform transitions; they cannot establish external app, IME, real SendInput, hook timing, startup or uninstall behavior.

| Check | Result | Evidence |
| --- | --- | --- |
| Build and core harness | Pending | Add actual build log |
| Self-contained x64 publish | Pending | Add output/runtimes |
| MakeAppx schema/payload validation | Pending | Add exact SDK/tool command |
| Development signature and install | Pending | Add package hash and trust setup |
| Windows App Certification Kit | Pending | Attach report and reviewed failures |

## Required real Windows cases

| ID | Procedure | Expected behavior | Result |
| --- | --- | --- | --- |
| T01 | First launch; type in another app before consent | No global conversion | Pending |
| T02 | ENG basic edits in each enabled browser/editor; repeat native Ctrl actions | Target action correct; native input preserved | Pending |
| T03 | Pinyin Chinese, Pinyin English, Cmd+Shift+Z in every declared compatible app | Correct verified redo; no mode change/stuck Shift; unverified context passes through | Pending |
| T04 | During Pinyin candidate entry press space, digits, arrows, Enter, Esc | Candidate selection, confirmation and cancellation unchanged | Pending |
| T05 | Composition plus optional navigation | No conversion in composition/unknown state; no text loss | Pending |
| T06 | Unknown/unsupported mode, composition, layout or foreground | Accurate unknown/blocked status; state-dependent rules pass through | Pending |
| T07 | At least three input sources, optional rotation | Only if implemented/verified: next source in system order; never claim binary Chinese/English | Pending |
| T08 | Return to Pinyin that was in English mode | Known actual status or Unknown; never falsely claim Chinese | Pending |
| T09 | Modify Pinyin Shift/full-width/punctuation switching settings | Conflict warning; no unintended isolated modifier events | Pending |
| T10 | Role-key holds, repeat, left/right modifiers, all release orders, simultaneous keys, unmatched combos | No loss, stuck key, duplicate action or unexpected Alt/Win menu | Pending |
| T11 | Change foreground during a synthesized shortcut | No remaining shortcut sent to the wrong application; clear safe pause on uncertainty | Pending |
| T12 | Elevated/unknown-integrity window, locked screen and return | No conversion/elevation; native input and security behavior preserved | Pending |
| T13 | Edit/import Win+L and Ctrl+Alt+Delete overrides and emergency-channel collision | Configuration rejected; reserved combinations remain native | Pending |
| T14 | Main-window pause, tray pause, emergency pause, registration collision, Resume and Exit | Under one second pause; user-driven recovery; Exit ends hook; own synthesized state cleaned | Pending |
| T15 | Terminate during input, restart; release and press affected modifier | Safe paused restart with truthful crash recovery guidance | Pending |
| T16 | Packaged opt-in startup; disable via Windows; sign out/in after explicit pause | Off by default; Windows disablement respected; explicit pause remains saved | Pending |
| T17 | Oversized, malformed, future-schema and invalid-rule imports; corrupt local save | Reject safely; preserve previous config; safe pause where integrity is lost | Pending |
| T18 | Update presets and installed package after user edits/disablements/pause | User intent retained; changed conflicts shown | Pending |
| T19 | Review local data, diagnostic files, process network behavior across all UI flows | No input/sequence/window-title history or automatic upload | Pending |
| T20 | Exit and uninstall; continue typing, inspect startup/services/permanent mappings | No engine, driver, service or permanent keyboard mapping remains | Pending |
| T21 | Sleep/wake, lock/unlock, long input session and quick app switching | Safe state transitions and no input loss/stuck keys | Pending |
| T22 | Narrator, keyboard-only navigation, high contrast, Windows text scaling | Named controls, visible focus, usable layout, status not color-only | Pending |
| T23 | Other remapper on/off, external injected input, AltGr, dead keys, non-US layout | External injection not remapped; unsupported layouts conservative; no AltGr breakage | Pending |

T07 may be explicitly recorded as disabled/not shipped after confirming the UI and metadata contain no enabled claim. A disabled conditional feature cannot be marked as a passed compatibility test.

Cover at least one real Windows keyboard and an Apple keyboard before making an Apple keyboard claim. Cover ordinary/elevated/unknown permissions, all declared Windows 11 versions, Edge/browser and code editor exact versions, ENG and both Pinyin modes, Pinyin legacy compatibility and modern mode, customized switching keys, composing/candidate/not composing, every declared layout, and coexisting remappers. Terminal presets remain off unless separately proven.

## Performance record

| Measurement | Acceptance target | Actual / method |
| --- | --- | --- |
| Hook callback duration | p99 < 2 ms on reference hardware; no blocking work | Pending |
| Added conversion latency | p95 < 10 ms for verified simple combos | Pending; define physical-event to final-emission interval |
| Tray idle CPU | Average < 0.5% over sustained idle sample | Pending; define sample duration and CPU normalization |
| Tray working set | <= 120 MB target | Pending; separately report settings-window open |
| Pause feedback | <= 1 second in normal use | Pending |
| Reliability | Long input, focus, sleep/lock without loss/stuck keys | Pending |
| Hook invalidation | Truthful unavailable/uncertain status; no visible probe keystrokes | Pending; document detection limits |

Create `artifacts/verification/<candidate-version>.md` with observed outcomes and links to evidence. Exclude personal data. Only rules/contexts listed with real passing evidence may be described as supported in release metadata.
