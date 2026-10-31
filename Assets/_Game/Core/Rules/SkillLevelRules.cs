using System;

namespace Game.Core
{
    /// <summary>
    /// Gold-bought skill levels (owner request 2026-10-31). A level multiplies the skill's magnitudes by its potency:
    /// damage, healing, shields, DoT/HoT ticks, stat changes, thorns, marks, %HP damage and revive HP. Durations, cooldowns,
    /// targets and chances (proc, stun, dodge) stay as authored, so a levelled skill hits harder but never becomes a sure thing.
    /// </summary>
    public static class SkillLevelRules
    {
        public static bool IsMaxLevel(int level, GameDatabase db) => level >= db.MaxSkillLevel;

        /// <summary>Gold to go from <paramref name="level"/> to the next level; null at max level.</summary>
        public static int? NextCost(int level, GameDatabase db)
        {
            if (IsMaxLevel(level, db)) return null;
            var next = db.GetSkillLevel(Math.Max(1, level) + 1);
            return next == null ? (int?)null : Math.Max(0, next.GoldCost);
        }

        /// <summary>Potency at a level (1 for level 1, unknown levels or no table).</summary>
        public static float Potency(int level, GameDatabase db)
        {
            if (level <= 1) return 1f;
            return db.GetSkillLevel(Math.Min(level, db.MaxSkillLevel))?.Potency ?? 1f;
        }

        /// <summary>A copy of the skill with its magnitudes scaled; the same instance when potency is 1 (or the skill is null).</summary>
        public static SkillDef Scale(SkillDef s, float potency)
        {
            if (s == null || Math.Abs(potency - 1f) < 1e-6f) return s;
            var c = new SkillDef
            {
                Id = s.Id, NameKey = s.NameKey, DescriptionKey = s.DescriptionKey, Targeting = s.Targeting, TargetCount = s.TargetCount,
                Cooldown = s.Cooldown, InitialCooldown = s.InitialCooldown, Trigger = s.Trigger, Threshold = s.Threshold, Chance = s.Chance,
                OncePerBattle = s.OncePerBattle,
            };
            foreach (var e in s.Effects) c.Effects.Add(e == null ? null : Scale(e, potency));
            return c;
        }

        public static EffectDef Scale(EffectDef e, float p)
        {
            var c = new EffectDef
            {
                Type = e.Type, Target = e.Target, Power = e.Power, Stat = e.Stat, Value = e.Value, Value2 = e.Value2, Duration = e.Duration,
                Stacking = e.Stacking, MaxStacks = e.MaxStacks, StatusId = e.StatusId, Chance = e.Chance,
            };
            switch (e.Type)
            {
                case EffectType.Damage:
                case EffectType.Heal:
                case EffectType.Shield:
                case EffectType.DamageOverTime:
                case EffectType.HealOverTime:
                case EffectType.Drain:
                case EffectType.Execute:
                    c.Power *= p;
                    break;
                case EffectType.ApplyStatus:
                case EffectType.Thorns:
                case EffectType.Mark:
                    c.Value *= p;
                    break;
                case EffectType.PercentHpDamage:
                    c.Value *= p;
                    c.Value2 *= p;
                    break;
                case EffectType.Revive:
                    c.Value = Math.Min(1f, c.Value * p); // never above full HP
                    break;
            }
            return c;
        }
    }
}
