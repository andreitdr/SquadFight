using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>Sound ids → clips (config, not code). Lives in Resources/AudioLibrary.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Audio Library", fileName = "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        public const string ResourcePath = "AudioLibrary";

        [Serializable]
        public sealed class Entry
        {
            public string id;
            public List<AudioClip> clips = new List<AudioClip>();
            [Range(0, 1)] public float volume = 1f;
            [Range(0, 0.3f)] public float pitchVariance;
            [Tooltip("Minimum seconds between two plays of this id (prevents stacking).")]
            public float cooldown = 0.03f;
        }

        public List<Entry> sfx = new List<Entry>();
        public List<Entry> music = new List<Entry>();

        private Dictionary<string, Entry> _sfx, _music;

        public Entry Sfx(string id) => Lookup(ref _sfx, sfx, id);
        public Entry Music(string id) => Lookup(ref _music, music, id);

        private static Entry Lookup(ref Dictionary<string, Entry> cache, List<Entry> list, string id)
        {
            if (cache == null)
            {
                cache = new Dictionary<string, Entry>();
                foreach (var e in list) if (e != null && !string.IsNullOrEmpty(e.id)) cache[e.id] = e;
            }
            return id != null && cache.TryGetValue(id, out var r) ? r : null;
        }
    }
}
