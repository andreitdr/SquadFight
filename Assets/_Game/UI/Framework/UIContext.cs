using System;
using Game.Core;
using Game.Data;
using Game.Services;

namespace Game.UI
{
    /// <summary>Everything screens need, passed in by the composition root (no static game state).</summary>
    public sealed class UIContext
    {
        public GameSession Session;
        public ConfigDatabase Config;
        public ILocalizer L;
        public Theme Theme;
        public IHaptics Haptics = NullHaptics.Instance;
        public UIManager Manager;
        /// <summary>Plays a UI sound by id (audio library, step 14).</summary>
        public Action<string> PlaySfx = _ => { };
        /// <summary>Starts a fight: the App loads the Battle scene.</summary>
        public Action<string> StartBattle = _ => { };
        /// <summary>Called when the player wants to leave the app from the root screen.</summary>
        public Action Quit = () => { };
        public string AppVersion = "dev";
        /// <summary>Builds the offline bug-report text (Settings → Copy debug info).</summary>
        public Func<string> DebugInfo = () => "";
        /// <summary>Released when the UIManager is destroyed (scene change): long-lived view models, bus subscriptions.</summary>
        public readonly System.Collections.Generic.List<IDisposable> SceneDisposables = new System.Collections.Generic.List<IDisposable>();

        public string T(string key) => L != null ? L.Get(key) : key;
        public string F(string key, params object[] args) => L != null ? L.Format(key, args) : key;
        public string Plural(string key, long count) => L != null ? L.Plural(key, count) : key;
    }
}
