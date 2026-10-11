# Development verification observed on October 11, 2026

This record covers development commands executed in the workspace. It does not establish real keyboard compatibility, a supported public Windows/application matrix, installation success, Microsoft Store certification or release readiness.

## Environment and executed checks

- Host: Windows 11 Pro, build 26300. This host observation is not a declaration of supported production Windows versions.
- .NET SDK: 10.0.401, installed app-locally for development.
- Packaging tool: Microsoft's `Microsoft.Windows.SDK.BuildTools` 10.0.28000.2705 NuGet distribution. MakeAppx and SignTool executable Authenticode signatures were observed as Valid with Microsoft Corporation as signer.
- PowerShell scripts parsed without syntax errors; dotnet and SDK-tool automatic discovery returned the app-local tools.
- All 36 generated PNG assets decoded successfully. Scale and target-size variants are included.
- Solution Release build completed with 0 warnings and 0 errors.
- The final self-contained `artifacts/portable/KeyBridge.exe --smoke-test` completed with exit code 0 and rendered all seven pages. This launch used the app host and its bundled runtime, without profile writes, hooks, shortcut registration or global conversion.
- Core console harness: 27 of 27 tests passed in the frozen final source build.
- Platform console harness: 56 keyboard lifecycle/native-structure checks passed in the frozen final source build. These use deterministic state/structure checks and do not prove real input injection behavior.
- Self-contained Windows x64 publish completed with .NET Core and Windows Desktop runtime 10.0.12.
- MakeAppx `pack` completed with schema validation enabled; `/nv` was not used.
- MakePri generated the final package's complete resource index. An actual dump lists scales 100/125/150/200/400 and target sizes 16/24/32/48/64/256.
- The resulting development-only MSIX was opened as an archive. Its manifest has x64 architecture, only `runFullTrust`, and startup initially disabled. The actual app executable/assembly, native CLR, local privacy file and 36 PNG assets are present. No PDB or package signature was present in the observed unsigned candidate.

## Runtime and certification limits

A preliminary root-agent WPF smoke-launch attempt was blocked by Windows Application Control with `FileLoadException` / HRESULT `0x800711C7` while loading `KeyBridge.dll`. This was a historical failed attempt: a subsequent smoke launch of the final source succeeded without disabling or bypassing Windows policy. It rendered seven application pages to `artifacts/ui-smoke`. Rendering establishes page creation in the smoke environment; it does not establish keyboard-only/Narrator usability or real editing/IME compatibility.

A native smoke run observed the actual API/message-thread foreground context and registered, paused and disposed an empty-rule keyboard hook. Its evidence is `artifacts/native-smoke-results.json`. Ctrl+Alt+F12 was already occupied (`RegisterHotKey` error 1409), and a fallback Ctrl+Alt+F10 registration succeeded. This is evidence that registration failure/fallback can be exercised on this host; it does not guarantee either combination is available elsewhere. No `SendInput` was performed in this native smoke run and no real shortcut/IME compatibility is claimed.

The development MSIX identity `KeyBridge.Development`, publisher `CN=KeyBridge Development`, is intentionally a local test identity. It is unrelated to an owner's real Partner Center identity and cannot serve as the product's Store submission identity. No certificate trust modification, package signing, package installation, update/uninstall test, Windows App Certification Kit run or Partner Center submission was performed.

The authoritative latest package hash is its adjacent `.msix.sha256` file under `artifacts/packages`; package contents change when the app is rebuilt. Record the final candidate hash and source commit before any real-device test. A manifest-only specimen may remain under ignored `artifacts/manifest-prototype`; it contains an SDK executable placeholder and must never be installed or distributed as KeyBridge.

Final frozen-source development delivery observed in this session:

- Unsigned package: `artifacts/packages/KeyBridge.Development_0.1.0.0_x64.msix`, 82,051,102 bytes, 522 archive entries.
- Package SHA-256: `A75E1CBF8029579C0712023C2E9C376AB3E13C569468321B2047CE7609725A4F`.
- Application assembly SHA-256: `8C3D3B31AFE51173881C79428B64F2DA0B71D6F6EFCFAD20B2E543DA98271BFF`.
- Self-contained portable executable: `artifacts/portable/KeyBridge.exe`. Its application assembly hash matches the final package and latest self-contained publish. Portable startup remains unavailable because it has no installed package identity.
- Archive inspection verified original icons/resource index, local privacy policy, exact .NET runtime/desktop notices and Windows SDK license; no debug symbols or signature were present.
- Structured inspection evidence: `artifacts/package-verification.json`; final MakeAppx/MakePri logs and resource dump are under `artifacts/logs`.

The package is regenerated after the frozen final source state. `VERIFICATION.md` remains the required real-device release matrix; all unexecuted rows stay pending. `STORE_RELEASE.md` lists owner and certification prerequisites.
