# Build and packaging record

Recorded October 11, 2026.

## Toolchain

- Windows 11 Pro build 26300.
- .NET SDK 10.0.401.
- Self-contained .NET Core and Windows Desktop runtime 10.0.12.
- Microsoft Windows SDK BuildTools 10.0.28000.2705; MakeAppx and SignTool signatures verified as Microsoft Corporation.

## Observed checks

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
