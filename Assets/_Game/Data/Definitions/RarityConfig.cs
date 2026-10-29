using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Config/Rarity Config", fileName = "RarityConfig")]
    public sealed class RarityConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public Rarity rarity;
            public Color color = Color.white;
            [Tooltip("Equipment stats are multiplied by this.")]
            public float equipmentStatMultiplier = 1f;
            [Tooltip("Hero shards needed to unlock a hero of this rarity.")]
            public int shardsToUnlock = 10;
            [Tooltip("Multiplies the star table shard costs for heroes of this rarity.")]
            public float starCostMultiplier = 1f;
            public float dropWeightModifier = 1f;
            [Tooltip("Primary currency received when selling an item of this rarity.")]
            public int itemSellValue;
            [Tooltip("Overflow currency per shard received beyond max star.")]
            public int overflowCurrencyPerShard;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Get(Rarity r) => entries.FirstOrDefault(e => e != null && e.rarity == r);

        public IEnumerable<RarityDef> ToCore() => entries.Where(e => e != null).Select(e => new RarityDef
        {
            Rarity = e.rarity, ColorHex = "#" + ColorUtility.ToHtmlStringRGB(e.color), EquipmentStatMultiplier = e.equipmentStatMultiplier,
            ShardsToUnlock = e.shardsToUnlock, StarCostMultiplier = e.starCostMultiplier, DropWeightModifier = e.dropWeightModifier,
            ItemSellValue = e.itemSellValue, OverflowCurrencyPerShard = e.overflowCurrencyPerShard,
        });
    }
}
