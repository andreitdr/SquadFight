using System.Collections.Generic;

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
        /// <summary>Hero with stars, gear and gold-bought skill levels (active and passive scaled by their potency).</summary>
        public static CombatantSetup Hero(HeroDef hero, int star, EquipmentStats equipment, int slot, GameDatabase db, int activeLevel = 1, int passiveLevel = 1)
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
                Active = string.IsNullOrEmpty(hero.ActiveSkillId) ? null : SkillLevelRules.Scale(db.GetSkill(hero.ActiveSkillId), SkillLevelRules.Potency(activeLevel, db)),
                Passive = string.IsNullOrEmpty(hero.PassiveSkillId) ? null : SkillLevelRules.Scale(db.GetSkill(hero.PassiveSkillId), SkillLevelRules.Potency(passiveLevel, db)),
            };
        }

        public static CombatantSetup Enemy(EnemyDef enemy, float statMultiplier, int slot, GameDatabase db) => new CombatantSetup
        {
            DefId = enemy.Id,
            Team = Team.Enemy,
            Slot = slot,
            IsBoss = enemy.IsBoss,
            PreStats = ScaleCombatStats(enemy.BaseStats, statMultiplier),
            Basic = db.GetSkill(enemy.BasicSkillId),
            Active = string.IsNullOrEmpty(enemy.ActiveSkillId) ? null : db.GetSkill(enemy.ActiveSkillId),
            Passive = string.IsNullOrEmpty(enemy.PassiveSkillId) ? null : db.GetSkill(enemy.PassiveSkillId),
        };

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
