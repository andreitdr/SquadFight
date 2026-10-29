# Milestone M1 — "Logic complete" (steps 01–10)

Date: 2026-10-29 · Commit: `step-10` (e9377d0)

## What works
All game logic runs in pure C# (Core/Save/Services are engine-free) and is covered by tests:
- **Save system**: atomic `save.tmp → save.json` with `save.bak`, SHA-256 envelope with save counter, recovery from corrupt/partial files, read-only protection for saves the app can't read (newer version, IO error), migrations v1→v2→v3 with fixtures, debounced auto-save (≤ ~1 s loss) + flush on pause/quit.
- **Heroes**: 10 heroes over 5 rarities, shards → manual unlock (shards consumed) → star-ups (shards + gold), overflow beyond max star converts to gold.
- **Equipment**: 24 templates (6 slot kinds × 4 tiers), 7-slot layout from config (two rings), equip/move/swap/sell, rarity multipliers, optional rolled stats.
- **Combat**: deterministic turn-based auto-battle (speed order, basic + active skills with cooldowns, damage/heal/shield/stun/buffs/debuffs with stacking, waves, bosses, timeout, manual skill queueing). 1,000-seed determinism test, 5v5 well under 5 ms.
- **Campaign**: 3 chapters × 8 fights with story beats, unlock chain, 3-star ratings, first-clear and chapter-clear rewards (granted exactly once).
- **Rewards**: configurable drop tables (guaranteed + weighted roll groups, random hero shards by rarity, random equipment with rarity weights, multipliers, optional pity), applied atomically together with progress and saved before the result is shown.
- **Content pipeline**: everything is CSV in `Assets/_Game/Content/Source/` → `Tools/Game/Content/Import CSV Content` (idempotent, previews, never deletes). Schema: `CONTENT_SCHEMA.md`. Builds are blocked by `Tools/Game/Validate Config` errors.

## Numbers
- Tests: **207/207 EditMode**, **1/1 PlayMode** (Unity batch mode), **202/202** in the fast dotnet harness.
- Config validator on real content: **0 errors, 0 warnings**.
- Progression simulator (3 policies × 3 seeds, `BalanceReports/progression.md`): campaign completed in every run — greedy 24 fights, casual 72, hardcore 264; no difficulty walls; first hero unlocked after fight 1.
- Drop simulator: observed rates within ±0.012 picks/clear of configured weights.
- Two independent review passes (save system, rewards/battle flow) found 15 issues in total, all fixed with regression tests.

## How to try it
1. Open the project in Unity 6000.6.7f1 and press Play in `Assets/_Game/Scenes/Boot.unity` (or Main — it boots first).
2. Press **`** (backquote) to open the dev console (four-finger tap on a device; dev builds only). Try:
   `help` · `list heroes` · `fight fight_1_1` · `give shards hero_rogue 20` · `unlock hero_rogue` · `starup hero_knight` · `give item eq_iron_sword epic` · `list items` · `clear all` · `reset`
3. Menus: `Tools/Game/Debug/Simulate Fight` (select a Fight asset), `Tools/Game/Balance/Drop Simulator`, `Tools/Game/Balance/Run Progression Simulator`, `Tools/Game/Save/*`.
4. F3 toggles the FPS/memory overlay.

There is no game UI yet (that is M2: steps 11–13).

## Known gaps
- **Balance**: the campaign is currently too easy for a greedy player (no losses), gold is rarely spent, Rare+ heroes unlock late. Scheduled for the step-15 balance pass with the simulators.
- Fight `recommendedPower` values are 0 until the step-15 tool computes them.
- Not yet verified on a real Android/iOS device (Boot→Main, IL2CPP save round trip, debug console gestures) — batched for the M2 device pass.

## Defaults I chose that you may want to change (no action needed)
- Name/package: **SquadFight**, `com.andreitdr.squadfight`.
- Fantasy roster and story (placeholder text), 3 common starting heroes.
- Star-ups cost gold as well as shards (gold sink).
- All choices are logged in `DECISIONS.md` marked "DEFAULTED – owner may override".

## Pending questions
None blocking. I'll continue with M2 (UI foundation, screens, battle presentation).
