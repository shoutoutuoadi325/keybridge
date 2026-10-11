# KeyBridge privacy policy

Policy version: 1.0. Last updated: October 11, 2026.

This policy describes the local KeyBridge desktop application published by Zhiqi Zhang. For privacy questions or product support, contact zhiqizhangg@gmail.com. This policy is supplied with the application and published at https://github.com/shoutoutuoadi325/keybridge/blob/main/docs/PRIVACY.md.

## Keyboard events processed on your device

After you explicitly enable conversion, KeyBridge processes keyboard events and modifier state in memory to match configured shortcuts and send their replacements. This includes virtual key identifiers and event flags required to distinguish physical and injected input. KeyBridge does not assemble ordinary key events into text, record keyboard history, or save or upload input content. The shortcut engine does not read clipboard contents, document text, window titles, password field text, IME candidate text, or composition strings.

The foreground process name, window thread, integrity level and keyboard layout are used in memory to decide whether conversion is permitted. A layout identifier does not prove an IME's language mode or composition state. Unavailable information is treated as unknown. KeyBridge does not scan and upload a list of installed applications or keep a timeline of foreground activity.

The Try it page displays the key you are testing only while its test control has focus. Text you type into its local editing field remains in memory for that application session and is not saved as a document or included in diagnostic files.

## Local information saved

KeyBridge saves your chosen keyboard role, application rules, exclusions, language preference, consent/onboarding completion, pause preference, recovery state and other application settings in your current user's application data area. Application rule entries may contain process names that you chose. Microsoft Store installations use Windows per-user application-data behavior. Directly run copies store settings in the user's local application data directory. Settings are not encrypted by KeyBridge; protect your Windows account and exported files accordingly.

Recovery markers indicate whether the last session ended normally. Configuration backups, when created by a migration or save operation, stay local. Diagnostics are limited to module status, application version and error information; input strings, key histories and window titles must not be included. The application has no automatic diagnostic upload.

Import and export use files that you select. Exported configurations can reveal the application names and shortcuts you configured. Review a file before sharing it. Exporting does not send the file to the publisher.

## Network and third parties

KeyBridge's shortcut functionality makes no network requests. The application includes no account service, advertisements, analytics SDK, cloud processing or automatic telemetry. It does not sell or disclose your local settings or input data to third parties.

Microsoft Store installation, licensing and updates are platform services governed by Microsoft's policies and may use a network connection. If you independently send a support message or share an exported file, the service you use and the recipient receive the information you send. The application does not send that information automatically. Opening the public policy or support website uses your browser and that website's behavior.

## Your controls

You choose whether conversion runs. Use Pause in the main window or tray, the configured emergency shortcut (Ctrl+Alt+F10 by default), or Exit from the tray to stop conversion. KeyBridge releases its synthesized input and restores held modifiers. If Windows cannot finish recovery, mapping stays paused and the status identifies the keys to press and release. Exit removes the hook; unfinished recovery requires a new recovery rehearsal on the next launch. A process crash can prevent cleanup; press and release any affected modifier before resuming.

Login startup is off initially and is enabled only by your choice. A startup task disabled in Windows is respected. Your explicit pause preference is retained, and unexpected termination causes a safe paused restart.

You can inspect, edit, export or reset your configuration in Settings. Uninstall through Windows Settings > Apps > Installed apps. Windows manages removal of packaged application data. Files you exported elsewhere are retained until you delete them. Settings for directly run copies can be removed manually from the application's data directory after the process exits.

## Policy changes and contact

Zhiqi Zhang will update this policy when data practices change. The application version and this policy version should be recorded with each public release. Contact zhiqizhangg@gmail.com for privacy questions, requests about data practices, or product support. KeyBridge does not automatically send support messages. Do not include passwords, private input, or sensitive configuration data in a support message.
