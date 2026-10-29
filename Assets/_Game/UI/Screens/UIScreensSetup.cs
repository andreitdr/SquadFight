using Game.ViewModels;

namespace Game.UI
{
    /// <summary>Builds the chrome (top bar, tab bar, notifications) and shows the first screen.</summary>
    public static class UIScreensSetup
    {
        public static NotificationsModel Dots { get; private set; }

        /// <summary>(Re)builds chrome and shows Home. Safe to call again (reset, language change).</summary>
        public static void ShowHome(UIContext ctx)
        {
            ctx.Manager.CloseAllPopups();
            UIKit.Clear(ctx.Manager.TopBarHost);
            UIKit.Clear(ctx.Manager.TabBarHost);
            if (Dots != null)
            {
                ctx.SceneDisposables.Remove(Dots);
                Dots.Dispose();
            }
            Dots = new NotificationsModel(ctx.Session);
            ctx.SceneDisposables.Add(Dots);
            TopBar.Create(ctx);
            TabBar.Create(ctx, Dots);
            ctx.Manager.Replace<HomeScreen>(clearStack: true);
            ctx.Manager.RefreshChrome();
        }
    }
}
