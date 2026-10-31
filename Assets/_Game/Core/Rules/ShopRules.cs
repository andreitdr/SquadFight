using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Gear shop (owner request 2026-10-31): weapons and armour of the best gear tier the player has reached, bought with
    /// the primary currency at a chosen rarity. Price = tier price × chosen rarity multiplier × template rarity multiplier.
    /// </summary>
    public static class ShopRules
    {
        public static float? RarityMultiplier(Rarity rarity, GameRules rules)
        {
            foreach (var r in rules.ShopRarityPrices) if (r.Rarity == rarity) return r.Multiplier;
            return null;
        }

        /// <summary>Rarities the shop sells, lowest first.</summary>
        public static List<Rarity> Rarities(GameRules rules) => rules.ShopRarityPrices.Select(r => r.Rarity).Distinct().OrderBy(r => r).ToList();

        /// <summary>Price of the template at a rarity; null when the shop does not sell it.</summary>
        public static long? Price(EquipmentTemplateDef t, Rarity rarity, GameRules rules)
        {
            if (!rules.ShopEnabled || t == null || t.Tier < 1 || t.Tier > rules.ShopTierPrices.Count || !rules.ShopSlots.Contains(t.Slot)) return null;
            var chosen = RarityMultiplier(rarity, rules);
            var quality = RarityMultiplier(t.Rarity, rules);
            if (chosen == null || quality == null) return null;
            return Math.Max(1L, (long)Math.Round((double)rules.ShopTierPrices[t.Tier - 1] * chosen.Value * quality.Value, MidpointRounding.AwayFromZero));
        }

        /// <summary>Templates on sale at a tier, in content order (rarity is chosen at purchase).</summary>
        public static List<EquipmentTemplateDef> Catalog(int tier, GameDatabase db) =>
            db.Equipment.Where(t => t != null && t.Tier == tier && Price(t, t.Rarity, db.Rules) != null).ToList();

        /// <summary>Highest gear tier a drop table can roll at random (0 = none named).</summary>
        public static int MaxTier(DropTableDef table)
        {
            var max = 0;
            if (table == null) return max;
            foreach (var e in table.Guaranteed.Concat(table.Groups.Where(g => g != null).SelectMany(g => g.Entries)))
            {
                if (e == null || e.Type != DropType.RandomEquipment || string.IsNullOrEmpty(e.RefId)) continue;
                foreach (var part in e.RefId.Split('+'))
                    if (int.TryParse(part.Trim(), out var tier) && tier > max) max = tier;
            }
            return max;
        }

        /// <summary>
        /// Shop tier: the highest tier named by the random-gear drops of any unlocked main fight (its drop and first-clear tables)
        /// or of its chapter's clear reward. At least 1.
        /// </summary>
        public static int CurrentTier(GameDatabase db, Func<string, bool> isFightUnlocked)
        {
            var tier = 1;
            foreach (var c in db.Chapters)
            {
                if (c == null) continue;
                var any = false;
                foreach (var id in c.FightIds)
                {
                    if (!db.TryGetFight(id, out var f) || !isFightUnlocked(id)) continue;
                    any = true;
                    tier = Math.Max(tier, Math.Max(TableTier(f.DropTableId, db), TableTier(f.FirstClearTableId, db)));
                }
                if (any) tier = Math.Max(tier, TableTier(c.ClearRewardTableId, db));
            }
            return tier;
        }

        private static int TableTier(string id, GameDatabase db) => db.TryGetDropTable(id, out var t) ? MaxTier(t) : 0;
    }
}
