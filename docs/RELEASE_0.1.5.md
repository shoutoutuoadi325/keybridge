# KeyBridge 0.1.5

Recorded October 11, 2026. Source tag: `v0.1.5`. This release contains the keyboard recovery, emergency registration, profile workflow and editor fixes documented in `BUGFIX_VERIFICATION.md`.

## Portable Windows download

- Archive: `KeyBridge-0.1.5-win-x64.zip` (83,193,368 bytes).
- SHA-256: `b1ebe8816623b61bd553ab40acbb4dbe28e97ae3e6fc9a416d7a75412488a422`.
- Adjacent checksum file: `KeyBridge-0.1.5-win-x64.zip.sha256`.
- Application assembly version: `0.1.5.0`.
- The ZIP includes the .NET and Windows Desktop runtimes, GPL license, runtime notices, privacy policy and extraction instructions.

The download targets Windows 11 x64 and ENG US in ordinary-permission application scopes, with Ctrl plus letter targets and optional Shift. Extract all files together and open `KeyBridge.exe`. The default emergency pause is Ctrl+Alt+F10. Previous exact Ctrl+Alt+F12 settings migrate to a compatible shortcut and require confirmation before mapping resumes.

## Observed release verification

- Standard Release build: zero warnings/errors; 54 Core tests, 75 Platform checks and 59 managed service checks passed.
- The generated archive was extracted into a fresh directory. Required executable, application, runtime, license and notice files were present.
- The extracted self-contained `KeyBridge.exe --smoke-test` exited with code zero and constructed/rendered all seven pages. This mode installs no keyboard hook, registers no hotkey, writes no profile and processes no global input.
- `git diff --check` passed before committing.

Local evidence is under `artifacts/review2`: `release-0.1.5-build.txt`, `release-0.1.5-package.txt` and `release-0.1.5-package-verification.json`. No additional interactive keyboard session was performed. This GitHub portable release does not constitute a new Microsoft Store submission.
