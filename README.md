# KeyBridge

KeyBridge is a local Windows desktop shortcut conversion app for people moving between Mac and Windows keyboard habits. It uses an ordinary-user WPF process, temporary documented keyboard APIs and a conservative input-context policy. Conversion requires an explicit choice and can be paused from the window, tray or emergency shortcut. No driver, service, elevation, text logging, account or telemetry is part of the core design.

The first release targets Windows 11 x64. This repository is an implementation and verification baseline; published compatibility and Microsoft Store approval require the release gates below. External Pinyin mode/composition cannot be inferred from a layout identifier. Unknown contexts preserve native input, and optional navigation/input-source actions must remain disabled until verified.

This development build allows basic shortcut conversion only for ENG US and ordinary-permission contexts. Application presets start disabled and unverified. After consent, physical-role confirmation and emergency-pause setup, Try it can run a temporary own-editor trial; losing editor focus ends that trial. The rule editor can start a temporary single-rule trial in one selected external application. These trials create no compatibility evidence automatically. Enable a saved application rule only after recording its actual local result; Pinyin, unsupported layouts, optional navigation and unknown context keep native behavior.

## Build and run

Install the supported .NET 10 SDK on Windows 11. From this directory:

```powershell
./scripts/Build.ps1
./scripts/Run.ps1
```

Build and Run support `-DotNetPath` for a non-PATH SDK. Build runs the deterministic core and platform harnesses. The app starts safely paused. Read the first-run disclosure, select and test the physical Command role, inspect applications and conflicts, and explicitly enable conversion. Ctrl+Alt+F12 is the default emergency pause; it must register successfully before enabling. Exit in the tray stops the application. Releasing and pressing an affected modifier once is the recovery step after an unexpected termination.

## Project map

- `src/KeyBridge.Core`: declarative profiles, validation, rule matching and engine state logic.
- `src/KeyBridge.App`: English desktop interface, Windows adapters, keyboard engine, recovery and tray controls.
- `tests/KeyBridge.Core.Tests`: dependency-light deterministic core verification.
- `tests/KeyBridge.Platform.Tests`: deterministic keyboard transition and safety checks through simulated platform adapters.
- `tests/KeyBridge.Platform.Smoke`: deliberately invoked real API infrastructure smoke with zero conversion rules and no SendInput.
- `scripts`: build, original icon generation, self-contained MSIX packaging and optional local test signing.
- `packaging`: identity-parameterized manifest with only `runFullTrust`, opt-in startup and original PNG artwork.
- `docs`: [privacy policy](docs/PRIVACY.md), [API review](docs/API_REVIEW.md), [verification matrix](docs/VERIFICATION.md), [Store release gates](docs/STORE_RELEASE.md) and [third-party notices](docs/THIRD_PARTY_NOTICES.md).

## Package and release

Use `scripts/Package.ps1` with real identity values from Partner Center and a Windows SDK MakeAppx installation. The script builds/tests, publishes self-contained `win-x64`, validates the manifest while packaging and emits an unsigned MSIX plus its SHA-256. A local installation needs a deliberately trusted signing certificate whose subject matches the manifest; `scripts/Sign-Package.ps1` supports an existing Current User certificate. It never changes certificate trust.

Follow [STORE_RELEASE.md](docs/STORE_RELEASE.md) before publishing. Outstanding owner/release tasks include real product identity, public privacy URL and support contact, name review, true screenshots/metadata and exact-binary certification and real keyboard/IME compatibility evidence. Compilation and simulated tests do not prove safe IME behavior or Store certification. English is the implementation language; only actually implemented and verified UI languages may be declared in the package/Store.

The development delivery includes a self-contained executable at `artifacts/portable/KeyBridge.exe`. It can run without a separately installed .NET runtime; packaged login startup is unavailable in this portable build. The unsigned development MSIX under `artifacts/packages` requires signing/trust for ordinary local installation. See [DEVELOPMENT_VERIFICATION.md](docs/DEVELOPMENT_VERIFICATION.md) for checks actually observed and their limits.

KeyBridge source is licensed under [GPL-3.0](LICENSE). Self-contained .NET runtime notices must be preserved and audited in the actual release payload.
