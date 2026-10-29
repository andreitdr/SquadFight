using System.Collections;
using Game.Core;
using Game.Data;
using Game.Services;
using Game.UI;
using UnityEngine;

namespace Game.App
{
    /// <summary>Main (menus) scene entry: builds the UI context from the booted services and shows Home.</summary>
    public sealed class MainSceneRoot : MonoBehaviour, ISceneTransition
    {
        public UIManager Manager { get; private set; }

        private void Start()
        {
            if (!AppContext.IsBooted) return; // SceneEntryGuard is loading Boot
            var services = AppContext.Services;
            var ctx = UIContextFactory.Create(services);
            Manager = UIManager.Create(ctx, transform);
            services.Get<SceneLoader>().Transition = this;
            UIScreensSetup.ShowHome(ctx);
            var completion = services.Get<BattleHandoff>().TakeCompletion();
            if (completion != null) Manager.Push<ResultScreen>(completion);
        }

        public IEnumerator FadeOut() => Manager != null ? Manager.FadeOut() : null;
        public IEnumerator FadeIn() => Manager != null ? Manager.FadeIn() : null;
    }

    /// <summary>Builds the <see cref="UIContext"/> from the composition root.</summary>
    public static class UIContextFactory
    {
        public static UIContext Create(ServiceLocator services)
        {
            var session = services.Get<GameSession>();
            var ctx = new UIContext
            {
                Session = session,
                Config = services.Get<ConfigDatabase>(),
                L = services.Get<ILocalizer>(),
                Theme = Theme.Load(),
                Haptics = services.TryGet<IHaptics>(out var h) ? h : NullHaptics.Instance,
                AppVersion = Application.version,
                Quit = Application.Quit,
            };
            ctx.StartBattle = fightId =>
            {
                var start = session.Battles.Start(fightId);
                if (!start.Ok)
                {
                    ctx.Manager.Toast(ctx.T(Game.ViewModels.ErrorKeys.For(start.Code)));
                    return;
                }
                services.Get<BattleHandoff>().FightId = fightId;
                services.Get<SceneLoader>().Load(SceneNames.Battle);
            };
            return ctx;
        }
    }
}
