# Step 07 — Stats & Formulas

**Depends on:** 05, 06. **Size:** S

## Goal
One place that computes a hero's final combat stats and all damage math, fully pure C# and unit tested, with every coefficient in config.

## Final stat pipeline
```
final = ( heroBase × starMultiplier  +  equipmentFlat ) × ( 1 + equipmentPercent + buffs )
```
- `heroBase`: `HeroDefinition.baseStats`
- `starMultiplier`: from `StarUpgradeConfig[star]` (e.g., 1.0, 1.15, 1.35, 1.6, 2.0)
- `equipmentFlat/Percent`: from equipped items (stat entries flagged flat or percent)
- Temporary buffs/debuffs are applied in combat only (step 08), not persisted.

## Formulas (all constants in `CombatConfig` asset)
- Damage = `max(minDamage, attack × skillPower × (100 / (100 + defense × defK)))`
- Crit: chance clamp 0–1, multiplier = `critDamage` (default 1.5)
- Speed → turn order (ties broken by party slot index, then ID for determinism)
- Heal = `attack × healPower`
- Hero **Power rating** (for recommended-power display): weighted sum of stats, weights in config.

## Tasks
1. `StatCalculator.Compute(HeroDefinition, star, equipment, config)` → `StatBlock`.
2. `DamageCalculator` (damage, crit, heal, mitigation) with `IRandom`.
3. `PowerRating.Compute(StatBlock, config)`.
4. `CombatConfig` ScriptableObject + validator rules.
5. Tests with hand-computed expectations for each formula, edge cases (0 defense, negative results clamped, max crit).
6. Property-style test: stats never negative; higher star never lowers any stat.

## Acceptance criteria
- Changing `defK` or star multipliers in config changes results as tests predict.
- No engine references; 100% of formula branches tested.

## Prompt for Claude
> Read `00-overview.md` and `07-stats-and-formulas.md`. Implement `StatCalculator`, `DamageCalculator`, `PowerRating` in Game.Core and `CombatConfig` in Game.Data, with validation and thorough unit tests using hand-computed values. Expose a `RosterService.GetFinalStats(heroId)` convenience in Game.Services.

## Decisions made
- Final stats clamp: no negative stats, crit chance ≤ `critChanceCap`, crit damage ≥ ×1
- Rounding: damage/heal rounded half away from zero to int; minDamage floor applied before crit
- Crit roll consumes RNG only when 0 < chance < 1 (determinism unaffected)
- Power = round(Σ stat × weight); weights in config.csv `combat.powerWeights`
