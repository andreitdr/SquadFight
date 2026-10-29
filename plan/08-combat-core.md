# Step 08 — Combat Core (pure simulation)

**Depends on:** 07. **Size:** L

## Goal
A deterministic, UI-free battle simulator: given a party, an enemy setup, and an RNG seed, it produces a stream of **combat events** and a result. The presentation (step 13) just plays those events.

## Assumptions (A1, A2)
Turn-based, speed-ordered, auto-battle with optional manual skill trigger; party of up to 5 vs. 1–5 enemies, possibly multiple waves.

## Domain model (Game.Core)
- `Combatant` { id, team, stats, currentHp, skills, cooldowns, statusEffects, alive }
- `BattleState` { teams, turn counter, wave index, rng }
- `BattleSimulator` : `Step()` / `RunToEnd()` / `QueuePlayerSkill(combatantId)`
- `IBattleAI` : picks action + target. Default `SimpleAI` (use skill when off cooldown, target lowest HP / random by config).
- `TargetingRule` enum: SingleEnemy, AllEnemies, SingleAlly, AllAllies, Self, LowestHpAlly, Random(n)
- `Effect` types: Damage, Heal, ApplyStatus(Buff/Debuff: stat modifier, duration), Shield (optional), Stun (optional)
- `StatusEffect` with duration in turns, stacking rule (refresh/stack/ignore)

## Combat events (output)
`BattleStarted`, `WaveStarted`, `TurnStarted(actor)`, `ActionChosen`, `DamageDealt(src,dst,amount,crit)`, `HealApplied`, `StatusApplied/Expired`, `CombatantDied`, `WaveCleared`, `BattleEnded(result, stats)`.
Result: `Victory | Defeat | Timeout` (max turns config, default 100 → Defeat).

## Battle result stats
turns taken, survivors, per-hero damage dealt/taken (used for 3-star ratings and balance tools).

## Tasks
1. Combatant/state model + `BattleSetupFactory` building from player party (via `StatCalculator`) and `FightDefinition`.
2. Turn order logic (speed, ties) with tests.
3. Skill execution pipeline + targeting + effects + status ticking.
4. Wave handling and win/lose detection; timeout.
5. `SimpleAI`; hook for manual skill queueing.
6. Event stream (`IReadOnlyList<BattleEvent>`; also streaming callback for presentation).
7. Determinism test: same seed + setup ⇒ identical event list (hash compare), run 1,000 seeds.
8. Performance test: a 5v5 fight simulates in < 5 ms.
9. Debug CLI: EditMode menu "Simulate fight" printing a text log.

## Acceptance criteria
- Pure Core, no UnityEngine; fully deterministic.
- Every effect/targeting rule has tests; no infinite loops possible (turn cap).
- A text log of a fight reads correctly and matches hand calculation for a scripted 1v1.

## Prompt for Claude
> Read `00-overview.md`, `07-stats-and-formulas.md`, and `08-combat-core.md`. Implement the combat simulator in Game.Core: combatants, turn order, skills/effects/status, targeting, waves, AI, event stream, result stats, timeout. Add `BattleSetupFactory` in Game.Services that builds a battle from PartyService + a FightDefinition. Include determinism and performance tests and an editor "Simulate fight" log tool. No visuals.

## Decisions made
- "Turn" = round: every living unit acts once per round, ordered by effective speed (desc), then slot, then id; timeout after `maxRounds` (100) counts as defeat
- Cooldown N: used on turn t, usable again on turn t+N (decremented at end of the holder's turn); `initialCooldown` delays first use
- Statuses/shields last N of the holder's turns; applied during the holder's own turn they skip that turn's tick
- Stacking: Refresh (reset duration), Stack (new instance up to maxStacks, refreshes the others), Ignore
- RandomEnemies picks with replacement; a hit whose target died retargets another living enemy
- Enemy difficulty multiplier scales HP/ATK/DEF only (not speed/crit); MaxHp buffs are not applied mid-battle
- Manual mode (`AutoSkills=false`): only queued skills fire; enemies always use skills when ready
- Events are flat records referencing combatants by index; `BattleSimulator.Hash` for determinism checks
