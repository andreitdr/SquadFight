using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Player level (account XP). Every defeated enemy is worth its rank's XP (bosses the most); XP fills levels.csv and
    /// each level reached rolls that level's reward table. Pure rules; the state lives in the save.
    /// </summary>
    public static class PlayerLevelRules
    {
        /// <summary>XP earned in a battle: the rank XP of every enemy that was defeated (on a defeat or retreat too).</summary>
        public static int XpForBattle(BattleResult result, GameDatabase db)
        {
            if (result == null) return 0;
            var xp = 0;
            foreach (var c in result.Combatants)
                if (c.Team == Team.Enemy && !c.Survived && db.TryGetEnemy(c.DefId, out var e)) xp += db.GetRank(e.Rank).Xp;
            return xp;
        }

        /// <summary>XP for clearing the whole fight (every enemy of every wave), for the fight preview.</summary>
        public static int XpForFight(FightDef fight, GameDatabase db) =>
            fight == null ? 0 : fight.Waves.SelectMany(w => w.Spawns).Sum(s => db.TryGetEnemy(s.EnemyId, out var e) ? db.GetRank(e.Rank).Xp : 0);

        /// <summary>XP needed from <paramref name="level"/> to the next one; 0 at the cap.</summary>
        public static int XpToNext(int level, GameDatabase db) => level >= db.MaxPlayerLevel ? 0 : db.GetPlayerLevel(level)?.XpToNext ?? 0;

        /// <summary>
        /// Adds XP to (level, xp into that level). Returns every level reached, in order. At the cap the level stops and
        /// extra XP is not kept (the bar shows full).
        /// </summary>
        public static List<int> AddXp(ref int level, ref int xp, int gained, GameDatabase db)
        {
            var reached = new List<int>();
            if (level < 1) level = 1;
            if (gained <= 0 || level >= db.MaxPlayerLevel) { if (level >= db.MaxPlayerLevel) xp = 0; return reached; }
            xp += gained;
            while (level < db.MaxPlayerLevel)
            {
                var need = XpToNext(level, db);
                if (need <= 0 || xp < need) break;
                xp -= need;
                level++;
                reached.Add(level);
            }
            if (level >= db.MaxPlayerLevel) xp = 0;
            return reached;
        }
    }
}
