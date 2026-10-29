using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Save;

namespace Game.Services
{
    public enum ItemSort { RarityDesc, Newest, Slot }

    public sealed partial class InventoryService
    {
        internal RosterService Roster { get; set; }
        internal CurrencyService Currency { get; set; }

        private IReadOnlyList<EquipmentSlot> Layout => _ctx.Db.Rules.SlotLayout;

        private EquipmentSave EquippedSave(string heroId, int slotIndex) =>
            _ctx.State.Data.Equipment.FirstOrDefault(e => e.EquippedByHeroId == heroId && e.EquippedSlotIndex == slotIndex);

        public ItemInfo? GetEquipped(string heroId, int slotIndex)
        {
            var s = EquippedSave(heroId, slotIndex);
            return s == null ? (ItemInfo?)null : Get(s.InstanceId);
        }

        /// <summary>Equipped item per slot index (null = empty), length = slot layout.</summary>
        public ItemInfo?[] GetEquippedBySlot(string heroId)
        {
            var result = new ItemInfo?[Layout.Count];
            foreach (var e in _ctx.State.Data.Equipment)
                if (e.EquippedByHeroId == heroId && e.EquippedSlotIndex >= 0 && e.EquippedSlotIndex < result.Length && _ctx.Db.TryGetEquipment(e.TemplateId, out var t))
                    result[e.EquippedSlotIndex] = new ItemInfo(e, t);
            return result;
        }

        public List<StatModifier> GetItemStats(string instanceId)
        {
            var s = _ctx.State.GetItem(instanceId);
            if (s == null || !_ctx.Db.TryGetEquipment(s.TemplateId, out var t)) return new List<StatModifier>();
            return EquipmentRules.GetStats(t, s.Rarity, s.RolledStats, _ctx.Db);
        }

        /// <summary>Sum of the hero's equipped item modifiers.</summary>
        public EquipmentStats GetEquipmentStats(string heroId)
        {
            var total = new EquipmentStats();
            foreach (var e in _ctx.State.Data.Equipment)
                if (e.EquippedByHeroId == heroId && _ctx.Db.TryGetEquipment(e.TemplateId, out var t))
                    total.Add(EquipmentRules.GetStats(t, e.Rarity, e.RolledStats, _ctx.Db));
            return total;
        }

        public IReadOnlyList<ItemInfo> Query(EquipmentSlot? slot = null, Rarity? minRarity = null, ItemSort sort = ItemSort.RarityDesc, bool includeEquipped = true)
        {
            IEnumerable<ItemInfo> q = All();
            if (slot.HasValue) q = q.Where(i => i.Slot == slot.Value);
            if (minRarity.HasValue) q = q.Where(i => i.Rarity >= minRarity.Value);
            if (!includeEquipped) q = q.Where(i => !i.IsEquipped);
            switch (sort)
            {
                case ItemSort.Newest: q = q.OrderByDescending(i => i.AcquiredOrder); break;
                case ItemSort.Slot: q = q.OrderBy(i => i.Slot).ThenByDescending(i => i.Rarity).ThenByDescending(i => i.AcquiredOrder); break;
                default: q = q.OrderByDescending(i => i.Rarity).ThenBy(i => i.Slot).ThenByDescending(i => i.AcquiredOrder); break;
            }
            return q.ToList();
        }

        /// <summary>
        /// Equips an item. Without <paramref name="slotIndex"/> the first empty compatible slot is used (else the first
        /// compatible slot). An item worn by another hero (or in another slot) is moved; the displaced item returns to the bag.
        /// </summary>
        public Result Equip(string heroId, string instanceId, int? slotIndex = null)
        {
            var item = _ctx.State.GetItem(instanceId);
            if (item == null) return Result.Fail(ErrorCode.NotFound, instanceId);
            if (!_ctx.Db.TryGetEquipment(item.TemplateId, out var template)) return Result.Fail(ErrorCode.NotFound, item.TemplateId);
            if (Roster == null || !Roster.IsUnlocked(heroId)) return Result.Fail(ErrorCode.HeroLocked, heroId);

            var index = slotIndex ?? EquipmentRules.ChooseSlot(Layout, template.Slot, i => EquippedSave(heroId, i) != null && EquippedSave(heroId, i) != item);
            if (index < 0 || index >= Layout.Count) return Result.Fail(ErrorCode.InvalidSlot, $"no {template.Slot} slot");
            if (!EquipmentRules.CanEquip(Layout, index, template.Slot)) return Result.Fail(ErrorCode.SlotIncompatible, $"{template.Slot} cannot go in {Layout[index]}");
            if (item.EquippedByHeroId == heroId && item.EquippedSlotIndex == index) return Result.Success;

            var previousOwner = item.EquippedByHeroId;
            var previousIndex = item.EquippedSlotIndex;
            var displaced = EquippedSave(heroId, index);
            if (displaced != null)
            {
                displaced.EquippedByHeroId = null;
                displaced.EquippedSlotIndex = -1;
            }
            item.EquippedByHeroId = heroId;
            item.EquippedSlotIndex = index;
            _ctx.Commit();
            if (displaced != null) _ctx.Emit(new ItemUnequipped(heroId, displaced.InstanceId, index));
            if (!string.IsNullOrEmpty(previousOwner)) _ctx.Emit(new ItemUnequipped(previousOwner, item.InstanceId, previousIndex));
            _ctx.Emit(new ItemEquipped(heroId, item.InstanceId, index, previousOwner));
            return Result.Success;
        }

        public Result Unequip(string heroId, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Layout.Count) return Result.Fail(ErrorCode.InvalidSlot);
            var item = EquippedSave(heroId, slotIndex);
            if (item == null) return Result.Fail(ErrorCode.NotFound, "slot is empty");
            item.EquippedByHeroId = null;
            item.EquippedSlotIndex = -1;
            _ctx.Commit();
            _ctx.Emit(new ItemUnequipped(heroId, item.InstanceId, slotIndex));
            return Result.Success;
        }

        public Result UnequipAll(string heroId)
        {
            for (var i = 0; i < Layout.Count; i++)
                if (EquippedSave(heroId, i) != null) Unequip(heroId, i);
            return Result.Success;
        }

        public long GetSellValue(string instanceId)
        {
            var s = _ctx.State.GetItem(instanceId);
            return s == null ? 0 : _ctx.Db.GetRarity(s.Rarity).ItemSellValue;
        }

        public Result SellItem(string instanceId)
        {
            if (!_ctx.Db.Rules.SellEnabled) return Result.Fail(ErrorCode.FeatureDisabled, "selling is disabled");
            var item = _ctx.State.GetItem(instanceId);
            if (item == null) return Result.Fail(ErrorCode.NotFound, instanceId);
            if (!string.IsNullOrEmpty(item.EquippedByHeroId)) return Result.Fail(ErrorCode.ItemEquipped, $"equipped by {item.EquippedByHeroId}");
            var value = GetSellValue(instanceId);
            _ctx.State.RemoveItem(instanceId);
            if (value > 0 && Currency != null) Currency.Add(_ctx.Db.Rules.PrimaryCurrencyId, value);
            _ctx.Commit();
            _ctx.Emit(new ItemSold(instanceId, value));
            return Result.Success;
        }

        /// <summary>Sells every unequipped item at or below <paramref name="maxRarity"/>; returns the number sold.</summary>
        public int SellAllUnequipped(Rarity maxRarity)
        {
            var ids = _ctx.State.Data.Equipment.Where(e => string.IsNullOrEmpty(e.EquippedByHeroId) && e.Rarity <= maxRarity).Select(e => e.InstanceId).ToList();
            var sold = 0;
            foreach (var id in ids) if (SellItem(id).Ok) sold++;
            return sold;
        }
    }
}
