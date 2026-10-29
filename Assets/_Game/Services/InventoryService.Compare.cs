using System.Collections.Generic;
using Game.Core;

namespace Game.Services
{
    public sealed partial class InventoryService
    {
        /// <summary>Hero's final stats if <paramref name="instanceId"/> were equipped in <paramref name="slotIndex"/> (null = auto slot).</summary>
        public StatBlock PreviewEquip(string heroId, string instanceId, int? slotIndex = null)
        {
            var item = _ctx.State.GetItem(instanceId);
            var hero = _ctx.Db.GetHero(heroId);
            var star = Roster != null && Roster.IsUnlocked(heroId) ? Roster.GetStar(heroId) : 1;
            if (item == null || !_ctx.Db.TryGetEquipment(item.TemplateId, out var t)) return StatCalculator.Compute(hero, star, GetEquipmentStats(heroId), _ctx.Db);
            var index = slotIndex ?? EquipmentRules.ChooseSlot(Layout, t.Slot, i => EquippedSave(heroId, i) != null && EquippedSave(heroId, i) != item);
            var stats = new EquipmentStats();
            foreach (var e in _ctx.State.Data.Equipment)
            {
                if (e == item || e.EquippedByHeroId != heroId || e.EquippedSlotIndex == index) continue;
                if (_ctx.Db.TryGetEquipment(e.TemplateId, out var et)) stats.Add(EquipmentRules.GetStats(et, e.Rarity, e.RolledStats, _ctx.Db));
            }
            if (index >= 0) stats.Add(EquipmentRules.GetStats(t, item.Rarity, item.RolledStats, _ctx.Db));
            return StatCalculator.Compute(hero, star, stats, _ctx.Db);
        }

        /// <summary>Power change for the hero if the item were equipped (positive = upgrade).</summary>
        public int PowerDelta(string heroId, string instanceId, int? slotIndex = null)
        {
            var current = Roster != null ? Roster.GetPower(heroId) : 0;
            return PowerRating.Compute(PreviewEquip(heroId, instanceId, slotIndex), _ctx.Db.Combat) - current;
        }

        /// <summary>True if equipping the (unequipped) item would raise the power of at least one of <paramref name="heroIds"/>.</summary>
        public bool IsUpgradeForParty(string instanceId, IEnumerable<string> heroIds)
        {
            var item = _ctx.State.GetItem(instanceId);
            if (item == null || !string.IsNullOrEmpty(item.EquippedByHeroId)) return false;
            foreach (var h in heroIds)
                if (h != null && PowerDelta(h, instanceId) > 0) return true;
            return false;
        }
    }
}
