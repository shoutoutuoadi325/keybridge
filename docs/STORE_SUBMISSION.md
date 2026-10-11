# KeyBridge Store submission 1

Prepared October 11, 2026. Submission 1 was submitted for certification on this date. Partner Center reported In certification, with Pre-processing in progress. This does not claim certification approval or public publication.

## Product and package identity

- Store ID: `9NK4XXK1JH04`
- Product name: KeyBridge
- Package identity: `14E33A41.KeyBridge`
- Publisher: `CN=BD412261-9548-4F5A-95C1-24B072A9C1CC`
- Partner Center publisher display name: `张之麒` (preserved exactly as supplied by the account)
- Public publisher/contact name: Zhiqi Zhang
- Public support and privacy contact: zhiqizhangg@gmail.com
- Privacy policy: https://github.com/shoutoutuoadi325/keybridge/blob/main/docs/PRIVACY.md
- Owner's pricing choice: Free
- Submission ID: `1152921505702095097`
- Submitted package version: `0.1.3.0`, Windows 11 x64, English (United States)
- Candidate SHA-256: `29CB77FBAD8FE71FD6AB8AA088ABFF65026F422E37B819EE2F9DB9FFD2D73FD0`

## English listing copy

Short description:

Configure and try local keyboard shortcut conversions with explicit controls.

Description:

KeyBridge helps you configure familiar keyboard shortcuts on Windows. Choose a physical Command key, define replacement shortcuts for individual applications, and try a rule before enabling it. Rules start disabled; conversion starts paused and requires your explicit choice.

The Try it page provides a local editor for checking your keyboard role and trying basic shortcut conversions. Application rules include local verification notes so you can record the results for your own application and Windows versions. Inspect conflicts, exclude applications, and import or export your settings.

Pause conversion from the app or system tray, use the configured emergency pause shortcut, or exit the app. Login startup is optional and starts disabled.

Current scope: Windows 11 x64, English interface, standard ENG US keyboard layout and ordinary-permission applications. Replacement targets are Ctrl plus a letter, optionally with Shift. External application presets require your own local verification. Microsoft Pinyin and unknown input contexts keep native input behavior. Elevated windows, secure desktops, hardware Fn conversion, per-device mapping, conditional navigation and input-source switching are unsupported. This version does not provide universal Mac shortcut compatibility.

Shortcut processing stays on your device. KeyBridge does not save keyboard history or upload input content. No account, advertising, analytics or cloud AI feature is included. The privacy policy describes local settings and information processed in memory.

Features:

- Choose and confirm a physical Command key.
- Configure application-specific shortcut replacements and exclusions.
- Try a shortcut in the local editor or a temporary application rule.
- Inspect conflicts and record your own compatibility checks.
- Pause from the window, tray or emergency shortcut.
- Import and export local settings; optional login startup.

Suggested category: Utilities + tools. Minimum hardware: Keyboard. Do not declare accessibility testing, pen/ink support, immersive display or generative AI features.

## Capability review explanation

KeyBridge uses runFullTrust for its ordinary-user WPF desktop process and documented Windows keyboard interfaces. It never requests administrator privileges, uiAccess, a driver or a service. With explicit consent, it matches configured shortcut combinations in memory and sends replacements. Window and tray pause, an emergency pause shortcut and Exit are available. Unknown input contexts and higher-integrity windows pass through. No keyboard history, input-content upload or automatic telemetry is implemented. Optional startup requires the user's choice and respects Windows disablement.

## Certification notes saved

The app launches paused. Read the disclosures, choose a Command key, confirm its physical identity on Try it and complete the emergency-pause test. Use ENG US. Try the local editor first. For external applications, use a temporary single-rule trial and record the actual local result before enabling a saved rule. Unknown and Microsoft Pinyin contexts pass through.

No credentials are required. Do not claim completed real-keyboard, packaged lifecycle, performance or Windows App Certification Kit checks: these remain outstanding in `VERIFICATION.md`. The 90 automated checks and MakeAppx validation are narrower evidence.

## Current gates

- Product name reserved and Submission 1 created in Partner Center.
- Public policy and support contact supplied by the owner and published.
- Version 0.1.3.0 replaced 0.1.1.0, was reported Validated, and Packages Complete. The runFullTrust approval request was supplied in Submission Options.
- Public policy URL, support URL, category and keyboard/processor requirements saved; Properties Complete.
- Free pricing/availability, properties, IARC ratings, packages, English listing and submission options all reported Complete. Four real 0.1.3.0 UI screenshots and reviewer testing notes were saved.
- Microsoft Application Developer Agreement 8.11 accepted on October 11, 2026 after the user's explicit authorization; acceptance verified in Partner Center.
- Complete the remaining real-device and exact-package release checks before representing the app as release validated.

- IARC terms and adult declaration accepted after explicit owner authorization; age ratings saved (3+ / Everyone as applicable).
- Submitted for certification successfully; Submission Complete and Pre-processing In progress. Certification and Publishing had not started at the receipt observation.
- Manual publication hold verified: Partner Center says the product will start publishing only when Publish now is clicked. No public release has been performed.
- Corresponding source published at commit `2084d0a976522206312178008f0d769933623f13`; record updates are subsequent commits. Screenshot receipt: ignored local `artifacts/store-submission/submitted-for-certification.jpg`.
