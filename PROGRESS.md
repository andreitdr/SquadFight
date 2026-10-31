# Progress
Last updated: 2026-10-31
Current step: — (all 18 steps done; owner change "The Hundred" done 2026-10-31; owner actions remain, see MILESTONE-M3.md)

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
| 15 | Balance & content tools | DONE | EditMode 237/237, dotnet 225/225 | gold surplus noted |
| 16 | Testing & QA | DONE | EditMode 247/247, PlayMode 13/13, dotnet 235/235 | device matrix pending (owner deferred) |
| 17 | Mobile optimization & builds | DONE | EditMode 254/254; Android dev APK built | device profiling pending |
| 18 | Polish, tutorial, release (M3) | DONE | EditMode 262/262, PlayMode 14/14, dotnet 243/243 | final art, store submission and device QA need the owner |

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

### Step 15
- [x] Editing one CSV and re-importing updates exactly that asset, no duplicates (`EditingOneCsvRow_UpdatesExactlyThatAsset_NoDuplicates`; export→import no-op).
- [x] Progression simulator completes the whole campaign in seconds (BalanceCli: 9 runs in ~3 s) and flags walls.
- [x] Same seed ⇒ identical reports (`Reports_AreDeterministic`, `SameSeed_IdenticalReport`).

### Step 16
- [x] Tests run locally only (`tools/test-all.sh`); GitHub CI removed at the owner's request (2026-10-31).
- [x] Coverage targets met (Core 96% lines, Services 95.5%).
- [x] Headless playthrough deterministic (`Playthrough_IsDeterministic`, `NewGame_..._StateIdentical`).
- [~] Device matrix checklist: pending (owner deferred device tests at M2).

### Step 17
- [~] Targets on low-end device: pending device (PERF_REPORT.md task list); Editor/batch measurements recorded.
- [x] Release AAB from one command: `./build.sh android-release` (needs keystore env vars — owner); dev APK built and verified here.
- [x] Manifest has no network permission (aapt on the built APK); game runs fully offline by design.

### Step 18
- [~] New player completes the tutorial without guidance: scripted overlay + PlayMode test drives the first steps with real pointer events; real-user test pending.
- [ ] No placeholder assets in release: **35 placeholders remain** (no final art provided) — audit in PLACEHOLDER_AUDIT.md.
- [ ] Store checklists complete / RC on device matrix / 1.0 submitted: **owner actions** (accounts, keystore, art, devices).

### M3 milestone checks (2026-10-30)
- Config validator 0/0; BalanceCli progression: all 9 runs complete (casual 72–92 fights); headless playthrough + 500-fight soak green; Android IL2CPP dev APK builds (47 MB) with no network permission.

### iOS Simulator play-test (2026-10-31, iPhone 18 Pro sim, Debug IL2CPP build)
- Boot, Home, Fight Preview, story, battle, result, Heroes, unlock, party, equip and the whole 13-step tutorial run end to end.
- Fixed: tutorial lost the Sister Maren highlight on the Heroes list (recycled cards re-registered stale anchor ids); hole drawn at a pre-layout rect on slow first frames (overlay now waits 2 frames per new target); weapon-slot target off screen inside the scroll view with input blocked = player stuck (overlay now scrolls targets into view); circular ring replaced by a frame that fits wide buttons; Skip button moved into the safe area (it overlapped the top bar under the notch). `FullScript_HighlightsEveryTarget` PlayMode test walks all 13 steps and checks the hole sits on an on-screen target.
- Follow-up fixes (same day): tutorial resumes correctly after a restart (resume points + already-done checks, EditMode tests); hint kept off popup panels (PlayMode check); tutorial fights 1-1/1-2 eased so the scripted party wins with no deaths ≥ 95% (`BalanceCli tune tutorial`; progression still completes in all 9 runs, casual 72–92 fights); fight preview warns only below 90% of recommended power (90–100% = caution colour); equipment picker rows fit narrow screens. Still open: FMOD output-device error in the simulator (check on device).

### "The Hundred": story, 100 heroes, 40 chapters, side quests (owner request, 2026-10-31; steps 02/08/09/10/12/13/15 reworked)
- [x] 100 heroes, 20 per class, rarity random per hero and strictly rarer at each tier (37/26/19/12/6); every hero has a unique active and passive (`uniqueHeroAbilities` validator; `RealContent_HundredHeroes_FortyChapters`).
- [x] 14 new effect types, 4 targeting rules, passives on 9 triggers; tests for every effect/trigger + 50-seed determinism (`EffectTests`, `RosterRulesTests`).
- [x] 40 chapters (8 acts), 401 main fights (9–11 per chapter, seeded), 79 side quests; 107 new enemies incl. 37 new bosses; 449 new story beats; 96 new items (tiers 2–8 + 40 relics).
- [x] Side quests never count toward completion, stars, unlock chain or "next fight" (`SideQuestTests`); campaign screen section + per-chapter Journal.
- [x] Shards spread: 3 targeted grants per new hero across its act, random shards limited to the current acts' pool, 89 item-only fights.
- [x] Old saves: a save with the original 24 fights cleared opens chapter 4 and the new fights of chapters 1–3 (`SaveFromTheOldThreeChapters_ContinuesIntoTheNewCampaign`); new heroes are added by the reconciler.
- [x] Balance (BalanceCli progression, seeds 1–3): casual 3/3 complete in 1,440 fights, 0 losses, 0 walls, 59–64 heroes; hardcore 3/3 in 2,880 fights, 0 walls, 69–74 heroes; greedy (never replays on purpose) clears chapters 1–3 and stalls around chapter 10 (Tide Mother) — the 400-fight campaign assumes some replaying.
- [x] Tests: dotnet 298/298, config validator 0/0, Unity EditMode 333/333, PlayMode 15/15 (`tools/test-all.sh`).
- [x] Release APK 101 MB (was 185 MB before ASTC + lighter frames for the new characters; 57 MB before "The Hundred"). Art is generated placeholder art.
- Known gap: gold piles up (≈540k earned vs ≈9k spent per casual run) — nothing new to spend it on; same issue noted at step 15, now larger.

### Enemy ranks, boss specials, player level (owner request, 2026-10-31; steps 02/08/09/10/12/13/15 reworked)
- [x] Ranks Minion < Soldier < Elite < Champion < Boss (`ranks.csv`): rank HP multiplier in battle, skill count per rank, XP per rank; validator checks climbing ranks, skill counts, specials only on bosses, and higher rank = more HP in every wave (`EnemyRankTests`).
- [x] Extra active skills (own cooldowns) and boss special attacks charged at 60%/30% HP (ignore silence, delayed by stun); battle shows rank tags, "Charging …!" and a special banner.
- [x] 64 new enemies with new generated art (8 per act); every boss has an extra skill + 2 specials; all 185 enemies appear in fights (`RealContent_EnemyRanks`).
- [x] Player level: XP from defeated enemies, 60 levels with rewards, save v7, Home/result/preview UI (`PlayerLevelTests`).
- [x] Balance: tune + ease 10 + recommend; casual 3/3 done (1,440 fights, 0 losses, level 51), hardcore 3/3 (level 60), greedy stalls at chapters 6–13; config validator 0/0.
- [ ] Look at the new enemies, rank tags and special banners on a device.

### Android release pipeline (owner request, 2026-10-31)
- [x] `BuildAndroidReleaseApk` / `BuildAndroidReleaseAll` (`./build.sh android-apk|android-release-all`): signed APK + AAB with one versionCode. Verified locally with a throwaway keystore passed as base64 (CI path): apksigner verifies, no network permissions, arm64-v8a.
- [x] Releases: signed APK built locally (`tools/build-apk.sh`) when the owner asks for one; the owner uploads it to GitHub Releases. No CI.
- [x] Project-local emulator (`tools/android-emulator.sh`, `AndroidEmulator/` gitignored): release APK installs, boots to Home, tutorial + touch input reach the Fight Preview.

### iPad / tablet layout (owner request, 2026-10-31)
- [x] Tablets get a canvas sized from the physical screen (`AdaptiveCanvasScaler`, ~1.2× phone size in inches instead of ~2×); phones unchanged.
- [x] Centred content columns (`MaxSizeFitter`), Heroes/Bag grids add columns, Home centred, battle formation kept compact on a full-screen scene; tab bar grouped in the middle.
- [x] `TabletLayoutTests` (EditMode); iPad 11" screenshots in the screen tour and battle test (`*_ipad.png`; needs a run with graphics — `-nographics` captures are blank).
- [ ] Check on a real iPad / iPad Simulator.

### Gold shop and skill levels (owner request, 2026-10-31; steps 03/05/06/12 reworked)
- [x] Gear shop: current-tier weapons and armour at Common–Epic for gold; tier follows the campaign; one-transaction purchase saved at once (`ShopTests`, 9 tests).
- [x] Skill levels 1–10 for active and passive; potency scales magnitudes only; levelled skills reach battles (`SkillLevelTests`, 11 tests incl. save v5→v6 migration and reconciler).
- [x] Validator: skill level table and shop prices (price > sell value); config validator 0/0 on real content; CSV loader matches the asset pipeline incl. skill levels and shop rules.
- [x] Tests: dotnet 318/318, Unity EditMode 353/353, PlayMode 15/15 (screen tour adds shop + skill-level shots).
- [ ] Simulator does not spend gold on the new sinks yet (balance reports unchanged).

## Pending owner questions (batched)
- none (M2 answers: CSV localizer; skip device tests for now)

## Pending verifications (need real Unity / device)
- Step 01: Boot → Main on a real Android device (verified on the arm64 emulator 2026-10-31).
- Step 03: IL2CPP release build loads/saves correctly (stripping/AOT) – smoke test on device.
- Step 04: debug console usable on device (4-finger tap).
- Step 18: tutorial with real first-time players; release AAB signing (keystore); store submission.
- Step 13: full boss battle at 60 fps on a mid-range Android device; touch controls (skill buttons, pause/back) on device.
- Step 11: 60 fps scrolling of the 1,000-item list on a mid-range Android device; notch/safe area on a real notched phone.
