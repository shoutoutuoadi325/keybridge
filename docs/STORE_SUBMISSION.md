# KeyBridge Store submission

## Product and package

- Product: KeyBridge
- Store ID: `9NK4XXK1JH04`
- Submission ID: `1152921505702095097`
- Package identity: `14E33A41.KeyBridge`
- Publisher: `CN=BD412261-9548-4F5A-95C1-24B072A9C1CC`
- Official publisher display name: `Zhiqi Zhang` (Partner Center package validation, October 11, 2026)
- Public publisher: Zhiqi Zhang
- Support: zhiqizhangg@gmail.com
- Price: Free
- Version: `0.1.5.0`
- Platform: Windows 11 x64, English (United States)
- Package: `14E33A41.KeyBridge_0.1.5.0_x64.msix` (82,063,438 bytes)
- Package SHA-256: `E6791091C317AB9F1C9529856A453245BB3A9619BB7D42B2F32D9C92E915A4BB`
- Privacy: https://github.com/shoutoutuoadi325/keybridge/blob/main/docs/PRIVACY.md

## Publication status

KeyBridge is now published on the Microsoft Store: https://apps.microsoft.com/detail/9nk4xxk1jh04?ocid=webpdpshare.

The submission record below preserves the October 11, 2026 in-certification receipts and package evidence for version 0.1.5.0.

## Store description

Bring familiar Command-style shortcuts to your Windows desktop with KeyBridge. Choose a physical Command key and personalize shortcuts for each application, with presets for Microsoft Edge, Google Chrome, Visual Studio Code and the built-in editor.

Create shortcuts for actions such as copy, paste, cut, select all, undo, redo, save and find. Preview a shortcut in the local editor or its selected application, check conflicts and confirm your settings before enabling it. Application exclusions give you control over where conversion runs.

Stay in control with Pause in the main window or notification area, a configurable emergency pause shortcut and Exit. Optional login startup respects Windows settings and starts paused. Import and export your configuration using files you choose.

KeyBridge works on Windows 11 x64 with an English interface. It converts application shortcuts to Ctrl plus a letter, optionally with Shift, using the ENG US keyboard layout in ordinary-permission applications. Windows security shortcuts, Microsoft Pinyin and other input contexts retain native behavior.

Your keyboard processing stays on your device. KeyBridge does not record keyboard history, read clipboard contents or upload input content. It runs without administrator access, drivers or services. No account is required, and there are no ads or automatic telemetry.

Short description: Familiar Command-style shortcuts for Windows, with local processing and convenient pause controls.

What's new in this version:

Improved keyboard recovery and configuration saving. Emergency pause now defaults to Ctrl+Alt+F10; existing default settings migrate with confirmation before resuming. Shortcut editing preserves composition settings and accepts common key aliases. Pending previews are canceled when their page or setup dialog closes.

Features:

- Choose a physical Command key for familiar shortcuts.
- Personalize application shortcuts with presets and exclusions.
- Preview shortcuts in the built-in editor or selected application.
- Check shortcut conflicts and confirm application settings.
- Pause from the window, notification area or emergency shortcut.
- Import and export configuration; optional login startup.

## Reviewer instructions

KeyBridge 0.1.5.0 is an English desktop shortcut utility for Windows 11 x64. No account or credentials are required. It maps configured Command-style combinations to Ctrl plus a letter, optionally with Shift, in selected ordinary-permission applications using ENG US. Other input sources and Windows security contexts retain native input behavior.

1. Launch KeyBridge. On Home, accept local shortcut processing, choose a Command role and use Confirm role with a physical key.
2. Press the configured emergency pause shortcut to complete setup. The default is Ctrl+Alt+F10. If Windows has assigned it elsewhere, choose another available combination in Settings, save it and press it. F12 is unavailable for emergency pause. Existing exact Ctrl+Alt+F12 settings migrate to a compatible shortcut and require confirmation before resuming.
3. Select ENG US. Open Try it and begin the isolated local trial. Preview select all, copy, paste, undo and redo using the selected Command role. End the preview with emergency pause.
4. For an application shortcut, select its executable in Shortcuts, choose the source and Ctrl-letter target, preview it in that application, then confirm and save its settings. Enable the shortcut and resume mapping.
5. Pause is available from the main window, notification area and emergency shortcut. Exit stops the app. Optional login startup uses Windows StartupTask, respects Windows settings and launches paused. Settings supports local import/export and optional diagnostics.

If recovery guidance appears after pausing, physically press and release the indicated keys before resuming. Resume stays unavailable while recovery is unfinished. After an interrupted or incomplete exit, the next launch starts paused and requires recovery confirmation.

This update improves keyboard recovery, saves overlapping configuration changes in order, validates the active emergency shortcut registration, preserves shortcut composition settings, accepts common shortcut aliases and cancels pending preview starts when their page or setup dialog closes.

Capability rationale: runFullTrust supports the ordinary-user WPF desktop process. The app uses documented WH_KEYBOARD_LL/SetWindowsHookEx, SendInput for configured combinations, RegisterHotKey for emergency pause and opt-in StartupTask. It runs asInvoker with uiAccess=false and does not request administrator access or install a driver or service. Keyboard matching stays in memory; there is no keyboard-history recording, clipboard/document/window-title capture, input upload or telemetry.

Source: https://github.com/shoutoutuoadi325/keybridge

## Restricted capability field

Local WPF app; asInvoker, uiAccess=false; no elevation, driver or service. Uses WH_KEYBOARD_LL/SetWindowsHookEx and SendInput for Ctrl+letter shortcuts, RegisterHotKey for emergency pause, and opt-in StartupTask. Starts paused with consent and setup. Other input sources and elevated windows retain native input. Pause/Exit attempt cleanup; unfinished recovery blocks Resume with physical key-release guidance. No key history, input upload or telemetry. https://github.com/shoutoutuoadi325/keybridge

## Submission record

The October 11, 2026 update to 0.1.5.0 uses source commit `c5902a2794d9396bdb4e20fc3b51732aa08ee531`. Partner Center initially displayed the existing submission as In draft, with 0.1.4.0 Validated and all six sections Complete. This is the observed starting state, even though the earlier receipt below recorded In certification.

Package validation now requires the official publisher display name `Zhiqi Zhang`. The first 0.1.5.0 upload used the historical display name `张之麒` and was rejected for that mismatch. Its SHA-256 was `419929077733D29E1125BF3CF195FE6EB25B3735A089A4A39B0F511E8DB51047`; it is not the accepted candidate. The replacement retains the exact package identity and Publisher distinguished name, and uses the display name reported by Partner Center.

The replacement MSIX passed local MakeAppx validation, all 188 deterministic checks and seven-page rendering from its extracted payload. Partner Center marked it Validated. Saving Packages replaced 0.1.4.0 with only 0.1.5.0 for the same Windows Desktop customers; all six submission sections displayed Complete before submission.

The English listing now includes the update notes above and four new 2740 x 1761 screenshots of Home, Shortcuts, Try it and Settings rendered from the final package. Reviewer instructions and the 499-character restricted-capability explanation were saved. The current publishing option was observed as Publish this submission as soon as it passes certification; that setting was retained. No agreement or age-rating terms were accepted in this update.

The final resubmission receipt at 21:35 China Standard Time on October 11, 2026 reports In certification: Submission Complete, Pre-processing In progress, Certification Not started and Publishing Not started. Partner Center confirms that publishing starts as soon as certification passes. An initial submission was canceled to clarify that closing a page or setup dialog cancels pending preview starts; the corrected listing and reviewer text were saved, reopened to verify persistence and resubmitted. This line records that submission-time state; KeyBridge is now published at https://apps.microsoft.com/detail/9nk4xxk1jh04?ocid=webpdpshare.

Local receipts are under `artifacts/store-submission`: `product-identity-0.1.5.png`, `reviewer-notes-0.1.5.png`, `listing-0.1.5.png`, `package-validated-0.1.5.png` and the final `submitted-0.1.5.png`. Final package verification is under `artifacts/store-0.1.5-verification/final`. No new physical keyboard session or installed-package/WACK run was performed.

## Historical 0.1.4.0 submission

Version 0.1.4.0 was resubmitted on October 11, 2026 after replacing the package, all four screenshots, English listing, reviewer instructions and capability rationale. Before submission, Partner Center marked the package Validated and all six sections Complete. The submission receipt reports In certification, Submission Complete and Pre-processing In progress. Publication retains the existing manual Publish now control.

Developer Agreement 8.11 and IARC terms were accepted with explicit owner authorization. Pricing remains Free. Corresponding application source: commit `8d5bb7c`; subsequent commits update copy conventions, packaging output and this record. Local receipts are under `artifacts/store-submission`, including `release-submitted-0.1.4.jpg`, `release-listing-0.1.4.jpg` and `release-reviewer-notes-0.1.4.jpg`.
