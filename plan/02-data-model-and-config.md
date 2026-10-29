# Step 02 — Data Model & Config (the "configurable before build" layer)

**Depends on:** 01. **Size:** M

## Goal
Define every piece of game content as data. All balance, rewards, and story content live in ScriptableObjects (authored in Editor) that are validated before each build.

## Core enums & value types (Game.Core)
- `Rarity { Common, Uncommon, Rare, Epic, Legendary }`
- `StatType { MaxHp, Attack, Defense, Speed, CritChance, CritDamage }` (extensible)
- `EquipmentSlot { Weapon, Helmet, Chest, Gloves, Boots, Ring }` + slot layout config (Ring ×2)
- `StatBlock` (struct/dictionary of StatType→float) with add/multiply helpers
- `Id` conventions: lowercase snake_case strings, unique per type

## ScriptableObject definitions (Game.Data)
| Asset | Key fields |
|-------|-----------|
| `HeroDefinition` | id, nameKey, rarity, baseStats (StatBlock), skill refs, portrait/sprite refs, shardIconRef, class/role, loreKey |
| `EnemyDefinition` | id, nameKey, baseStats, skills, sprite, isBoss |
| `SkillDefinition` | id, target rule, power multiplier, cooldown, effects list (damage, heal, buff, debuff) |
| `EquipmentTemplate` | id, nameKey, slot, rarity, baseStats, icon, optional set id |
| `RarityConfig` | per rarity: color, stat multiplier, shards-to-unlock, star-up cost table, drop weight modifiers |
| `StarUpgradeConfig` | per star level: shards required, stat multiplier curve |
| `DropTable` | list of `DropEntry` (see step 10) |
| `FightDefinition` | id, enemy waves, recommended power, dropTable ref, first-clear reward, background |
| `ChapterDefinition` | id, nameKey, ordered fights, unlock condition, chapter-clear reward |
| `GameConfig` | root asset referencing all lists, party size (5), global constants |

## Config database
- `ConfigDatabase` loads `GameConfig` and builds `Dictionary<string, T>` lookups for each asset type.
- Loaded in Boot scene; exposed via `IConfigProvider`.

## Validation (Game.Editor)
`ConfigValidator` runs from menu `Tools/Game/Validate Config` and automatically in `IPreprocessBuildWithReport` (build fails on errors). Checks:
- duplicate / empty / badly formatted IDs
- missing references (sprites, drop tables, skills)
- every rarity has config; star table has no gaps
- every fight has ≥1 enemy and a drop table; chapters not empty; fight IDs unique globally
- drop weights > 0; probabilities sum sane
- orphan assets warning (hero never obtainable)

## Tasks
1. Implement enums, `StatBlock`, ID helpers in Core with tests.
2. Implement ScriptableObjects with `[CreateAssetMenu]` and custom inspectors showing computed previews (e.g., total unlock shards).
3. Implement `ConfigDatabase` + `IConfigProvider`.
4. Implement `ConfigValidator` + build hook.
5. Create **sample content**: 8 heroes (2 per rarity tier subset), 12 equipment templates, 3 enemies, 1 chapter with 5 fights, one drop table.
6. EditMode tests: validator catches duplicate IDs, missing refs, bad weights.

## Acceptance criteria
- A designer can add a new hero by creating one asset and adding it to `GameConfig`; no code changes.
- Build is blocked when config is invalid, with a clear error list.
- Sample content loads in Boot scene without errors.

## Prompt for Claude
> Read `00-overview.md` and `02-data-model-and-config.md`. Implement the enums/StatBlock in Game.Core and all ScriptableObject types in Game.Data, `ConfigDatabase`, and the editor `ConfigValidator` with build hook. Generate a small set of sample content via an editor menu script (`Tools/Game/Create Sample Content`). Write EditMode tests for validator rules. Keep all balance numbers in assets, none in code.

## Decisions made
- Stat list final: ___
- Rarity multipliers: ___
