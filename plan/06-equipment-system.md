# Step 06 — Equipment System

**Depends on:** 04. **Size:** M

## Goal
Heroes wear a weapon, armor pieces and rings. Items have rarity; higher rarity means stronger stats. Items are dropped, owned, equipped, and swapped.

## Rules (config-driven)
- Slots (A3): Weapon, Helmet, Chest, Gloves, Boots, Ring ×2. Slot layout defined in `GameConfig.slotLayout` (list of slots, so adding a Necklace later is config + enum).
- An item **instance** = `templateId` + `rarity` + optional rolled variation. Stats = `template.baseStats × RarityConfig[rarity].statMultiplier` (+ rolled bonus if enabled).
- **Roll variation (optional, config flag):** ±X% random per stat at drop time, stored in `EquipmentSave.rolledStats` so it never changes afterwards.
- Item sets (optional, v1.1): `setId` + thresholds for bonuses. Reserve the field only.
- Equipping an item already worn by another hero **moves** it (UI confirms), no duplication.
- Rings: two ring slots, same ring template may be worn twice only if two instances exist.
- Item level requirements: none in v1.

## API (InventoryService additions)
```
Result AddItem(EquipmentInstance)
Result Equip(heroId, instanceId, slotIndex?)   // auto picks slot, handles swap
Result Unequip(heroId, slot)
IReadOnlyList<EquipmentInstance> Query(slot?, minRarity?, sortBy)
EquipmentInstance GetEquipped(heroId, slot, index)
StatBlock GetEquipmentStats(heroId)           // sum of equipped items
Result SellItem(instanceId)                   // optional: converts to gold via config; blocked if equipped
```
Events: `ItemAdded`, `ItemEquipped`, `ItemUnequipped`, `ItemSold`.

## Tasks
1. Core: `EquipmentInstance`, `EquipmentFactory.Create(template, rarity, IRandom, config)` with tests (deterministic with seeded RNG).
2. Slot rules in Core: `EquipmentRules.CanEquip(slotLayout, current, item)`.
3. Service implementation + save mapping (guid instance IDs).
4. Inventory capacity: config `inventoryLimit` (default unlimited/large); overflow handling = auto-sell lowest rarity or block (D3, default: no limit in v1).
5. Sample content: 4 rarity tiers × each slot template set.
6. Tests: equip/unequip/swap between heroes, ring double-slot, stat sums, rarity multiplier math, roll determinism, save round-trip.

## Acceptance criteria
- Equipment state survives save/load exactly.
- `GetEquipmentStats` equals manual calculation in tests for all rarities.
- Adding a new slot type needs only enum + config edit.

## Prompt for Claude
> Read `00-overview.md` and `06-equipment-system.md`. Implement `EquipmentInstance`, factory, slot rules in Game.Core and extend `InventoryService` for equip/unequip/swap/query/sell with events and save integration. Rolled stat variation is behind a config flag. Provide unit tests as listed. No UI.

## Decisions made
- Roll variation enabled: ___
- Inventory limit: ___
