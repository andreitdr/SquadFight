using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    /// <summary>Base for content assets with a stable string id.</summary>
    public abstract class ContentDefinition : ScriptableObject
    {
        [Tooltip("Stable lowercase snake_case id. Never change after release (saves reference it).")]
        public string id;
    }
}
