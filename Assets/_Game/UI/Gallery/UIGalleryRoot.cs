using Game.Core;
using Game.Data;
using Game.Services;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Dev scene root: builds a standalone UI context (in-memory save) and shows every widget.</summary>
    public sealed class UIGalleryRoot : MonoBehaviour
    {
        public UIManager Manager { get; private set; }
        public UIContext Context { get; private set; }

        private void Start() => Build();

        public void Build()
        {
            if (Manager != null) return;
            var config = ConfigDatabase.LoadFromResources();
            var clock = new ManualClock();
            var session = new GameSession(config.Database, NewGameFactory.Create(config.Database, clock), null, new SeededRandom(1), clock);
            Context = new UIContext
            {
                Session = session,
                Config = config,
                L = LocalizationLoader.Load("en"),
                Theme = Theme.Load(),
            };
            Manager = UIManager.Create(Context, transform);
            Manager.Push<UIGalleryScreen>();
        }
    }
}
