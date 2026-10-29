# Progress
Last updated: 2026-10-29
Current step: 06

| Step | Title | Status (TODO/DOING/DONE/REWORK) | Tests | Notes / pending |
|------|-------|-------------------------------|-------|-----------------|
| 01 | Project setup | DONE | EditMode 7/7, PlayMode 1/1, dotnet 7/7 | Android device run pending (see below) |
| 02 | Data model & config | DONE | EditMode 49/49, PlayMode 1/1, dotnet 45/45 | Some heroes unobtainable until ch2-3 content (step 09/10) – validator warnings |
| 03 | Save system | DONE | EditMode 102/102 (all), dotnet 98/98 | Second review pass done; IL2CPP device smoke test pending |
| 04 | Player state & services | DONE | (same run) | Debug console in-Editor manual check pending |
| 05 | Heroes, shards & unlocking | DONE | dotnet 111/111 (Unity run with step 06) | |
| 06 | Equipment system | TODO | | |
| 07 | Stats & formulas | TODO | | |
| 08 | Combat core | TODO | | |
| 09 | Campaign progression | TODO | | |
| 10 | Rewards & drop tables (M1) | TODO | | |
| 11 | UI foundation | TODO | | |
| 12 | UI screens | TODO | | |
| 13 | Battle presentation (M2) | TODO | | |
| 14 | Audio, settings, localization | TODO | | |
| 15 | Balance & content tools | TODO | | |
| 16 | Testing & QA | TODO | | |
| 17 | Mobile optimization & builds | TODO | | |
| 18 | Polish, tutorial, release (M3) | TODO | | |

## Environment
- Unity project root: `/Volumes/SSD/Projects/SquadFight/SquadFight`
- Unity version (from ProjectVersion.txt): 6000.6.7f1 (b22767f04f04)
- Template / render pipeline: Universal 2D template (URP 17.6.0, Renderer2D), Input System 1.20.1 (active handler = Input System only), uGUI 2.6.0 (includes TextMeshPro), Test Framework 1.8.0
- Unity Editor executable available to Claude: yes — `/Volumes/SSD/Apps/UnityEditors/6000.6.7f1/Unity.app/Contents/MacOS/Unity` (Android + iOS + Mac IL2CPP modules installed, Android SDK/NDK/JDK bundled). Batch mode works, license OK.
- dotnet available: yes, SDK 10.0.402 (used for the fast pure-C# test harness in `DotnetTests/`)
- Git initialised: yes (owner's initial commit `1baed26`; template snapshot `ccfc71f`). git-lfs 3.8.1 available.

## Acceptance log
### Step 01
- [x] Project opens/compiles with zero errors; no warnings in game code (batch compile log checked).
- [x] Boot → Main loads: PlayMode test `BootFlowTests.Boot_LoadsMainScene` (Editor). Android device run → pending (M2 device pass).
- [x] `using UnityEngine;` in Core fails: probe file produced CS0246 in batch compile, then removed.
- [x] Sample EditMode test passes (`ArchitectureTests`, 7 tests incl. no-Unity-refs check for Core/Save/Services).

### Step 02
- [x] New hero = one asset added to `GameConfig.heroes` (ConfigDatabase gathers the rest); or one CSV row + import. No code changes.
- [x] Build blocked on invalid config: `ConfigValidationBuildHook` throws `BuildFailedException` (test `BuildHook_BlocksInvalidConfig`).
- [x] Sample content loads in Boot: PlayMode `Boot_LoadsMainScene` asserts heroes loaded, no error logs.
- [x] Validator rules tested (24 cases: duplicate/bad ids, missing refs, bad weights/amounts, star gaps, rarity gaps, slot layout, unobtainable hero warning).
- [x] Importer idempotent: 2nd import = 63 unchanged; test `Importer_PreviewOfCommittedSources_IsClean`.

### Step 03
- [x] Kill loses ≤ ~1 s: debounce writes 1 s after first change (test `Debounce_SavesAtMostOneSecondAfterFirstChange`) + flush on pause/focus/quit (`SaveLifecycle`).
- [x] Corrupt `save.json` never crashes; backup restores (tests for truncated/garbage/hostile files, both corrupt → new game, interrupted tmp, crash before replace, read errors).
- [x] Old fixture saves load through migrations (`V1Fixture_MigratesToCurrent`).
- [x] Second self-review pass (subagent) for data loss/duplication/crash: 7 findings fixed, regression tests added.

### Step 04
- [x] State built from SaveData, mutated, exported to identical format (`State_ExportsIdenticalFormat_AndRoundTrips`).
- [x] Invariants: no negative currency, no party duplicates, size ≤ 5, only unlocked heroes (CoreServicesTests).
- [x] Debug console: `DebugConsole` (IMGUI) compiled only under `UNITY_EDITOR || DEVELOPMENT_BUILD`; command logic tested (`DebugCommands_GiveListReset`). Visual check on device pending.

### Step 05
- [x] Debug console shards/unlock/starup follow config exactly (`DebugConsole_ShardsAndUnlockWork`).
- [x] Edge cases: exact threshold, insufficient, double unlock, star cap, overflow conversion, party rules (HeroProgressionTests, 13 tests).
- [x] Changing RarityConfig changes behaviour without code (`ChangingRarityConfig_ChangesBehaviour`).

## Pending owner questions (batched)
- none

## Pending verifications (need real Unity / device)
- Step 01: Boot → Main on an Android device/emulator (Editor PlayMode verified).
- Step 03: IL2CPP release build loads/saves correctly (stripping/AOT) – smoke test on device.
- Step 04: debug console usable on device (4-finger tap).
