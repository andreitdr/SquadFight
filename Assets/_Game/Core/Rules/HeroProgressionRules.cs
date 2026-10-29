using System;

namespace Game.Core
{
    public readonly struct StarUpCost
    {
        public readonly int Shards;
        public readonly int Currency;
        public StarUpCost(int shards, int currency) { Shards = shards; Currency = currency; }
    }

    /// <summary>Stateless shard/unlock/star rules (D1: shards consumed on unlock; D2: overflow beyond max star → currency).</summary>
    public static class HeroProgressionRules
    {
        public static int ShardsToUnlock(HeroDef hero, GameDatabase db) => db.GetRarity(hero.Rarity).ShardsToUnlock;

        public static bool CanUnlock(bool unlocked, int shards, int needed) => !unlocked && needed > 0 && shards >= needed;

        public static bool IsMaxStar(int star, GameDatabase db) => star >= db.MaxStar;

        /// <summary>Cost to go from <paramref name="currentStar"/> to the next star; null at max star.</summary>
        public static StarUpCost? NextStarCost(HeroDef hero, int currentStar, GameDatabase db)
        {
            if (currentStar < 1 || IsMaxStar(currentStar, db)) return null;
            var level = db.GetStarLevel(currentStar + 1);
            if (level == null) return null;
            var rarity = db.GetRarity(hero.Rarity);
            var shards = (int)Math.Ceiling(level.ShardsRequired * rarity.StarCostMultiplier - 1e-4);
            return new StarUpCost(Math.Max(1, shards), Math.Max(0, level.CurrencyCost));
        }

        /// <summary>Stat multiplier for a star level (1 for locked/unknown).</summary>
        public static float StarMultiplier(int star, GameDatabase db)
        {
            if (star < 1) return 1f;
            var level = db.GetStarLevel(Math.Min(star, db.MaxStar));
            return level?.StatMultiplier ?? 1f;
        }

        /// <summary>Currency granted for shards that cannot be used (hero at max star).</summary>
        public static long OverflowValue(HeroDef hero, int shards, GameDatabase db) =>
            shards <= 0 ? 0 : (long)shards * db.GetRarity(hero.Rarity).OverflowCurrencyPerShard;

        /// <summary>Total shards from locked → max star (for UI/tools).</summary>
        public static int TotalShardsToMax(HeroDef hero, GameDatabase db)
        {
            var total = ShardsToUnlock(hero, db);
            for (var s = 1; s < db.MaxStar; s++) total += NextStarCost(hero, s, db)?.Shards ?? 0;
            return total;
        }
    }
}
