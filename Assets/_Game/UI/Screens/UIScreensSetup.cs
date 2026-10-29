namespace Game.UI
{
    /// <summary>Entry point used by the App to show the first screen (step 12 adds chrome: top bar + tab bar).</summary>
    public static class UIScreensSetup
    {
        public static void ShowHome(UIContext ctx) => ctx.Manager.Replace<HomeScreen>(clearStack: true);
    }
}
