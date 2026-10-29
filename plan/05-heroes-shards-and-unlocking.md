# Step 05 — Heroes, Shards & Unlocking

**Depends on:** 04. **Size:** M

## Goal
Implement the collection mechanic: gather hero-specific shards → unlock hero → further shards upgrade the hero's star level and base stats.

## Rules (all numbers in config)
1. Each hero has its own shard type (`shard_<heroId>`), stored per hero in `HeroSave.shards`.
2. **Unlock:** hero unlocks when `shards >= RarityConfig[hero.rarity].shardsToUnlock`. Shards are consumed on unlock (decision D1: consume vs. keep — default **consume**).
3. **Auto vs manual unlock:** default **manual** (player presses "Unlock" in hero screen; button highlights when affordable).
4. **Star-up:** for an unlocked hero, `StarUpgradeConfig[currentStar → next].shardsRequired`. Excess shards beyond max star are **converted to a currency** (config: `shardsToCurrencyRate`) or kept (D2).
5. Max star configurable (default 5). Each star applies `starMultiplier` to the hero's base stats (see step 07).
6. Shards found for a locked hero accumulate; shards found for an unlocked hero go to the star-up pool (same counter, different threshold).
7. Starting heroes unlocked at star 1 on new game.

## API (RosterService additions)
```
int GetShards(heroId)
bool IsUnlocked(heroId)
int GetStar(heroId)
UnlockInfo GetUnlockInfo(heroId)      // have/need, canUnlock
StarUpInfo GetStarUpInfo(heroId)      // have/need, canUpgrade, nextMultiplier
Result AddShards(heroId, amount)      // used by RewardService
Result UnlockHero(heroId)
Result UpgradeStar(heroId)
```
Events: `ShardsChanged`, `HeroUnlocked`, `HeroStarUpgraded`.

## Tasks
1. Pure-C# `HeroProgressionRules` in Core (stateless: can unlock?, cost, next star, overflow conversion) + tests.
2. Wire into `RosterService`, mutations transactional, save dirty.
3. `PartyService` enforcement: only unlocked heroes may be placed in party; if a hero is somehow removed, party slot cleared.
4. Config: fill `RarityConfig.shardsToUnlock` (e.g., Common 10 / Uncommon 20 / Rare 40 / Epic 80 / Legendary 160 — tune later) and `StarUpgradeConfig` table.
5. Debug console commands from step 04 now functional.
6. Tests: exact threshold unlock, insufficient shards rejected, double-unlock rejected, star cap, overflow conversion, party rules.

## Acceptance criteria
- Giving shards via debug console unlocks/upgrades heroes exactly per config.
- All rule edge cases covered by passing tests.
- Changing a number in `RarityConfig` changes behavior with no code edit.

## Prompt for Claude
> Read `00-overview.md` and `05-heroes-shards-and-unlocking.md`. Implement `HeroProgressionRules` in Game.Core and extend `RosterService`/`PartyService` for unlocking and star-ups with events and save integration. Use defaults D1=consume shards on unlock, manual unlock, overflow converted to currency. Provide thorough unit tests. No UI.

## Decisions made
- D1 consume vs keep: consume (config `consumeShardsOnUnlock`)
- D2 overflow handling: converted to `shardOverflowCurrency` at `overflowCurrencyPerShard` (per rarity), both for incoming shards at max star and leftovers when max star is reached
- Unlock is manual; star-ups also cost primary currency (`stars.csv currencyCost`); shard cost × rarity `starCostMultiplier`
