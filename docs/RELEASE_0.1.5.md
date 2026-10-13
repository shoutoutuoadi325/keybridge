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

Local evidence is under `artifacts/review2`: `release-0.1.5-build.txt`, `release-0.1.5-package.txt` and `release-0.1.5-package-verification.json`. No additional interactive keyboard session was performed. The original GitHub portable publication did not include a Microsoft Store submission; the later Store update is recorded below.

## Microsoft Store package

The later October 11, 2026 Store update uses package version `0.1.5.0`, the same application source and the official Partner Center identity `14E33A41.KeyBridge`. PublisherDisplayName is `Zhiqi Zhang`, as reported by the current Product identity page.

- Package: `14E33A41.KeyBridge_0.1.5.0_x64.msix`, 82,063,438 bytes.
- SHA-256: `E6791091C317AB9F1C9529856A453245BB3A9619BB7D42B2F32D9C92E915A4BB`.
- Release build and all 188 deterministic checks passed; MakePri and MakeAppx validation completed.
- The final extracted MSIX executable rendered all seven pages with exit code zero.

The final October 11 submission receipt reports In certification, with Submission Complete and Pre-processing In progress at submission time. KeyBridge is now published on the Microsoft Store: https://apps.microsoft.com/detail/9nk4xxk1jh04?ocid=webpdpshare. The current submission status, reviewer instructions and receipts are maintained in `STORE_SUBMISSION.md`. `VERIFICATION.md` distinguishes current automated evidence from historical physical observations and unrun reference cases.
