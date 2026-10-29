# Content schema (CSV → ScriptableObjects)

All game content is authored as CSV in `Assets/_Game/Content/Source/` and imported with
**Tools/Game/Content/Import CSV Content** (batch: `-executeMethod Game.EditorTools.Content.ContentImporter.ImportBatch`).
The importer matches assets **by id**, creates missing ones, updates changed ones, never deletes (ids that disappear
from the CSVs are reported as orphans). It previews first and aborts on any parse error without touching assets.
English text in the CSVs is written to `Content/Localization/strings_content.csv` under generated keys (`<id>_name`, `<id>_lore`, `<id>_desc`, `<id>_text`).

Conventions: ids are lowercase snake_case (`hero_knight`). Lists inside a cell use `;`. Lines starting with `#` are comments.
Numbers use `.` as decimal separator. Enums are case-insensitive.

| File | Columns |
|------|---------|
| `config.csv` | `key,value` — `partySize`, `currencies` (`id:Name;…`), `primaryCurrency`, `startingHeroes`, `startingCurrencies` (`id:amount;…`), `consumeShardsOnUnlock`, `shardOverflowCurrency`, `slotLayout` (`Weapon;…;Ring;Ring`), `inventoryLimit` (0 = unlimited), `equipmentRollVariation`, `rollVariationPercent`, `sellEnabled`, `starsEnabled`, `defaultStarTurnLimit`, `defeatConsolation` (table id), `currencyDropMultiplier`, `shardDropMultiplier`, `pityEnabled`, `pityFightsWithoutShard`, `showDropOdds`, `combat.minDamage`, `combat.defenseK`, `combat.maxRounds`, `combat.critChanceCap`, `combat.aiTargeting` (LowestHp/Random/First), `combat.powerWeights` (`Stat=w;…`) |
| `rarities.csv` | `rarity,color,equipmentStatMultiplier,shardsToUnlock,starCostMultiplier,dropWeightModifier,itemSellValue,overflowCurrencyPerShard` (one row per rarity) |
| `stars.csv` | `star,shardsRequired,currencyCost,statMultiplier` — star 1 is the unlocked state (costs ignored); costs are to reach that star from the previous one; shard cost × rarity `starCostMultiplier` |
| `skills.csv` | `id,name,description,targeting,targetCount,cooldown,initialCooldown,effects` |
| `heroes.csv` | `id,name,rarity,class,maxHp,attack,defense,speed,critChance,critDamage,basicSkill,activeSkill,tint,lore` |
| `enemies.csv` | `id,name,isBoss,maxHp,attack,defense,speed,critChance,critDamage,basicSkill,activeSkill,tint` |
| `equipment.csv` | `id,name,slot,rarity,stats,setId` — `stats` = `Attack=12;MaxHp=5%` (`%` = percent modifier) |
| `story.csv` | `id,speaker,speakerName,text` — `speaker` = hero/enemy id (uses its name) or any id + `speakerName` |
| `droptables.csv` | `table,group,minRolls,maxRolls,withoutReplacement,type,refId,rarity,slot,rarityWeights,weight,amountMin,amountMax` |
| `chapters.csv` | `id,order,name,clearReward,unlockConditions` |
| `fights.csv` | `id,chapter,order,name,waves,recommendedPower,dropTable,firstClearTable,starTurnLimit,preBeats,postBeats,unlockConditions` |

## Mini-syntaxes
- **targeting**: `SingleEnemy, AllEnemies, SingleAlly, AllAllies, Self, LowestHpAlly, RandomEnemies` (relative to the caster's team; `targetCount` for RandomEnemies).
- **effects** (joined by `|`, prefix `self>` to apply to the caster):
  `Damage:power` · `Heal:power` · `Shield:power:turns` · `Stun:turns[:chance]` · `Status:Stat:value:turns[:Refresh|Stack|Ignore[:maxStacks[:chance]]]`
  (power multiplies caster attack; status value is a percent: `0.25` = +25%, `-0.3` = −30%).
- **waves**: spawns joined by `+`, waves separated by `/`, optional stat multiplier `enemy_id@1.2` — e.g. `enemy_goblin+enemy_goblin / enemy_goblin_king@1.5`.
- **drop groups**: `group` = `G` (guaranteed, always given) or any name; each named group rolls `minRolls..maxRolls` times and picks entries by `weight`; `withoutReplacement` prevents the same entry twice per clear. Group settings are read from the group's first row.
- **drop types**: `Currency` (refId = currency), `HeroShard` (refId = hero), `RandomHeroShardByRarity` (rarity), `Equipment` (refId = template, rarity = item rarity), `RandomEquipment` (optional `slot`, `rarityWeights` = `Common=70;Uncommon=30`).
- **unlockConditions** (`;`-joined): `ClearFight:fight_id`, `MinPartyPower:500`, `HeroOwned:hero_id`. Empty = default (previous fight / chapter cleared).

## Adding content without the CSVs
Every type also has a `Create > Game > …` asset menu. A new hero = one `HeroDefinition` asset added to `GameConfig.heroes`
(and a drop that gives its shards). Run **Tools/Game/Validate Config** afterwards; builds are blocked on validation errors.

## Balance workflow (step 15)
- **Export**: `Tools/Game/Content/Export Tables to CSV` writes heroes/enemies/equipment/rarities/stars back from the assets (Inspector edits → spreadsheet). Export→import is a no-op (tested).
- **Fast simulations outside Unity** (seconds): `dotnet run -c Release --project DotnetTests/BalanceCli -- <command>`
  - `progression [seeds]` — greedy/casual/hardcore campaign runs (fights, losses, gold, walls, unlock times)
  - `fights casual 1` — win rate per fight at the casual player's first attempt
  - `tune` — re-fits enemy multipliers in fights.csv to target first-attempt win rates (weakest of 8 simulated players)
  - `recommend` — writes `recommendedPower` (median casual party power at first attempt)
  - `validate` — config validation on the CSVs
- **In the Editor** (`Tools/Game/Balance/…`): Fight Balance Simulator, Run Progression Simulator, Drop Simulator, Economy Report, Star & Stat Curves, Save Config Snapshot / Diff Last Two Snapshots. Reports go to `BalanceReports/` (git-ignored).
