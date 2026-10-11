# Microsoft Store release preparation

This is a release gate, not a certificate of compliance. KeyBridge uses a packaged, ordinary-user WPF desktop process. Calling a documented API or successfully compiling does not establish Store approval. The source design's real-device compatibility and privacy tests remain required.

## Applicable policy

Checked against official Microsoft documentation on October 11, 2026. [Store Policies 7.19](https://learn.microsoft.com/en-us/windows/apps/publish/store-policy-archive/store-policy-7-19) apply before October 22, 2026; [7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies) is published and takes effect on that date. Recheck the effective version on the actual submission date.

Required review areas include accurate feature descriptions and limitations, supported Windows behavior with user consent, responsive operation and clean removal, privacy-policy access and capabilities tied to actual functionality. The desktop product needs a privacy policy even when it does not upload data. Store metadata must match the languages the app actually supports.

## Package model and capability

`packaging/AppxManifest.template.xml` declares x64, the Windows desktop device family, a Windows 11 build 22000 minimum, and `runFullTrust` only. The maximum tested build value in a manifest is a compatibility declaration used by Windows; its presence is not proof that a test was run. Update that value and the support list from recorded release tests.

The app runs at ordinary user integrity and its executable manifest requests `asInvoker` with `uiAccess=false`. The package does not declare `allowElevation`, `broadFileSystemAccess`, camera, microphone, location, internet capabilities, a driver or a service. The absence of an internet capability does not technically prevent an ordinary desktop process from networking; verify actual behavior before promising offline operation.

`runFullTrust` is a [restricted capability](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations). Partner Center may require an explanation and approval. Do not describe it as administrative elevation. The startup extension is declared with `Enabled=false`, and the UI must obtain an explicit choice before calling `StartupTask.RequestEnableAsync`. Windows startup disablement must be respected. [Microsoft documents this packaged desktop extension](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/desktop-to-uwp-extensions#start-an-executable-file-when-users-log-into-windows).

Suggested certification capability explanation, after verifying it matches the release:

> KeyBridge is a local desktop shortcut conversion tool. runFullTrust is required for the ordinary-user desktop process to use documented Windows keyboard interfaces and provide tray controls. Conversion starts only after the user's explicit choice. The app matches configured shortcut combinations in memory and sends their replacements. It does not request administrator privileges, install drivers or services, or save or upload input content. Pause, emergency pause and Exit remain available. Conversion is restricted to tested ordinary-privilege applications and verified input contexts.

## Build an unsigned package

Prerequisites: Windows 11 x64, the supported .NET 10 SDK, and the Windows SDK's x64 MakeAppx and MakePri tools. The publish output is self-contained, so the installed app does not require a separately installed .NET runtime. Packaging retains the WPF assemblies and native dependencies; trimming and single-file publication are disabled.

From the repository root:

```powershell
./scripts/Build.ps1
./scripts/Package.ps1 `
  -IdentityName $partnerCenterIdentityName `
  -Publisher $partnerCenterPublisher `
  -PublisherDisplayName $partnerCenterPublisherDisplayName `
  -Version 0.1.0.0
```

Use the exact identity name, publisher distinguished name and publisher display name from Partner Center's Product identity page. The script requires these inputs and never creates a fake Store publisher. It accepts XML-sensitive characters safely and requires a zero final version component for Store packages. Keep identity values stable between upgrades. Increase the package version on every submission.

For a local development package, choose your own development identity and the exact subject of your development signing certificate. Such a package is not a Store submission. Do not mix that identity with the eventual Store product identity.

`-DotNetPath`, `-MakeAppxPath` and `-MakePriPath` override discovery when required. The scripts search PATH, the SDK registry roots and versioned Windows SDK `bin` directories. A repository sibling `.tools/dotnet/dotnet.exe`, `%LOCALAPPDATA%/KeyBridgeBuild/dotnet/dotnet.exe`, and sibling `.tools/windows-sdk-buildtools` are also supported. The latter can hold the official [Microsoft Windows SDK BuildTools NuGet](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools); use a supported version and verify Microsoft's tool signatures. Build restores dependencies when necessary; the installed app's runtime remains local. Do not use `-SkipTests` for a release candidate.

Output: `artifacts/packages/<identity>_<version>_x64.msix` plus a SHA-256 sidecar and MakeAppx/MakePri logs under `artifacts/logs`. Staging is generated in a unique subdirectory under `artifacts/staging` to avoid stale files. The script uses MakeAppx schema validation and does not suppress validation. MakePri creates a single `resources.pri` so Windows can resolve image scale/target-size variants in the complete package. See [Microsoft's MakePri documentation](https://learn.microsoft.com/en-us/windows/uwp/app-resources/makepri-exe-command-options). Original PNG artwork includes scale 100/125/150/200/400 and target-size variants. Regenerate it with `scripts/New-Assets.ps1`.

## Signing and local installation

An unsigned file is useful for packaging review; it cannot be installed as a normal trusted package by double-clicking. The Store signs accepted submitted packages, so no private production signing key is committed. For sideload testing, use `scripts/Sign-Package.ps1` with a development certificate already present in your Current User Personal certificate store. Its subject must exactly match the package Publisher. The script does not create certificates, import trust roots or ask for elevation.

```powershell
./scripts/Sign-Package.ps1 -PackagePath $testPackage -CertificateThumbprint $developmentCertificateThumbprint
Add-AppxPackage -Path $testPackage
```

Trust setup for a development certificate is a separate deliberate local action by the tester. Never distribute its private key. Do not submit the development certificate or a package whose identity differs from Partner Center. See [Microsoft's signing instructions](https://learn.microsoft.com/en-us/windows/msix/package/sign-app-package-using-signtool), [MakeAppx instructions](https://learn.microsoft.com/en-us/windows/msix/package/create-app-package-with-makeappx-tool) and [Store package requirements](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements).

After signing, the script refreshes the SHA-256 sidecar. Record which hash was installed and tested. Validate the actual final payload with the Windows App Certification Kit and review any desktop-specific exemptions against current official requirements. Record failures rather than assuming every generic kit check applies identically to WPF.

## Mandatory release blockers

- Reserve and check the final product name; supply real Partner Center identity, account verification and access.
- Add the publisher's legal identity, working support contact and public HTTPS privacy URL. Replace development policy/contact text in the app and published policy.
- Complete `VERIFICATION.md` on physical Windows and real applications, with exact versions, layouts and input-method settings. Resolve every input-loss, unintended IME switching, stuck-key, safety-shortcut or privacy failure.
- Record the verified rules for each application and input context. Ship conditional navigation/IME actions only if proven; enable supported actions within their documented scope.
- Verify packaged startup (including Windows disablement), update/persistence, sleep/lock, clean uninstall, and crash recovery.
- Measure hook and emission latency, idle CPU and working set with hardware and methodology. Provide evidence of hook failure handling.
- Validate the exact MSIX using MakeAppx and the certification kit; sign and install a local test package, then record its hash and install/update/uninstall results.
- Audit third-party distribution notices against the actual publish output and runtime versions.
- Provide real product screenshots, accurate description, age rating, regions, price and availability. Do not promise ARM64 or Windows 10 based on compilation.
- Recheck applicable policies immediately before submission, and address restricted-capability review honestly.

The current product identity and submission are recorded in `STORE_SUBMISSION.md`.

## Review instructions for certification

1. Install and launch the exact candidate package. Observe that global conversion is off.
2. Read local privacy and shortcut-behavior disclosures; select a Command role and confirm left/right key identity in Try it.
3. Choose only application/input contexts listed in the accompanying verified support record. Inspect source/target conflicts and reserved keys.
4. Test the local editor and the listed real external applications; explicitly enable conversion. The local editor is not evidence of other-app compatibility.
5. Confirm native Ctrl actions, unmatched keys and unknown IME contexts pass through; higher-integrity windows remain unsupported.
6. Pause from the window and tray and use the configured emergency shortcut (Ctrl+Alt+F10 by default). Verify Resume requires the user and Exit stops conversion.
7. If desired, enable login startup, disable it in Windows, and verify the app respects the Windows-disabled state and a saved pause.
8. Export/import declarative settings. Confirm invalid/oversized/future schemas preserve the active settings.
9. Exit, uninstall through Windows Settings and confirm normal keyboard behavior with no permanent mapping.
