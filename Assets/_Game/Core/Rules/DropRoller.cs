using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>Rolls drop tables into concrete <see cref="RewardBundle"/>s. Deterministic for a given RNG state.</summary>
    public static class DropRoller
    {
        /// <summary>Rolls one table. <paramref name="onPick"/> (optional) sees every entry that was resolved (guaranteed or rolled).</summary>
        public static RewardBundle Roll(DropTableDef table, IRandom rng, GameDatabase db, Action<DropEntryDef> onPick = null)
        {
            var bundle = new RewardBundle();
            if (table == null) return bundle;
            foreach (var e in table.Guaranteed)
            {
                if (e == null) continue;
                onPick?.Invoke(e);
                Resolve(e, rng, db, bundle);
            }
            foreach (var g in table.Groups)
            {
                if (g == null) continue;
                var pool = g.Entries.Where(e => e != null && e.Weight > 0).ToList();
                var rolls = rng.NextInt(Math.Max(0, g.MinRolls), Math.Max(g.MinRolls, g.MaxRolls) + 1);
                for (var r = 0; r < rolls && pool.Count > 0; r++)
                {
                    var pick = PickWeighted(pool, e => e.Weight, rng);
                    if (g.WithoutReplacement) pool.Remove(pick);
                    onPick?.Invoke(pick);
                    Resolve(pick, rng, db, bundle);
                }
            }
            return bundle;
        }

        /// <summary>Cumulative-weight pick; entries with weight ≤ 0 are skipped. Returns default when nothing is pickable.</summary>
        public static T PickWeighted<T>(IReadOnlyList<T> items, Func<T, double> weight, IRandom rng)
        {
            double total = 0;
            foreach (var i in items) total += Math.Max(0, weight(i));
            if (total <= 0) return default;
            var roll = rng.NextDouble() * total;
            double acc = 0;
            T last = default;
            foreach (var i in items)
            {
                var w = Math.Max(0, weight(i));
                if (w <= 0) continue;
                acc += w;
                last = i;
                if (roll < acc) return i;
            }
            return last;
        }

        private static int Amount(DropEntryDef e, IRandom rng) => rng.NextInt(Math.Max(0, e.AmountMin), Math.Max(e.AmountMin, e.AmountMax) + 1);

        private static int Scale(int amount, float multiplier) => amount <= 0 ? 0 : Math.Max(1, (int)Math.Round(amount * Math.Max(0f, multiplier), MidpointRounding.AwayFromZero));

        private static void Resolve(DropEntryDef e, IRandom rng, GameDatabase db, RewardBundle bundle)
        {
            switch (e.Type)
            {
                case DropType.Currency:
                    bundle.AddCurrency(e.RefId, Scale(Amount(e, rng), db.Rules.CurrencyDropMultiplier));
                    break;
                case DropType.HeroShard:
                    bundle.AddShards(e.RefId, Scale(Amount(e, rng), db.Rules.ShardDropMultiplier));
                    break;
                case DropType.RandomHeroShardByRarity:
                {
                    var heroes = ShardCandidates(e, db);
                    if (heroes.Count == 0) break;
                    var hero = heroes[rng.NextInt(0, heroes.Count)];
                    bundle.AddShards(hero.Id, Scale(Amount(e, rng), db.Rules.ShardDropMultiplier));
                    break;
                }
                case DropType.Equipment:
                {
                    if (!db.TryGetEquipment(e.RefId, out var t)) break;
                    var n = Amount(e, rng);
                    for (var i = 0; i < n; i++) bundle.Items.Add(EquipmentFactory.Create(t, e.Rarity, rng, db));
                    break;
                }
                case DropType.RandomEquipment:
                {
                    var templates = db.Equipment.Where(t => e.SlotFilter == null || t.Slot == e.SlotFilter.Value).ToList();
                    if (templates.Count == 0) break;
                    var n = Amount(e, rng);
                    for (var i = 0; i < n; i++)
                    {
                        var rarity = PickWeighted(e.RarityWeights, w => w.Weight * (db.TryGetRarity(w.Rarity, out var rd) ? rd.DropWeightModifier : 1f), rng);
                        if (rarity.Weight <= 0) continue;
                        var t = templates[rng.NextInt(0, templates.Count)];
                        bundle.Items.Add(EquipmentFactory.Create(t, rarity.Rarity, rng, db));
                    }
                    break;
                }
            }
        }

        /// <summary>Expected number of times each entry is picked per roll of the table (rolls drawn uniformly in [min, max]; with replacement).</summary>
        public static Dictionary<DropEntryDef, double> ExpectedPicks(DropTableDef table)
        {
            var result = new Dictionary<DropEntryDef, double>();
            foreach (var e in table.Guaranteed.Where(e => e != null)) result[e] = 1;
            foreach (var g in table.Groups.Where(g => g != null))
            {
                var pool = g.Entries.Where(e => e != null && e.Weight > 0).ToList();
                double total = pool.Sum(e => (double)e.Weight);
                var avgRolls = (Math.Max(0, g.MinRolls) + Math.Max(g.MinRolls, g.MaxRolls)) / 2.0;
                foreach (var e in g.Entries.Where(e => e != null))
                    result[e] = e.Weight > 0 && total > 0 ? avgRolls * e.Weight / total : 0;
            }
            return result;
        }

        /// <summary>
        /// Heroes a <see cref="DropType.RandomHeroShardByRarity"/> entry can give: the entry's rarity, limited to the origin pool in
        /// <c>RefId</c> when set ("act_1+act_2" = heroes whose origin is one of those).
        /// </summary>
        public static List<HeroDef> ShardCandidates(DropEntryDef e, GameDatabase db)
        {
            var pool = string.IsNullOrEmpty(e.RefId) ? null : new HashSet<string>(e.RefId.Split('+').Select(x => x.Trim()).Where(x => x.Length > 0));
            return db.Heroes.Where(h => h != null && h.Rarity == e.Rarity && (pool == null || pool.Contains(h.Origin ?? ""))).ToList();
        }
    }
}
