# Step 10 — Rewards & Drop Tables (configurable random rewards)

**Depends on:** 05, 06, 09. **Size:** M

## Goal
Each fight rewards random items from a drop table defined in config before build. Rewards are rolled via `IRandom`, applied atomically, and shown to the player.

## Data model
`DropTable` (ScriptableObject):
- `guaranteed` : list of `RewardEntry` (always given)
- `rolls` : list of `RollGroup { rollCount (min/max), entries: DropEntry[] }`
- `DropEntry { type, refId, rarity?, weight, amountMin, amountMax }`
  - types: `Currency`, `HeroShard(heroId)`, `RandomHeroShardByRarity(rarity)`, `Equipment(templateId + rarity)`, `RandomEquipment(slot?, rarity weights)`
- Optional: `pity` rule (e.g., after N fights without a shard, guarantee one) — config flag, default off; counters stored in save.
- `FightDefinition.dropTable` (repeat clears) + `firstClearTable` (first-clear bonus).
- Global multipliers in `GameConfig` (e.g., `dropRateMultiplier` for tuning builds).

## Roller (Game.Core)
- `DropRoller.Roll(DropTable data, IRandom, context) → RewardBundle`
- Weighted selection (cumulative weights, deterministic), no-replacement option per group, zero-weight skipping.
- `RewardBundle`: currencies, shards per hero, equipment instances (already built via `EquipmentFactory`).

## RewardService
- `Apply(RewardBundle)` transactional: shards → RosterService (can trigger "unlockable" notifications), items → Inventory, currency → CurrencyService, one save.
- Returns `RewardResult` containing what changed (e.g., `HeroNowUnlockable`, `StarUpAvailable`, `NewItemIsBetterThanEquipped`) for UI highlights.

## Tasks
1. Extend config data + validator (weights, hero refs, rarity weights, sums).
2. Implement `DropRoller`, `RewardBundle`, `RewardService`.
3. Pity counters in save + migration step.
4. Replace the stub in `ProgressService.CompleteFight`.
5. Editor tool: **Drop Simulator** — roll a table N times (10k) and print expected vs observed rates, avg shards/item per clear.
6. Tests: statistical test with fixed seed (observed within tolerance), guaranteed entries, first-clear only once, atomic apply (failure leaves state unchanged), pity behavior.

## Acceptance criteria
- Editing a drop table asset changes in-game rewards without code changes.
- Simulator output matches configured weights within tolerance.
- Rewards persist; save contains correct counts after a fight.

## Prompt for Claude
> Read `00-overview.md` and `10-rewards-and-drop-tables.md`. Extend DropTable data, implement `DropRoller` in Core, `RewardService` in Services (transactional), pity counters with save migration, wire into `ProgressService.CompleteFight`, and add the editor Drop Simulator window. Provide statistical and atomicity tests.

## Decisions made
- Pity enabled: no by default (config `pityEnabled`, `pityFightsWithoutShard`); counter in save (`pity.fightsWithoutShard`, migration v2→v3, fixture `save_v2.json`)
- Pity shard = 1 shard of a hero the fight's main drop table can give (fallback: a random locked hero)
- Drop rate multipliers: `currencyDropMultiplier`, `shardDropMultiplier` (amounts, min 1)
- RandomEquipment rarity weights × rarity `dropWeightModifier`; template uniform within the slot filter
- Full inventory never loses rewards: overflow items are converted to their sell value
- Rewards are applied inside the fight-completion transaction and saved immediately; any failure rolls back progress + rewards
- Second review pass for duplication/atomicity: done (see DECISIONS step 10)
