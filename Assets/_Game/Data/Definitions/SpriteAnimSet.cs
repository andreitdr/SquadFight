using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Frame-by-frame animation clips for one battle unit (idle, attack, skill, hit, death, victory).
    /// Swap art by replacing the frame PNGs, by pointing clips at other sprites, or by assigning another set.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Art/Sprite Anim Set", fileName = "anim_new")]
    public sealed class SpriteAnimSet : ScriptableObject
    {
        public const string Idle = "idle", Attack = "attack", Skill = "skill", Hit = "hit", Death = "death", Victory = "victory";

        [Serializable]
        public sealed class Clip
        {
            public string name;
            public float fps = 10;
            public bool loop;
            public Sprite[] frames = new Sprite[0];
            public float Duration => frames.Length / Mathf.Max(1f, fps);
        }

        [Tooltip("Frame size divided by the character's on-screen body size (the frame has padding for weapons and FX).")]
        public float displayScale = 1.6f;
        [Tooltip("Where the feet sit inside the frame, 0 = bottom, 1 = top.")]
        public float feetY = 0.17f;
        public List<Clip> clips = new List<Clip>();

        public Clip Get(string clipName)
        {
            foreach (var c in clips)
                if (c != null && c.name == clipName && c.frames.Length > 0) return c;
            return null;
        }

        public Sprite FirstFrame => Get(Idle)?.frames[0];
    }
}
