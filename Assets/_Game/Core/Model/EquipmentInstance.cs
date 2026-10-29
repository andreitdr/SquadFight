using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>An item instance before/independent of ownership: template + rarity + optional rolled stats.</summary>
    public sealed class EquipmentInstance
    {
        public string InstanceId;
        public string TemplateId;
        public Rarity Rarity;
        /// <summary>Final stats rolled at drop time (roll variation on); null = template × rarity multiplier.</summary>
        public List<StatModifier> RolledStats;

        public override string ToString() => $"{TemplateId} ({Rarity}) #{InstanceId}";
    }
}
