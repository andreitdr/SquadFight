using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Story Beat", fileName = "beat_new")]
    public sealed class StoryBeatDefinition : ContentDefinition
    {
        public string speakerKey;
        public string textKey;
        public Sprite portrait;

        public StoryBeatDef ToCore() => new StoryBeatDef { Id = id, SpeakerKey = speakerKey, TextKey = textKey };
    }
}
