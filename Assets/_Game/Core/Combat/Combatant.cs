using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class StatusEffect
    {
        public string StatusId;
        public StatType Stat;
        public float Value;
        public int Remaining;
        /// <summary>Applied during the holder's own turn: skip the end-of-turn tick once.</summary>
        public bool SkipNextTick;
    }

    /// <summary>Input to build one combatant (from a hero with gear/stars, or an enemy spawn).</summary>
    public sealed class CombatantSetup
    {
        public string DefId;
        public Team Team;
        public int Slot;
        public bool IsBoss;
        /// <summary>base × star + flat (before percent bonuses).</summary>
        public StatBlock PreStats = new StatBlock();
        /// <summary>Percent bonuses from equipment.</summary>
        public StatBlock PercentStats = new StatBlock();
        public SkillDef Basic;
        public SkillDef Active;
    }

    /// <summary>Live battle state of one unit.</summary>
    public sealed class Combatant
    {
        public int Index;
        public string DefId;
        public Team Team;
        public int Slot;
        public int Wave;
        public bool IsBoss;
        public int MaxHp;
        public int Hp;
        public int Shield;
        public int ShieldTurns;
        public bool ShieldSkipNextTick;
        public int StunTurns;
        public int ActiveCooldown;
        public SkillDef Basic;
        public SkillDef Active;
        public readonly List<StatusEffect> Statuses = new List<StatusEffect>();
        public readonly StatBlock PreStats;
        public readonly StatBlock PercentStats;
        public readonly CombatantStats Totals;
        private readonly CombatRules _rules;

        public Combatant(int index, CombatantSetup s, int wave, CombatRules rules)
        {
            Index = index;
            DefId = s.DefId;
            Team = s.Team;
            Slot = s.Slot;
            Wave = wave;
            IsBoss = s.IsBoss;
            Basic = s.Basic;
            Active = s.Active;
            PreStats = s.PreStats.Clone();
            PercentStats = s.PercentStats.Clone();
            _rules = rules;
            MaxHp = Math.Max(1, DamageCalculator.RoundToInt(Stat(StatType.MaxHp)));
            Hp = MaxHp;
            ActiveCooldown = Active?.InitialCooldown ?? 0;
            Totals = new CombatantStats { Index = index, DefId = DefId, Team = Team };
        }

        public bool Alive => Hp > 0;
        public float HpRatio => MaxHp <= 0 ? 0 : (float)Hp / MaxHp;
        public bool ActiveReady => Active != null && ActiveCooldown <= 0;

        public float BuffPercent(StatType stat)
        {
            var sum = 0f;
            foreach (var s in Statuses) if (s.Stat == stat) sum += s.Value;
            return sum;
        }

        /// <summary>Effective stat = (pre) × (1 + equipment% + buffs), clamped like out-of-combat stats.</summary>
        public float Stat(StatType stat)
        {
            var v = StatCalculator.Combine(PreStats[stat], PercentStats[stat] + BuffPercent(stat));
            switch (stat)
            {
                case StatType.CritChance: return Math.Min(v, _rules.CritChanceCap);
                case StatType.CritDamage: return Math.Max(1f, v);
                default: return v;
            }
        }

        public override string ToString() => $"#{Index} {DefId}({Team}{Slot}) {Hp}/{MaxHp}";
    }
}
