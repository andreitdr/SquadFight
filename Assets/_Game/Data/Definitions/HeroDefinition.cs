using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Hero", fileName = "hero_new")]
    public sealed class HeroDefinition : ContentDefinition
    {
        public string nameKey;
        public string loreKey;
        public Rarity rarity;
        public HeroClass heroClass;
        public StatTable baseStats = new StatTable();
        public SkillDefinition basicSkill;
        public SkillDefinition activeSkill;
        public Sprite portrait;
        [Tooltip("Tint applied to the (placeholder) portrait.")]
        public Color tint = Color.white;
        public Sprite shardIcon;
        [Header("Art (Tools/Game/Art/Apply Generated Art fills empty slots)")]
        [Tooltip("Round head-and-shoulders icon (party slots, turn order, dialogue).")]
        public Sprite icon;
        [Tooltip("Tall 3:4 bust shown inside hero cards.")]
        public Sprite cardArt;
        public SpriteAnimSet animations;

        public HeroDef ToCore() => new HeroDef
        {
            Id = id, NameKey = nameKey, LoreKey = loreKey, Rarity = rarity, Class = heroClass,
            BaseStats = baseStats.ToStatBlock(), BasicSkillId = basicSkill ? basicSkill.id : null, ActiveSkillId = activeSkill ? activeSkill.id : null,
        };
    }
}
