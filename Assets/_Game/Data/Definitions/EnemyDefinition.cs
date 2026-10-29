using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Enemy", fileName = "enemy_new")]
    public sealed class EnemyDefinition : ContentDefinition
    {
        public string nameKey;
        public bool isBoss;
        public StatTable baseStats = new StatTable();
        public SkillDefinition basicSkill;
        public SkillDefinition activeSkill;
        public Sprite sprite;
        public Color tint = Color.white;
        [Header("Art (Tools/Game/Art/Apply Generated Art fills empty slots)")]
        public Sprite portrait;
        public Sprite icon;
        public SpriteAnimSet animations;

        public EnemyDef ToCore() => new EnemyDef
        {
            Id = id, NameKey = nameKey, IsBoss = isBoss, BaseStats = baseStats.ToStatBlock(),
            BasicSkillId = basicSkill ? basicSkill.id : null, ActiveSkillId = activeSkill ? activeSkill.id : null,
        };
    }
}
