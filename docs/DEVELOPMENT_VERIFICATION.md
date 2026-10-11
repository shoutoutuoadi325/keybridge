# Build and packaging record

Recorded October 11, 2026.

## Toolchain

- Windows 11 Pro build 26300.
- .NET SDK 10.0.401.
- Self-contained .NET Core and Windows Desktop runtime 10.0.12.
- Microsoft Windows SDK BuildTools 10.0.28000.2705; MakeAppx and SignTool signatures verified as Microsoft Corporation.

## Current 0.1.5.0 Store candidate

Source commit: `c5902a2794d9396bdb4e20fc3b51732aa08ee531`.

- `scripts/Package.ps1` built Release and ran all three deterministic harnesses: 54 Core tests, 75 Platform checks and 59 managed service checks passed (188 total), zero warnings/errors.
- Self-contained win-x64 publication completed with runtime 10.0.12. MakePri generated the resource index and MakeAppx packed 0.1.5.0 with validation enabled.
- Final package: `14E33A41.KeyBridge_0.1.5.0_x64.msix`, 82,063,438 bytes, SHA-256 `E6791091C317AB9F1C9529856A453245BB3A9619BB7D42B2F32D9C92E915A4BB`.
- Product identity and Publisher remain unchanged. Partner Center's current Product identity requires PublisherDisplayName `Zhiqi Zhang`; a first upload retaining the old display name was rejected and replaced.
- MakeAppx unpacked the final MSIX into a separate verification directory. Its manifest reports 0.1.5.0/x64 and the official identity/publisher/display name; the application file version is 0.1.5.0.
- The final extracted executable rendered all seven pages with exit code zero. This mode installs no hook, registers no hotkey, writes no profile and processes no global input.
- Home, Shortcuts, Try it and Settings renders were visually inspected and saved as the four English Store screenshots (2740 x 1761). The README shortcut image was refreshed from the same final payload. Reopening the Store listing confirmed four saved images and the new release notes.
- Final payload verification is recorded in `artifacts/store-0.1.5-verification/final/result.json`; rendering is under its `artifacts/ui-smoke` subdirectory. Package tool logs are `artifacts/logs/MakeAppx-5608b308aead42aca68a3e3285dead37.log` and the adjacent MakePri log.

No additional physical keyboard session, installed-package lifecycle test or Windows App Certification Kit run was performed. These checks are not certification approval. The current Partner Center result and supplied screenshots are recorded in `STORE_SUBMISSION.md`.

## Historical 0.1.4.0 observed checks

- Release build: zero warnings and errors.
- Core harness: 32 tests passed.
- Platform harness: 58 keyboard lifecycle and native structure checks passed.
- Self-contained win-x64 publication completed.
- MakeAppx packed version 0.1.4.0 with schema validation enabled.
- MakePri generated the package's resource index.
- The staged 0.1.4 executable rendered all seven application pages at native DPI with exit code zero. This rendering mode does not install keyboard hooks, register shortcuts, write profiles or process global input.
- Four rendered screenshots were supplied for the English Store listing.
- The payload includes original icons, the privacy policy, exact runtime notices and the Windows SDK license.

Version 0.1.4.0 package SHA-256: `1F485535D17033FAAF7FEA00598429947EA1E0FCA2BB1FED8647A155A0DB4A96`.

## Native keyboard session

The owner completed physical role confirmation, emergency pause and editing shortcuts in the KeyBridge editor with ENG US. Detailed observed results and certificate cleanup are in `WINDOWS_TEST_SESSION.md`.

## Submission

The package uses the owner's Partner Center identity `14E33A41.KeyBridge`, publisher `CN=BD412261-9548-4F5A-95C1-24B072A9C1CC`. The owner authorized Developer Agreement 8.11 and the IARC terms. Public contact and privacy information are supplied. Current submission status is recorded in `STORE_SUBMISSION.md`.
