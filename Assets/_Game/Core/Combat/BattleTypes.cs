using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Core
{
    public enum Team { Player, Enemy }

    public enum BattleOutcome { None, Victory, Defeat, Timeout }

    public enum BattleEventType
    {
        BattleStarted, WaveStarted, RoundStarted, TurnStarted, ActionChosen, Stunned,
        DamageDealt, HealApplied, ShieldApplied, StatusApplied, StatusExpired, ShieldExpired,
        CombatantDied, WaveCleared, BattleEnded,
        // Appended (step "The Hundred"): keep order, hashes and the presentation switch rely on it.
        DotTick, HotTick, Dodged, Revived, Cleansed, Dispelled, Silenced, PassiveTriggered, CooldownChanged, ThornsReflected,
        // Appended (enemy ranks): a boss's HP crossed a special's threshold; it fires on the boss's next turn.
        SpecialCharged,
    }

    /// <summary>
    /// One flat event record (cheap to hash/compare, easy to play back). Combatants are referenced by <see cref="Combatant.Index"/>.
    /// Unused fields keep their defaults.
    /// </summary>
    public sealed class BattleEvent
    {
        public BattleEventType Type;
        public int Round;
        public int Wave;
        public int Actor = -1;
        public int Target = -1;
        public int Amount;
        public int Absorbed;
        public int HpAfter;
        public bool Crit;
        public string SkillId;
        public string StatusId;
        public StatType Stat;
        public float Value;
        public int Duration;
        public int Stacks;
        public BattleOutcome Outcome;
        /// <summary>ActionChosen: chosen target indexes.</summary>
        public int[] Targets;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Round).Append(' ').Append(Type);
            if (Actor >= 0) sb.Append(" a=").Append(Actor);
            if (Target >= 0) sb.Append(" t=").Append(Target);
            if (Amount != 0) sb.Append(" n=").Append(Amount);
            if (Absorbed != 0) sb.Append(" abs=").Append(Absorbed);
            if (Type == BattleEventType.DamageDealt || Type == BattleEventType.HealApplied) sb.Append(" hp=").Append(HpAfter);
            if (Crit) sb.Append(" crit");
            if (SkillId != null) sb.Append(" s=").Append(SkillId);
            if (StatusId != null) sb.Append(" st=").Append(StatusId).Append(':').Append(Stat).Append(':').Append(Value.ToString("0.###", CultureInfo.InvariantCulture)).Append(':').Append(Duration).Append('x').Append(Stacks);
            if (Type == BattleEventType.WaveStarted || Type == BattleEventType.WaveCleared) sb.Append(" w=").Append(Wave);
            if (Outcome != BattleOutcome.None) sb.Append(' ').Append(Outcome);
            if (Targets != null) sb.Append(" ->").Append(string.Join(",", Targets));
            return sb.ToString();
        }
    }

    public sealed class CombatantStats
    {
        public int Index;
        public string DefId;
        public Team Team;
        public long DamageDealt;
        public long DamageTaken;
        public long HealingDone;
        public bool Survived;
    }

    public sealed class BattleResult
    {
        public BattleOutcome Outcome;
        public int Rounds;
        public int Actions;
        public int PlayerDeaths;
        public readonly List<string> Survivors = new List<string>();
        public readonly List<CombatantStats> Combatants = new List<CombatantStats>();
        public bool Victory => Outcome == BattleOutcome.Victory;
    }
}
