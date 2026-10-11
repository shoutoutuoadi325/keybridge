# Public Windows API and permissions review

Reviewed October 11, 2026. This document describes the intended allowed implementation boundary; review the final binary imports and package before release. Ordinary desktop APIs are not categorically banned by Microsoft Store merely because a package declares `runFullTrust`. Usage, consent, data handling and restricted-capability approval still matter.

| Interface | Purpose | Required boundary |
| --- | --- | --- |
| `SetWindowsHookExW(WH_KEYBOARD_LL)`, `CallNextHookEx`, `UnhookWindowsHookEx`, message loop | Temporary process keyboard observation and filtering | Dedicated message thread; quick callback; no blocking UI, disk, network or cross-process COM. No injected DLL, driver or service. Unmatched and unsupported contexts pass through. |
| `SendInput`, `KBDLLHOOKSTRUCT` flags and `dwExtraInfo` | Emit a replacement combo and prevent recursive conversion | Tag own events; pass through external injected events. Never elevate or bypass UIPI. Check full/partial results, never retry a partial shortcut blindly. Release only synthesized state owned by KeyBridge. |
| `GetForegroundWindow`, `GetWindowThreadProcessId`, `System.Diagnostics.Process` and process/module file-version metadata | Determine foreground rule scope and expire version-specific local evidence | Obtain process name and accessible executable-version metadata without reading window titles, documents or installed-program lists. Lookup failures deny conversion. File-version/process lookup runs outside the hook callback. |
| `OpenProcessToken`, `GetTokenInformation(TokenIntegrityLevel)` | Check ordinary-user integrity compatibility | Fail closed if access or integrity cannot be confirmed. No `AdjustTokenPrivileges` or elevation. Secure desktop and administrator windows unsupported. |
| `GetKeyboardLayout(foregroundThread)` | Identify the foreground thread's layout | Never substitute the app's own thread layout. A layout alone does not establish Pinyin mode or composition. Ambiguous contexts remain unknown. |
| `RegisterHotKey`, `UnregisterHotKey` | Emergency pause | Check registration result; expose a user-selected alternative if unavailable. Keep tray/window recovery paths. No conflicting ordinary rules. |
| `GetAsyncKeyState` where needed | Reconcile physical modifiers during recovery | No text reconstruction, persistent key sequences or synthetic probes. Do not release every user-held modifier. |
| Packaged WinRT `StartupTask` | Query and control opt-in login startup | Handle unavailable unpackaged startup safely. No registry Run-key fallback. No enable request until explicit user choice. Respect `DisabledByUser`/policy states. |
| WPF/Windows Forms tray and standard user-selected file dialogs | Settings, pause/exit and local configuration I/O | Single user process, declarative validated JSON, bounded import. No broad-file-system capability or arbitrary command execution. |
| `Windows.UI.ViewManagement.UISettings.TextScaleFactor`, WPF system fonts/colors and standard DPI support | Respect Windows text scaling and display preferences | Text scaling is read at launch, with a system-font fallback. The executable declares PerMonitorV2 DPI awareness; do not claim untested live scale changes or accessibility compatibility. |
| `Microsoft.Win32.SystemEvents.SessionSwitch`, `PowerModeChanged` | Pause for session/power lifecycle transitions | Ordinary supported desktop events, no persistent service or secure-desktop input. Resume remains a user choice. |

IMM32/TSF composition APIs are research options. They must not be used to read composition text or infer cross-process certainty without validation. If external Pinyin state is unavailable through supported safe interfaces, report unknown and restrict rules. Do not use accessibility APIs to circumvent input security boundaries.

Official primary references:

- [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc): Windows can remove a slow hook without notification; responsiveness does not by itself prove the hook is alive. Record the implemented failure-observation limits.
- [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput): check the emitted-event count and existing keyboard state; UIPI limits apply.
- [KBDLLHOOKSTRUCT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct): injected flags and application marker are event metadata, not a security identity.
- [GetKeyboardLayout](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getkeyboardlayout): thread-specific layout information.
- [ImmGetOpenStatus](https://learn.microsoft.com/en-us/windows/win32/api/imm/nf-imm-immgetopenstatus): open status is not a universal Chinese/English mode classifier.
- [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey): registrations can fail and reserved combinations exist.
- [StartupTask](https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.startuptask): packaged startup remains under Windows user control.
- [UISettings.TextScaleFactor](https://learn.microsoft.com/en-us/uwp/api/windows.ui.viewmanagement.uisettings.textscalefactor): the public Windows text-size setting read at launch.
- [Process.MainModule](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.mainmodule): accessible module metadata used for rule-version evidence, with access failures handled conservatively.
- [SystemEvents.SessionSwitch](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.sessionswitch): documented desktop session-event notifications.
- [Capability declarations](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations): medium-integrity desktop apps require appropriate full-trust capability but are not AppContainer applications.
- [Store policy 7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies): user consent and supported methods are required for modifications to the Windows experience.

The final verification record must identify APIs actually called, remove unused claims, record failed boundary tests and list any dependency with its version and license. Public documentation is evidence of supported API semantics, not an assurance of certification.
