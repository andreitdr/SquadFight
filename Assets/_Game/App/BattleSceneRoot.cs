using System.Collections;
using Game.BattleView;
using Game.Services;
using Game.UI;
using UnityEngine;

namespace Game.App
{
    /// <summary>Battle scene entry: presents the current battle, records the outcome, then returns to Main.</summary>
    public sealed class BattleSceneRoot : MonoBehaviour, ISceneTransition
    {
        public UIManager Manager { get; private set; }
        public BattleScreen Screen { get; private set; }

        private void Start()
        {
            if (!AppContext.IsBooted) return;
            var services = AppContext.Services;
            var session = services.Get<GameSession>();
            var battle = session.Battles.Current;
            if (battle == null)
            {
                ReturnToMain(null);
                return;
            }
            var ctx = UIContextFactory.Create(services);
            Manager = UIManager.Create(ctx, transform);
            services.Get<SceneLoader>().Transition = this;
            ctx.Quit = () => { };
            var boss = battle.Setup.EnemyWaves.Exists(w => w.Exists(e => e.IsBoss));
            services.Get<Game.Core.IAudioService>().PlayMusic(boss ? "music_boss" : "music_battle");
            Screen = Manager.Push<BattleScreen>(new BattleScreenArgs { Battle = battle, Leave = retreat => Resolve(retreat) });
        }

        /// <summary>Records the battle (finishing remaining turns instantly) and goes back to Main.</summary>
        public void Resolve(bool retreat)
        {
            var session = AppContext.Services.Get<GameSession>();
            var result = retreat ? session.Battles.Retreat() : session.Battles.Finish();
            if (!result.Ok)
            {
                Debug.LogError("[Battle] could not record the battle: " + result);
                session.Battles.Discard();
            }
            ReturnToMain(result.Ok ? result.Value : null);
        }

        private static void ReturnToMain(FightCompletion completion)
        {
            AppContext.Services.Get<BattleHandoff>().Completion = completion;
            AppContext.Services.Get<SceneLoader>().Load(SceneNames.Main);
        }

        public IEnumerator FadeOut() => Manager != null ? Manager.FadeOut() : null;
        public IEnumerator FadeIn() => Manager != null ? Manager.FadeIn() : null;
    }
}
