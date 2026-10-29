using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Chapter", fileName = "chapter_new")]
    public sealed class ChapterDefinition : ContentDefinition
    {
        public string nameKey;
        [Tooltip("Ordered fights. Reorder here to change campaign order.")]
        public List<FightDefinition> fights = new List<FightDefinition>();
        [Tooltip("Leave empty for the default rule (previous chapter cleared).")]
        public List<UnlockConditionData> unlockConditions = new List<UnlockConditionData>();
        public DropTable clearReward;
        [Header("Art")]
        [Tooltip("Portrait battle background for this chapter's fights.")]
        public Sprite background;
        [Tooltip("Wide banner shown on the campaign screen.")]
        public Sprite banner;

        public ChapterDef ToCore() => new ChapterDef
        {
            Id = id, NameKey = nameKey, FightIds = fights.Select(f => f ? f.id : null).ToList(),
            UnlockConditions = unlockConditions.Where(u => u != null).Select(u => u.ToCore()).ToList(),
            ClearRewardTableId = clearReward ? clearReward.id : null,
        };
    }
}
