using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Skill", fileName = "skill_new")]
    public sealed class SkillDefinition : ContentDefinition
    {
        public string nameKey;
        public string descriptionKey;
        public TargetingRule targeting;
        [Min(1)] public int targetCount = 1;
        [Min(0)] public int cooldown;
        [Min(0)] public int initialCooldown;
        public List<EffectData> effects = new List<EffectData>();
        public Sprite icon;

        public SkillDef ToCore() => new SkillDef
        {
            Id = id, NameKey = nameKey, DescriptionKey = descriptionKey, Targeting = targeting, TargetCount = targetCount,
            Cooldown = cooldown, InitialCooldown = initialCooldown, Effects = effects.Where(e => e != null).Select(e => e.ToCore()).ToList(),
        };
    }
}
