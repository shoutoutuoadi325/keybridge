# Windows keyboard session: KeyBridge 0.1.3.0

Recorded October 11, 2026 on the owner's computer. This session used the staged executable and the owner's physical keyboard with the ENG US layout in the KeyBridge editor.

## Environment

- Windows 11 Pro build 26300, x64.
- Intel Core i5-1135G7; 8,424,185,856 bytes of physical memory.
- Implementation commit: `2084d0a976522206312178008f0d769933623f13`.
- Submitted package SHA-256: `29CB77FBAD8FE71FD6AB8AA088ABFF65026F422E37B819EE2F9DB9FFD2D73FD0`.

## Observed results

| Action | Result |
| --- | --- |
| Initial launch | Mapping started paused. |
| Emergency shortcut | Windows reported Ctrl+Alt+F12 was assigned elsewhere; mapping stayed paused. Choosing Ctrl+Alt+F10 registered successfully. |
| Physical Command role | The owner pressed Left Alt; the app displayed Physical role confirmed and saved the confirmation. |
| Emergency pause | The owner pressed Ctrl+Alt+F10; the app displayed Emergency pause tested successfully and saved the confirmation. |
| Input source | Selecting ENG US produced EnglishKeyboard / NotApplicable / NotComposing. |
| Select all | The owner reported Left Alt+A selected the sample. |
| Copy and paste | The owner reported Left Alt+C copied the sample, Right/Enter selected an insertion point and Left Alt+V pasted it. |
| Undo and redo | The owner reported Left Alt+Z removed the pasted sample and Left Alt+Shift+Z restored it. |
| End session | The owner pressed Ctrl+Alt+F10. The app was observed paused afterward. |

The editor contained synthetic sample text. These observations describe this keyboard, layout and local editing session.

## Certificate operations

With the owner's explicit authorization, a separate MSIX copy was signed using a short-lived, non-exportable test key. Its SHA-256 was `39FFD0BC4783BCA77257044AF5432EA20BD8C3DB80741F60775F9629C7E1109E`. The exact public certificate was temporarily trusted in LocalMachine TrustedPeople. SignTool verify /pa returned zero errors and warnings. Archive comparison showed the original application and manifest payload unchanged; signing updated `[Content_Types].xml` and added two signature entries.

The session ended before package installation, at the owner's request. The exact certificate trust and newly created private signing key were removed at 14:00:55 +08:00 on October 11, 2026. No test certificate trust remains. The original Store package was unchanged by these operations.

## Recorded measurement

A 20.03-second sample with the window open recorded CPU usage of 0.429% normalized across logical processors, working set 215.43 MB and private bytes 138.50 MB. This describes that window-open sample. Local measurement artifacts are under `artifacts/verification/0.1.3.0`.

The current package and submission are recorded in `STORE_SUBMISSION.md`.
