using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    /// <summary>Enemy ranks (ranks.csv): HP multiplier, player XP and minimum skill count per rank.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Enemy Rank Config", fileName = "EnemyRankConfig")]
    public sealed class EnemyRankConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public EnemyRank rank;
            [Tooltip("Applied to the enemy's max HP in battle.")]
            public float hpMultiplier = 1f;
            [Tooltip("Player XP for defeating one enemy of this rank.")]
            public int xp;
            [Tooltip("Skills beyond the basic attack: at least this many, fewer than the next rank's minimum.")]
            public int minSkills;
        }

        public List<Entry> entries = new List<Entry>();

        public IEnumerable<RankDef> ToCore() => entries.Where(e => e != null).Select(e => new RankDef
        {
            Rank = e.rank, HpMultiplier = e.hpMultiplier, Xp = e.xp, MinSkills = e.minSkills,
        });
    }
}
