# Step 15 — Balance & Content Tools

**Depends on:** 07–10. **Size:** M

## Goal
Make tuning a fun-to-play economy practical: simulate thousands of fights/playthroughs, import content from spreadsheets, and see pacing numbers before building.

## Tools (all in Game.Editor)
1. **CSV/Google-Sheet-export importer:** heroes, enemies, equipment templates, rarity config, star table, fights, drop tables → creates/updates ScriptableObjects by ID (idempotent, shows a diff preview, never deletes without confirmation). Export back to CSV too.
2. **Fight Balance Simulator:** pick a fight + party preset (or "auto-best party from owned heroes") → run N seeded battles using the real `BattleSimulator` → win rate, avg turns, avg HP lost, stars distribution.
3. **Progression Simulator:** simulates a "typical player" through the whole campaign using drop tables: greedy policy (unlock hero when possible, equip best items, star-up, fight next unlocked fight with best party) → outputs:
   - fights needed per chapter, shards earned per hero, time to unlock each hero (in number of fights), power curve vs. enemy recommended power, wall points (win rate < 50%).
   - Configurable player archetypes (casual: replays 2× per fight; hardcore: replays 10×).
4. **Economy report:** shards per clear, items per clear by rarity, gold income vs. sinks (sell values, if any).
5. **Rarity/Stat curve visualizer:** editor window with charts of star multipliers and stat growth.
6. **Config diff tool:** compare two config snapshots (for balance patches).
7. Reports exported as markdown/CSV into `BalanceReports/` (git-ignored).

## Tasks
1. Importer/exporter with schema docs (`CONTENT_SCHEMA.md`).
2. Fight balance simulator window (runs off the main thread where possible, with progress bar).
3. Progression simulator with policy interface (`IPlayerPolicy`) so new strategies can be added.
4. Reports + charts.
5. Tests for importer idempotency and simulator determinism.

## Acceptance criteria
- Editing one CSV and re-importing updates assets correctly without duplicates.
- Progression simulator completes the entire sample campaign in seconds and flags difficulty walls.
- Same seed ⇒ identical reports.

## Prompt for Claude
> Read `00-overview.md` and `15-balance-and-content-tools.md`. Implement the editor tools: CSV importer/exporter with documented schema, fight balance simulator, progression simulator with pluggable player policies, report export, and curve visualizer. Reuse Core simulators — do not duplicate game logic. Provide tests for idempotency and determinism.
