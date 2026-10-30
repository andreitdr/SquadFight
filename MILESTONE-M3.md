# Milestone M3 — "Release candidate" (steps 14–18)

Date: 2026-10-30 · Commit: `step-18`

## What's new since M2
- **Onboarding:** a 13-step guided first session with a highlight overlay: first fight → reward → unlock Sister Maren → add her to the party → equip the starter sword → second fight. It is saved step by step, skippable, and replayable from Settings. One-time tips cover shards, star-ups, equipment and party power.
- **Audio and feel:**
  - Music for menus, battles and bosses, plus 15 sound effects (synthesized CC0 placeholders).
  - Haptics.
  - A gold count-up on the result screen, an unlock celebration, and button feedback.
  - Reduce-motion and screen-shake options.
- **Balance:**
  - **Difficulty:** fitted per fight from simulated players. Across 12 unseen simulated players, a casual player finishes the campaign in about 72–110 fights; 10 of the 12 never needed more than two tries at any fight.
  - **Hero unlocks:** a new hero roughly every 10–15 fights; Drakon is the campaign reward.
  - **Effort levels:** "greedy" players who never replay fights must farm at a few walls; "hardcore" players never lose.
- **Tools:**
  - Balance runs outside Unity (`DotnetTests/BalanceCli`: 9 full campaign simulations in about 3 s).
  - Editor windows for fight balance, economy, curves and config diffs.
  - CSV export and import (round trip tested).
- **QA:**
  - 262 EditMode, 14 PlayMode and 243 dotnet tests.
  - Coverage: Core 96% lines, Services 95.5%.
  - A headless full loop and a 500-fight soak.
  - No memory leaks over 18 scene round trips.
  - CI workflow and `QA_CHECKLIST.md`.
- **Builds:**
  - `./build.sh android-dev` produces a 47 MB IL2CPP ARM64 APK.
  - Release AAB and iOS project targets are ready.
  - The INTERNET permission is stripped (verified in the APK).
  - Quality tiers and versioning are in place.
- **Legal and store:** privacy policy (fully offline, no data collected), generated third-party notices, and a store listing checklist with draft texts.

## How to try it
- **Editor:** play `Assets/_Game/Scenes/Boot.unity`. To see the tutorial again, use Settings → Replay tutorial or `reset` in the console.
- **Android:** install `Builds/Android/SquadFight-1.0.0-26102922-dev.apk`, a development build with the console and FPS overlay. Use the device checklist in `QA_CHECKLIST.md`.

## What only you can do before 1.0
1. **Final art and audio.** 35 placeholder assets remain; see `PLACEHOLDER_AUDIT.md`. Drop finals into `Art/Final` and `Audio/Final` with the same names, then rebuild the theme and audio library.
2. **Identity.** Choose the final game name and package id (package id `com.andreitdr.squadfight` cannot change after the first upload). Provide a support email for the privacy policy.
3. **Accounts and signing.** You need a Google Play and Apple developer account, plus an upload keystore (see `BUILD.md`). APKs are built locally with `tools/build-apk.sh` (no GitHub CI).
4. **Device QA.** Run the matrix and the performance task list (`QA_CHECKLIST.md`, `PERF_REPORT.md`). You deferred this at M2.
5. **Optional decisions:**
   - Turn on the daily bonus (`dailyBonusEnabled` in `config.csv`).
   - Add a gold sink: gold currently piles up, about 12k earned vs 600 spent by a casual player.
   - Romanian translation of content names and story.

## Known gaps
- Long labels are truncated instead of ellipsized, because of a font quirk with the dynamic font atlas.
- Content text (hero, item and fight names, story) is English in the Romanian table.
- Nothing has been verified on a physical device yet.
