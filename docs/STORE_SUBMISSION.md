# KeyBridge Store submission

## Product and package

- Product: KeyBridge
- Store ID: `9NK4XXK1JH04`
- Submission ID: `1152921505702095097`
- Package identity: `14E33A41.KeyBridge`
- Publisher: `CN=BD412261-9548-4F5A-95C1-24B072A9C1CC`
- Official publisher display name: `张之麒`
- Public publisher: Zhiqi Zhang
- Support: zhiqizhangg@gmail.com
- Price: Free
- Version: `0.1.4.0`
- Platform: Windows 11 x64, English (United States)
- Package SHA-256: `1F485535D17033FAAF7FEA00598429947EA1E0FCA2BB1FED8647A155A0DB4A96`
- Privacy: https://github.com/shoutoutuoadi325/keybridge/blob/main/docs/PRIVACY.md

## Store description

Bring familiar Command-style shortcuts to your Windows desktop with KeyBridge. Choose a physical Command key and personalize shortcuts for each application, with presets for Microsoft Edge, Google Chrome, Visual Studio Code and the built-in editor.

Create shortcuts for actions such as copy, paste, cut, select all, undo, redo, save and find. Preview a shortcut in the local editor or its selected application, check conflicts and confirm your settings before enabling it. Application exclusions give you control over where conversion runs.

Stay in control with Pause in the main window or notification area, a configurable emergency pause shortcut and Exit. Optional login startup respects Windows settings and starts paused. Import and export your configuration using files you choose.

KeyBridge works on Windows 11 x64 with an English interface. It converts application shortcuts to Ctrl plus a letter, optionally with Shift, using the ENG US keyboard layout in ordinary-permission applications. Windows security shortcuts, Microsoft Pinyin and other input contexts retain native behavior.

Your keyboard processing stays on your device. KeyBridge does not record keyboard history, read clipboard contents or upload input content. It runs without administrator access, drivers or services. No account is required, and there are no ads or automatic telemetry.

Short description: Familiar Command-style shortcuts for Windows, with local processing and convenient pause controls.

Features:

- Choose a physical Command key for familiar shortcuts.
- Personalize application shortcuts with presets and exclusions.
- Preview shortcuts in the built-in editor or selected application.
- Check shortcut conflicts and confirm application settings.
- Pause from the window, notification area or emergency shortcut.
- Import and export configuration; optional login startup.

## Reviewer instructions

KeyBridge 0.1.4.0 is an English desktop shortcut utility for Windows 11 x64. No account or credentials are required. It maps configured Command-style combinations to Ctrl plus a letter, optionally with Shift, in selected ordinary-permission applications using ENG US. Other input sources and Windows security contexts retain native input behavior.

1. Launch KeyBridge. On Home, accept local shortcut processing, choose a Command role and use Confirm role with a physical key.
2. Press the configured emergency pause shortcut to complete setup. The default is Ctrl+Alt+F12. If Windows has assigned it elsewhere, choose another combination in Settings, save it and press it. Ctrl+Alt+F10 is an alternative.
3. Select ENG US. Open Try it and begin the isolated local trial. Preview select all, copy, paste, undo and redo using the selected Command role. End the preview with emergency pause.
4. For an application shortcut, select its executable in Shortcuts, choose the source and Ctrl-letter target, preview it in that application, then confirm and save its settings. Enable the shortcut and resume mapping.
5. Pause is available from the main window, notification area and emergency shortcut. Exit stops the app. Optional login startup uses Windows StartupTask, respects Windows settings and launches paused. Settings supports local import/export and optional diagnostics.

Capability rationale: runFullTrust supports the ordinary-user WPF desktop process. The app uses documented WH_KEYBOARD_LL/SetWindowsHookEx, SendInput for configured combinations, RegisterHotKey for emergency pause and opt-in StartupTask. It runs asInvoker with uiAccess=false and does not request administrator access or install a driver or service. Keyboard matching stays in memory; there is no keyboard-history recording, clipboard/document/window-title capture, input upload or telemetry.

Source: https://github.com/shoutoutuoadi325/keybridge

## Submission record

The owner authorized Developer Agreement 8.11 and the IARC terms on October 11, 2026. Pricing, properties and age ratings are saved. The original 0.1.3 submission was returned to draft to replace its package, screenshots and release copy with version 0.1.4. Publication uses the existing manual control.
