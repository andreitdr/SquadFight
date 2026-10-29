using System;

namespace Game.Core
{
    /// <summary>Breakdown of a hero's final stats for UI: final = (base × star + flat) × (1 + percent).</summary>
    public sealed class StatBreakdown
    {
        public StatBlock Base;
        public float StarMultiplier;
        public StatBlock EquipmentFlat;
        public StatBlock EquipmentPercent;
        public StatBlock Final;
    }

    public static class StatCalculator
    {
        /// <summary>final = (heroBase × starMultiplier + equipmentFlat) × (1 + equipmentPercent). Buffs are applied in combat only.</summary>
        public static StatBlock Compute(HeroDef hero, int star, EquipmentStats equipment, GameDatabase db) =>
            Compute(hero.BaseStats, HeroProgressionRules.StarMultiplier(star, db), equipment, db.Combat);

        public static StatBlock Compute(StatBlock baseStats, float starMultiplier, EquipmentStats equipment, CombatRules combat)
        {
            var result = new StatBlock();
            foreach (var s in StatBlock.All)
            {
                var flat = equipment?.Flat[s] ?? 0f;
                var pct = equipment?.Percent[s] ?? 0f;
                result[s] = Combine(baseStats[s] * starMultiplier + flat, pct);
            }
            return Clamp(result, combat);
        }

        public static StatBreakdown Breakdown(HeroDef hero, int star, EquipmentStats equipment, GameDatabase db)
        {
            var mult = HeroProgressionRules.StarMultiplier(star, db);
            return new StatBreakdown
            {
                Base = hero.BaseStats.Clone(),
                StarMultiplier = mult,
                EquipmentFlat = equipment?.Flat.Clone() ?? new StatBlock(),
                EquipmentPercent = equipment?.Percent.Clone() ?? new StatBlock(),
                Final = Compute(hero.BaseStats, mult, equipment, db.Combat),
            };
        }

        /// <summary>pre × (1 + percent), never negative.</summary>
        public static float Combine(float pre, float percent) => Math.Max(0f, pre * (1f + percent));

        /// <summary>No negative stats; crit chance within [0, cap]; crit damage at least ×1.</summary>
        public static StatBlock Clamp(StatBlock s, CombatRules combat)
        {
            foreach (var t in StatBlock.All) if (s[t] < 0 || float.IsNaN(s[t])) s[t] = 0;
            s[StatType.CritChance] = Math.Min(s[StatType.CritChance], combat?.CritChanceCap ?? 1f);
            if (s[StatType.CritDamage] < 1f) s[StatType.CritDamage] = 1f;
            return s;
        }
    }

    public static class DamageCalculator
    {
        /// <summary>100 / (100 + defense × defK): fraction of damage that gets through.</summary>
        public static double Mitigation(float defense, CombatRules c) => 100.0 / (100.0 + Math.Max(0f, defense) * c.DefenseK);

        /// <summary>Damage before crit: max(minDamage, attack × power × mitigation).</summary>
        public static double BaseDamage(float attack, float power, float defense, CombatRules c) =>
            Math.Max(c.MinDamage, Math.Max(0f, attack) * power * Mitigation(defense, c));

        public static bool RollCrit(float critChance, CombatRules c, IRandom rng)
        {
            var chance = Math.Min(Math.Max(0f, critChance), c.CritChanceCap);
            if (chance <= 0) return false;
            if (chance >= 1) return true;
            return rng.NextDouble() < chance;
        }

        /// <summary>Final integer damage (crit rolled with <paramref name="rng"/>).</summary>
        public static int Damage(float attack, float power, float defense, float critChance, float critDamage, CombatRules c, IRandom rng, out bool crit)
        {
            crit = RollCrit(critChance, c, rng);
            var dmg = BaseDamage(attack, power, defense, c) * (crit ? Math.Max(1f, critDamage) : 1f);
            return Math.Max(0, RoundToInt(dmg));
        }

        public static int Heal(float attack, float power) => Math.Max(0, RoundToInt(Math.Max(0f, attack) * power));

        public static int RoundToInt(double v) => (int)Math.Min(int.MaxValue, Math.Round(v, MidpointRounding.AwayFromZero));
    }

    public static class PowerRating
    {
        /// <summary>Σ stat × weight (weights in CombatConfig), rounded.</summary>
        public static int Compute(StatBlock stats, CombatRules c)
        {
            double sum = 0;
            foreach (var s in StatBlock.All) sum += stats[s] * c.PowerWeights[s];
            return DamageCalculator.RoundToInt(sum);
        }
    }
}
