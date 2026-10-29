# Progress
Last updated: 2026-10-29
Current step: 15

| Step | Title | Status (TODO/DOING/DONE/REWORK) | Tests | Notes / pending |
|------|-------|-------------------------------|-------|-----------------|
| 01 | Project setup | DONE | EditMode 7/7, PlayMode 1/1, dotnet 7/7 | Android device run pending (see below) |
| 02 | Data model & config | DONE | EditMode 49/49, PlayMode 1/1, dotnet 45/45 | Some heroes unobtainable until ch2-3 content (step 09/10) – validator warnings |
| 03 | Save system | DONE | EditMode 102/102 (all), dotnet 98/98 | Second review pass done; IL2CPP device smoke test pending |
| 04 | Player state & services | DONE | (same run) | Debug console in-Editor manual check pending |
| 05 | Heroes, shards & unlocking | DONE | dotnet 111/111, Unity verified in step 06 run | |
| 06 | Equipment system | DONE | EditMode 133/133, dotnet 129/129 | |
| 07 | Stats & formulas | DONE | dotnet 140/140, Unity verified in step 08 run | branch coverage measured in step 16 |
| 08 | Combat core | DONE | EditMode 161/161, dotnet 156/156 | |
| 09 | Campaign progression | DONE | EditMode 178/178, dotnet 173/173 | recommended power filled by step-15 tool |
| 10 | Rewards & drop tables (M1) | DONE | EditMode 207/207, PlayMode 1/1, dotnet 202/202 | Second review pass done; balance pass in step 15 |
| 11 | UI foundation | DONE | EditMode 211/211, PlayMode 7/7, dotnet 206/206 | 60 fps scroll on device pending |
| 12 | UI screens | DONE | EditMode 222/222, PlayMode 9/9, dotnet 217/217 | device pass pending; battle presentation in step 13 |
| 13 | Battle presentation (M2) | DONE | EditMode 222/222, PlayMode 12/12, dotnet 217/217 | 60 fps on mid-range Android pending |
| 14 | Audio, settings, localization | DONE | EditMode 232/232, PlayMode 12/12, dotnet 222/222 | background audio pause on device pending |
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

### Step 06
- [x] Equipment state survives save/load exactly (`EquipmentState_SurvivesSaveLoadExactly`, `RolledStats_UsedForEquipmentStats_AndSurviveSave`).
- [x] `GetEquipmentStats` equals manual calculation for all 5 rarities (parameterised test).
- [x] New slot = enum + config only (`SlotLayoutIsConfig_AddingASlotNeedsNoCode` adds a second weapon slot via config).

### Step 07
- [x] Changing defK / star multipliers changes results as tests predict (`Damage_DefKChangesMitigation`, `StatPipeline_ChangingStarMultiplierChangesResult`).
- [x] No engine refs (ArchitectureTests); formula branches covered by FormulaTests (min damage, zero/negative defense, crit cap, crit dmg < 1, negative attack, property test over random gear).

### Step 08
- [x] Pure Core, deterministic: 1,000 seeds × 2 runs identical hashes (`Determinism_1000Seeds_IdenticalEventStreams`).
- [x] Every effect/targeting rule tested; turn cap prevents infinite loops (`Timeout_AfterMaxRounds_NoInfiniteLoop`).
- [x] Scripted 1v1 matches hand calculation and the text log reads correctly (`Scripted1v1_MatchesHandCalculation`; editor tool output checked).
- [x] 5v5 < 5 ms (measured avg well below in dotnet and Unity runs).

### Step 09
- [x] Debug console plays 1-1 → last fight in order (`DebugConsole_PlaysWholeCampaignInOrder`, `UnlockChain_FightByFight_ThenNextChapter`).
- [x] Progress persists after restart (`Progress_PersistsAcrossRestart_AndIsSavedImmediately`).
- [x] Reordering = editing the Chapter asset only (`ReorderingChapterAsset_ChangesOrder`; importer orders by `order` column).

### Step 10
- [x] Editing a drop table asset changes rewards without code (all rewards come from DropTableDef via DropRoller; content import tested).
- [x] Simulator output matches weights within tolerance (`Statistical_ObservedMatchesWeights` ±0.03; Drop Simulator report on real tables: |Δ| ≤ 0.012 picks/clear).
- [x] Rewards persist; save has correct counts right after a fight without any tick (`RewardsPersist_SaveHasCorrectCountsAfterFight`).
- [x] Atomic apply / rollback (`CompleteFight_FailureMidway_RollsBackProgressAndRewards`, `Apply_InvalidBundle_LeavesStateUnchanged`), first-clear once, pity, migration.
- [x] Second review pass (subagent): 8 findings fixed + regression tests.

### M1 milestone checks (2026-10-29)
- Config validator: **0 errors, 0 warnings** on real content.
- Headless simulated playthrough: campaign completed by all 9 runs (3 policies × 3 seeds).
- Progression simulator (BalanceReports/progression.md): greedy 24 fights, casual 72, hardcore 264; no difficulty walls; first unlock at fight 1, second at fight 5. **Known balance gap:** campaign too easy for a greedy player (0 losses), gold barely spent, Rare+ heroes unlock late or not at all within one pass → step 15 balance pass.

### Step 11
- [x] Gallery renders at 16:9, 19.5:9 and 4:3 (PlayMode capture `TestResults/screens/gallery_*.png`, inspected; tablet layout centred). Real notched devices pending.
- [x] Back pops popups then screens, asks to quit at the root; popped screens are destroyed (`Navigation_PushPopBack_DestroysInstances`, `Replace_ClearStack_LeavesOneScreen`).
- [x] No service mutation in views: views only call view models / services through VM commands (code review; screens in step 12 follow the same rule).
- [x] Pooled list ×1000: < 30 live items, < 40 ever created, layout < 2 ms per scroll step (`RecycleList_1000Items_StaysBoundedAndFast`). Profiler check on device pending.

### Step 12
- [x] Full loop playable in the Editor: PlayMode `FullLoop_AndEveryScreen` boots the game, opens Campaign → Preview → Battle scene → Result, unlocks a hero, opens picker, party, bag, settings; save written before the result shows. (Battle presentation itself = step 13; device pass pending.)
- [x] No raw keys/placeholder text: every key used exists in strings_ui.csv/strings_content.csv (screenshots inspected; missing-key tool in step 14).
- [x] No dead ends: every screen has back/tab navigation; Result → Next/Retry/Home.
- [x] 16:9 / 19.5:9 / tablet: gallery captured at all three; screen tour at 19.5:9 (tour_*.png). Real devices pending.

### Step 13
- [~] 5v5 boss fight smooth at 60 fps on a mid-range Android device → **pending device**; Editor run of a full boss fight completes with no errors.
- [x] Skip and ×3 (tested at ×60) give the same result as normal: `FullBossBattle_PlayedFast_MatchesSimulation_NoDesync` (event-stream hash identical), `Skip_GivesSameOutcomeAsFullSimulation`.
- [x] HP bars never desync: dev-build assert after every turn; test asserts 0 desyncs.

### M2 milestone checks (2026-10-29)
- Config validator: 0 errors, 0 warnings. Progression simulator: all 9 runs complete (24 / 72 / 264 fights). Headless + PlayMode full loop (boot → campaign → preview → battle scene → result → heroes/party/bag/settings) passes.

### Step 14
- [x] Changing language at runtime updates all visible text (PlayMode `LanguageSwitch_UpdatesVisibleText`).
- [x] Volume/vibration persist across restarts (`SettingsPersistAcrossRestart`; settings live in the save).
- [~] Audio stops/resumes on background: `AudioListener.pause` on pause/focus (code); device check pending.
- [x] Every used key has English text (`EveryUsedKey_HasEnglishText`, 317 keys); all UI keys translated to ro.

## Pending owner questions (batched)
- none (M2 answers: CSV localizer; skip device tests for now)

## Pending verifications (need real Unity / device)
- Step 01: Boot → Main on an Android device/emulator (Editor PlayMode verified).
- Step 03: IL2CPP release build loads/saves correctly (stripping/AOT) – smoke test on device.
- Step 04: debug console usable on device (4-finger tap).
- Step 13: full boss battle at 60 fps on a mid-range Android device; touch controls (skill buttons, pause/back) on device.
- Step 11: 60 fps scrolling of the 1,000-item list on a mid-range Android device; notch/safe area on a real notched phone.
