using Game.Core;

namespace Game.App
{
    /// <summary>
    /// Access point to the composition root built by <see cref="GameBootstrap"/> in the Boot scene.
    /// Holds no game state itself; scenes read services from <see cref="Services"/>.
    /// </summary>
    public static class AppContext
    {
        public static ServiceLocator Services { get; private set; }
        public static bool IsBooted => Services != null;

        internal static void Set(ServiceLocator services) => Services = services;

        /// <summary>Used by tests to tear down between runs.</summary>
        public static void Reset() => Services = null;
    }
}
