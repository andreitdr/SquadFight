using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Config/Star Upgrade Config", fileName = "StarUpgradeConfig")]
    public sealed class StarUpgradeConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Level
        {
            public int star = 1;
            [Tooltip("Shards to reach this star from the previous one (before rarity multiplier).")]
            public int shardsRequired;
            [Tooltip("Primary currency to reach this star.")]
            public int currencyCost;
            public float statMultiplier = 1f;
        }

        public List<Level> levels = new List<Level>();

        public IEnumerable<StarLevelDef> ToCore() => levels.Where(l => l != null).Select(l => new StarLevelDef
        {
            Star = l.star, ShardsRequired = l.shardsRequired, CurrencyCost = l.currencyCost, StatMultiplier = l.statMultiplier,
        });
    }
}
