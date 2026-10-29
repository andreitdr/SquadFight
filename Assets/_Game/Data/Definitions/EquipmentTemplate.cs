using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Equipment Template", fileName = "eq_new")]
    public sealed class EquipmentTemplate : ContentDefinition
    {
        public string nameKey;
        public EquipmentSlot slot;
        public Rarity rarity;
        public List<StatModifierData> stats = new List<StatModifierData>();
        public Sprite icon;
        [Tooltip("Reserved for item sets (v1.1).")]
        public string setId;

        public EquipmentTemplateDef ToCore() => new EquipmentTemplateDef
        {
            Id = id, NameKey = nameKey, Slot = slot, Rarity = rarity, SetId = setId, Stats = stats.Select(s => s.ToCore()).ToList(),
        };
    }
}
