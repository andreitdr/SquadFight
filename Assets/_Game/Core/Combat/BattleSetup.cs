using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>Everything a battle needs: player units and enemy waves (already resolved from content).</summary>
    public sealed class BattleSetup
    {
        public readonly List<CombatantSetup> Players = new List<CombatantSetup>();
        public readonly List<List<CombatantSetup>> EnemyWaves = new List<List<CombatantSetup>>();
        public string FightId;
        public CombatRules Rules;
    }

    /// <summary>Builds combatant setups from content definitions (used by services, simulators and tests).</summary>
    public static class BattleSetupBuilder
    {
        public static CombatantSetup Hero(HeroDef hero, int star, EquipmentStats equipment, int slot, GameDatabase db)
        {
            var mult = HeroProgressionRules.StarMultiplier(star, db);
            var pre = new StatBlock();
            foreach (var s in StatBlock.All) pre[s] = hero.BaseStats[s] * mult + (equipment?.Flat[s] ?? 0f);
            return new CombatantSetup
            {
                DefId = hero.Id,
                Team = Team.Player,
                Slot = slot,
                PreStats = pre,
                PercentStats = equipment?.Percent.Clone() ?? new StatBlock(),
                Basic = db.GetSkill(hero.BasicSkillId),
                Active = string.IsNullOrEmpty(hero.ActiveSkillId) ? null : db.GetSkill(hero.ActiveSkillId),
                Passive = string.IsNullOrEmpty(hero.PassiveSkillId) ? null : db.GetSkill(hero.PassiveSkillId),
            };
        }

        public static CombatantSetup Enemy(EnemyDef enemy, float statMultiplier, int slot, GameDatabase db) => new CombatantSetup
        {
            DefId = enemy.Id,
            Team = Team.Enemy,
            Slot = slot,
            IsBoss = enemy.IsBoss,
            Rank = enemy.Rank,
            PreStats = EnemyStats(enemy, statMultiplier, db),
            Basic = db.GetSkill(enemy.BasicSkillId),
            Active = string.IsNullOrEmpty(enemy.ActiveSkillId) ? null : db.GetSkill(enemy.ActiveSkillId),
            Passive = string.IsNullOrEmpty(enemy.PassiveSkillId) ? null : db.GetSkill(enemy.PassiveSkillId),
            Extras = enemy.ExtraSkillIds.Select(db.GetSkill).ToList(),
            Specials = enemy.SpecialSkillIds.Select(db.GetSkill).ToList(),
        };

        /// <summary>An enemy's stats in a fight: base scaled by the spawn multiplier, then max HP by its rank.</summary>
        public static StatBlock EnemyStats(EnemyDef enemy, float statMultiplier, GameDatabase db)
        {
            var s = ScaleCombatStats(enemy.BaseStats, statMultiplier);
            s[StatType.MaxHp] *= db.GetRank(enemy.Rank).HpMultiplier;
            return s;
        }

        /// <summary>Scales HP/Attack/Defense; speed and crit stats stay unchanged (turn order would otherwise drift with difficulty).</summary>
        public static StatBlock ScaleCombatStats(StatBlock s, float multiplier)
        {
            var r = s.Clone();
            r[StatType.MaxHp] *= multiplier;
            r[StatType.Attack] *= multiplier;
            r[StatType.Defense] *= multiplier;
            return r;
        }

        public static void AddFightWaves(BattleSetup setup, FightDef fight, GameDatabase db)
        {
            setup.FightId = fight.Id;
            foreach (var wave in fight.Waves)
            {
                var list = new List<CombatantSetup>();
                for (var i = 0; i < wave.Spawns.Count; i++)
                    list.Add(Enemy(db.GetEnemy(wave.Spawns[i].EnemyId), wave.Spawns[i].StatMultiplier, i, db));
                setup.EnemyWaves.Add(list);
            }
        }
    }
}
