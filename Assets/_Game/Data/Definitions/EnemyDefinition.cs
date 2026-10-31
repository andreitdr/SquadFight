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
        [Tooltip("Higher ranks have more HP (rank multiplier) and more skills, and give more player XP. Boss = top rank with special attacks.")]
        public EnemyRank rank = EnemyRank.Soldier;
        public bool isBoss => rank == EnemyRank.Boss;
        public StatTable baseStats = new StatTable();
        public SkillDefinition basicSkill;
        public SkillDefinition activeSkill;
        public SkillDefinition passiveSkill;
        [Tooltip("More active skills (Champions and Bosses), used after the active skill.")]
        public List<SkillDefinition> extraSkills = new List<SkillDefinition>();
        [Tooltip("Boss special attacks; each charges when HP drops to the skill's threshold.")]
        public List<SkillDefinition> specials = new List<SkillDefinition>();
        public Sprite sprite;
        public Color tint = Color.white;
        [Tooltip("Reuse another enemy's generated art (an elite variant), tinted with this enemy's tint.")]
        public string artFrom;
        /// <summary>Colour to draw generated art with: white, or a tint for variants that borrow another enemy's art.</summary>
        public Color ArtColor => string.IsNullOrEmpty(artFrom) ? Color.white : Color.Lerp(Color.white, tint, 0.55f);
        [Header("Art (Tools/Game/Art/Apply Generated Art fills empty slots)")]
        public Sprite portrait;
        public Sprite icon;
        public SpriteAnimSet animations;

        public EnemyDef ToCore() => new EnemyDef
        {
            Id = id, NameKey = nameKey, Rank = rank, BaseStats = baseStats.ToStatBlock(),
            BasicSkillId = basicSkill ? basicSkill.id : null, ActiveSkillId = activeSkill ? activeSkill.id : null,
            PassiveSkillId = passiveSkill ? passiveSkill.id : null,
            ExtraSkillIds = extraSkills.Where(s => s).Select(s => s.id).ToList(),
            SpecialSkillIds = specials.Where(s => s).Select(s => s.id).ToList(),
        };
    }
}
