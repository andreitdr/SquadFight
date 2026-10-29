using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Sum of equipment modifiers: flat values and percent bonuses per stat.</summary>
    public sealed class EquipmentStats
    {
        public readonly StatBlock Flat = new StatBlock();
        public readonly StatBlock Percent = new StatBlock();

        public void Add(IEnumerable<StatModifier> mods)
        {
            foreach (var m in mods)
            {
                if (m.Kind == ModifierKind.Percent) Percent[m.Stat] += m.Value;
                else Flat[m.Stat] += m.Value;
            }
        }
    }

    public static class EquipmentFactory
    {
        /// <summary>
        /// Creates an instance of <paramref name="template"/> at <paramref name="rarity"/>. The instance id and (when roll
        /// variation is on) the rolled stats come from <paramref name="rng"/>, so a seeded RNG gives identical items.
        /// </summary>
        public static EquipmentInstance Create(EquipmentTemplateDef template, Rarity rarity, IRandom rng, GameDatabase db)
        {
            var item = new EquipmentInstance { InstanceId = NewId(rng), TemplateId = template.Id, Rarity = rarity };
            if (db.Rules.EquipmentRollVariation && db.Rules.RollVariationPercent > 0)
            {
                var mult = db.GetRarity(rarity).EquipmentStatMultiplier;
                var p = db.Rules.RollVariationPercent;
                item.RolledStats = new List<StatModifier>(template.Stats.Count);
                foreach (var s in template.Stats)
                {
                    var roll = 1 + (rng.NextDouble() * 2 - 1) * p;
                    var value = (float)Math.Round(s.Value * mult * roll, s.Kind == ModifierKind.Percent || s.Value < 1 ? 4 : 1);
                    item.RolledStats.Add(new StatModifier(s.Stat, value, s.Kind));
                }
            }
            return item;
        }

        /// <summary>RFC 4122 v4-format GUID built from the game RNG (deterministic under a seed).</summary>
        public static string NewId(IRandom rng)
        {
            var b = new byte[16];
            for (var i = 0; i < 16; i += 4)
            {
                var v = rng.NextInt(int.MinValue, int.MaxValue);
                b[i] = (byte)v; b[i + 1] = (byte)(v >> 8); b[i + 2] = (byte)(v >> 16); b[i + 3] = (byte)(v >> 24);
            }
            b[7] = (byte)((b[7] & 0x0F) | 0x40);
            b[8] = (byte)((b[8] & 0x3F) | 0x80);
            return new Guid(b).ToString();
        }
    }

    public static class EquipmentRules
    {
        /// <summary>Final modifiers of an item: rolled stats if present, else template × rarity multiplier.</summary>
        public static List<StatModifier> GetStats(EquipmentTemplateDef template, Rarity rarity, IReadOnlyList<StatModifier> rolled, GameDatabase db)
        {
            if (rolled != null && rolled.Count > 0) return new List<StatModifier>(rolled);
            var mult = db.GetRarity(rarity).EquipmentStatMultiplier;
            var list = new List<StatModifier>(template.Stats.Count);
            foreach (var s in template.Stats) list.Add(new StatModifier(s.Stat, s.Value * mult, s.Kind));
            return list;
        }

        public static bool CanEquip(IReadOnlyList<EquipmentSlot> layout, int slotIndex, EquipmentSlot itemSlot) =>
            slotIndex >= 0 && slotIndex < layout.Count && layout[slotIndex] == itemSlot;

        /// <summary>
        /// Slot index to use when the player does not pick one: the first empty compatible slot, else the first compatible one
        /// (its item gets replaced). -1 if the layout has no such slot. <paramref name="occupied"/>(index) says if a slot is used.
        /// </summary>
        public static int ChooseSlot(IReadOnlyList<EquipmentSlot> layout, EquipmentSlot itemSlot, Func<int, bool> occupied)
        {
            var first = -1;
            for (var i = 0; i < layout.Count; i++)
            {
                if (layout[i] != itemSlot) continue;
                if (first < 0) first = i;
                if (!occupied(i)) return i;
            }
            return first;
        }
    }
}
