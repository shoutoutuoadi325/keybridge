# KeyBridge configuration schema 1

`ProfileStore` is the executable validator for schema version 1. It rejects unknown properties, duplicate JSON properties, missing required fields, unsupported enum values, illegal virtual keys, unsupported schema versions, invalid scopes, ambiguous enabled rules, and protected shortcut conflicts. Configuration is declarative data. It cannot run programs, scripts, macros, or network actions.

The product design document contains an illustrative configuration. Actual schema 1 uses chord objects rather than arrays of strings. A chord contains required `modifiers` and `key` properties. Enum values are strings. Flags are represented as comma-separated enum names.

```json
{
  "schemaVersion": 1,
  "profileName": "Windows keyboard",
  "commandKey": "LeftAlt",
  "enabled": false,
  "startAtLogin": false,
  "emergencyPause": { "modifiers": "Control, Alt", "key": "F10" },
  "consentGranted": false,
  "roleKeyConfirmed": false,
  "recoveryTested": false,
  "previousSessionClean": true,
  "imePolicy": {
    "preserveNativeSwitching": true,
    "switchAction": "None",
    "additionalProtectedShortcuts": []
  },
  "rules": [
    {
      "id": "editor-copy",
      "intent": "Copy",
      "source": { "modifiers": "Command", "key": "C" },
      "target": { "modifiers": "LeftControl", "key": "C" },
      "scope": { "processName": "editor.exe" },
      "contextPolicy": "VerifiedImeCompatibility",
      "origin": "UserApplication",
      "enabled": false,
      "verification": {
        "applicationVersion": "",
        "windowsBuild": "",
        "notes": "",
        "verifiedContexts": []
      }
    }
  ],
  "excludedApps": []
}
```

The example starts disabled. Replace `editor.exe` with your application and confirm the shortcut settings before enabling.

## Required root fields

`schemaVersion`, `profileName`, `commandKey`, `enabled`, `startAtLogin`, `emergencyPause`, `imePolicy`, `rules`, and `excludedApps` must be present. Setup flags default to false if omitted; `previousSessionClean` defaults to true. Enabling mapping requires consent, confirmation of the physical role key, and a successful recovery rehearsal. The Windows registration result remains a separate runtime requirement.

## Chords and matching

`Command` is a source-only alias resolved to the selected side-specific role key. Supported role keys are `LeftAlt`, `LeftControl`, `RightControl`, `LeftWindows`, and `RightWindows`. Right Alt is excluded because it may represent AltGr.

Supported modifier names are `None`, `LeftShift`, `RightShift`, `LeftControl`, `RightControl`, `LeftAlt`, `RightAlt`, `LeftWindows`, `RightWindows`, `Command`, `Shift`, `Control`, `Alt`, and `Windows`. Generic names match either physical side. Generic and side-specific names from the same family cannot be mixed. Generic Alt and Right Alt cannot be used in ordinary mappings; choose explicit Left Alt. Recovery uses generic modifiers because `RegisterHotKey` does not distinguish their physical sides.

Main keys are letters `A` through `Z`, digits `D0` through `D9`, `F1` through `F12`, and the supported named navigation/editing keys defined by `KeyCode`. Modifiers alone cannot be main keys. Sources require at least one modifier; single-key remapping is unavailable. Targets cannot contain `Command`. Matching requires the exact modifier families: holding an extra modifier preserves native input.

Source and target are checked against Win L, Ctrl Alt Delete, Ctrl Shift Escape, the configured recovery shortcut, known IME shortcuts, and user-provided protected shortcuts. Navigation and deletion require `RequiresNoComposition`; both active and unknown composition block these rules.

The recovery shortcut is also checked against built-in and user-provided protected input-method shortcuts, even when the profile has no rules. F12 is unavailable for recovery because Windows reserves it for debuggers. The default is Ctrl Alt F10; runtime registration still checks whether another application already uses the selected combination.

## Rules, priority, and evidence

Each rule requires `id`, `intent`, `source`, and `target`. IDs must be unique. App scopes contain executable file names such as `msedge.exe`, without paths. Process matching ignores case. Rule priority is `UserApplication`, then `ApplicationPreset`, then `UserGlobal`. Enabled rules with overlapping sources, the same priority, and the same scope are rejected.

Enabled rules require a nonempty application version, Windows build, and explicit input-context trial evidence. Runtime application versions and Windows builds must match exactly. The distinct evidence labels are `EnglishKeyboard`, `PinyinChineseIdle`, `PinyinEnglishIdle`, `PinyinUnknown`, `PinyinComposing`, and `OtherIme`. Evidence for ENG does not imply evidence for Microsoft Pinyin English mode. Evidence describes local user trials; suggested presets carry none.

The current native adapter only permits the standard US ENG layout and the validated target shapes documented in the release limitations. The larger core state model reserves future independently tested contexts. Recording a label cannot bypass adapter restrictions. Enabled shortcuts use application scopes; Windows retains control of input-method switching. `switchAction` must remain `None`, and `preserveNativeSwitching` must remain true.

## Bounds and persistence

Configuration files are limited to 256 KiB, JSON depth 16, 256 rules, 128 exclusions, and 32 additional protected shortcuts. Profile names are limited to 80 characters. Rule IDs and intent labels are limited to 80 characters, executable names to 128, version/build fields to 128, and evidence notes to 512. Printable text is required; control characters are rejected.

Saving validates first, writes and flushes a temporary sibling file, then atomically replaces `profile.json`. One previous-save backup is retained as `profile.json.backup`. Every launch starts paused. An unclean session additionally resets recovery confirmation. Invalid settings are left intact and produce safe defaults for the UI; they must not be automatically saved over the rejected file.

Profile operations wait for initialization and serialize the complete mutation and save. Exit waits for loading and active saves, rejects queued edits, and records a clean session only after acknowledged keyboard recovery. Emergency confirmation belongs to the current live shortcut registration; changing the shortcut invalidates delayed events from the old registration. The editor preserves a saved composition policy and requires `RequiresNoComposition` for sensitive keys. Its shortcut fields use the shared parser, including case-insensitive `Ctrl`, `Win` and `Cmd` aliases, and reject empty or duplicate modifier segments.

An otherwise valid profile using the previous exact default Ctrl Alt F12 is migrated on read to Ctrl Alt F10, or another Ctrl Alt function key from F1 through F11 if the configuration has a conflict. The migration preserves rules and preferences, pauses mapping and clears recovery confirmation. The replacement must be tested before mapping resumes. It never registers a shortcut while parsing or changes the original file on read; the next normal save retains the original in the rotating previous-save backup. Other invalid recovery combinations, unrelated validation errors, or profiles with no conflict-free migration choice remain rejected.

Before a user-requested reset or import replaces rejected settings, `PreserveRejectedAsync` can copy the original bytes into a separate `profile.rejected-<UTC>-<GUID>.json` snapshot. It never changes the original. At most three managed rejected snapshots are retained, with a 1 MiB limit per snapshot. Larger rejected files require manual preservation before replacement. Only files with the exact managed snapshot naming pattern in this store directory are eligible for retention deletion; unrelated files are preserved.

Import validates the incoming file without changing the existing profile. Successful imports remain paused, disable login startup, reset setup confirmations, disable every rule, and clear verified-context labels so local trials must be repeated. Descriptive version/build/notes fields remain available for review. Export may contain user-selected process names and local evidence notes; users should inspect it before sharing.
