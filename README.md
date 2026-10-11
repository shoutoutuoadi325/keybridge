# KeyBridge

KeyBridge brings familiar Command-style shortcuts to your Windows desktop. Choose a physical role key, personalize shortcuts for individual applications, and keep control with window, tray and emergency pause controls.

KeyBridge runs on Windows 11 x64 with an English interface. Application shortcuts use Ctrl plus a letter, optionally with Shift, with the ENG US keyboard layout in ordinary-permission applications. Presets include Microsoft Edge, Google Chrome, Visual Studio Code and the built-in editor. Confirm your chosen shortcuts for each application's settings before enabling them. Microsoft Pinyin, other layouts, elevated windows and contexts outside your confirmed settings retain native input behavior.

Keyboard processing stays on your device. KeyBridge uses documented Windows APIs without a driver, service or administrator access. It does not record keyboard history, read clipboard contents or upload input content. Optional login startup respects Windows settings and starts paused.

## Build and run

Use the .NET 10 SDK on Windows 11:

```powershell
./scripts/Build.ps1
./scripts/Run.ps1
```

Both scripts accept `-DotNetPath`. Build runs the core and platform harnesses. Complete setup on Home, choose your physical Command key and confirm your emergency pause shortcut. Ctrl+Alt+F12 is the default; choose a different combination in Settings if Windows has already assigned it. Use Try it to preview editing actions, then configure and confirm shortcuts in their target applications.

## Project map

- `src/KeyBridge.Core`: profiles, validation, rule matching and engine state.
- `src/KeyBridge.App`: English desktop interface, Windows adapters and tray controls.
- `tests`: core, platform and explicitly invoked native API harnesses.
- `scripts`: build, assets, self-contained MSIX packaging and local signing tools.
- `packaging`: package manifest, opt-in startup and original icons.
- `docs`: [privacy policy](docs/PRIVACY.md), [API references](docs/API_REVIEW.md), [test records](docs/WINDOWS_TEST_SESSION.md), [packaging instructions](docs/STORE_RELEASE.md) and [third-party notices](docs/THIRD_PARTY_NOTICES.md).

## Package

`scripts/Package.ps1` takes the exact Partner Center identity, publisher and version, publishes self-contained `win-x64`, and uses the Windows SDK to create an MSIX and SHA-256 file. Microsoft Store handles distribution signing. For a direct local installation, use a signing certificate matching the publisher; `scripts/Sign-Package.ps1` uses an existing Current User certificate and does not change certificate trust.

Store ID: `9NK4XXK1JH04`. Public support: zhiqizhangg@gmail.com. [Privacy policy](docs/PRIVACY.md).

KeyBridge is licensed under [GPL-3.0](LICENSE). The package includes the applicable .NET and Windows SDK runtime notices.
