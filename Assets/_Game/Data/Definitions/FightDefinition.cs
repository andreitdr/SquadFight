using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Fight", fileName = "fight_new")]
    public sealed class FightDefinition : ContentDefinition
    {
        public string nameKey;
        public List<WaveData> waves = new List<WaveData>();
        [Min(0)] public int recommendedPower;
        public DropTable dropTable;
        public DropTable firstClearTable;
        [Tooltip("Rounds limit for the 3rd star (0 = global default).")]
        [Min(0)] public int starTurnLimit;
        public Sprite background;
        public List<StoryBeatDefinition> preBeats = new List<StoryBeatDefinition>();
        public List<StoryBeatDefinition> postBeats = new List<StoryBeatDefinition>();
        [Tooltip("Leave empty for the default rule (previous fight cleared).")]
        public List<UnlockConditionData> unlockConditions = new List<UnlockConditionData>();
        [Tooltip("Optional zone (lore, better items). List it under the chapter's side quests; it never counts toward completion.")]
        public bool isSideQuest;

        public FightDef ToCore() => new FightDef
        {
            Id = id, NameKey = nameKey, RecommendedPower = recommendedPower,
            DropTableId = dropTable ? dropTable.id : null, FirstClearTableId = firstClearTable ? firstClearTable.id : null,
            StarTurnLimit = starTurnLimit, BackgroundKey = background ? background.name : null,
            PreBeatIds = preBeats.Select(b => b ? b.id : null).ToList(),
            PostBeatIds = postBeats.Select(b => b ? b.id : null).ToList(),
            UnlockConditions = unlockConditions.Where(u => u != null).Select(u => u.ToCore()).ToList(),
            IsSideQuest = isSideQuest,
            Waves = waves.Where(w => w != null).Select(w => new WaveDef
            {
                Spawns = w.spawns.Where(s => s != null).Select(s => new EnemySpawnDef { EnemyId = s.enemy ? s.enemy.id : null, StatMultiplier = s.statMultiplier }).ToList(),
            }).ToList(),
        };
    }
}
