using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    /// <summary>Player levels (levels.csv): XP to the next level and the reward for reaching each level.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Player Level Config", fileName = "PlayerLevelConfig")]
    public sealed class PlayerLevelConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Level
        {
            public int level = 1;
            [Tooltip("XP from this level to the next (ignored on the last level).")]
            public int xpToNext;
            [Tooltip("Rolled once when the player reaches this level.")]
            public DropTable reward;
        }

        public List<Level> levels = new List<Level>();

        public IEnumerable<PlayerLevelDef> ToCore() => levels.Where(l => l != null).Select(l => new PlayerLevelDef
        {
            Level = l.level, XpToNext = l.xpToNext, RewardTableId = l.reward ? l.reward.id : null,
        });
    }
}
