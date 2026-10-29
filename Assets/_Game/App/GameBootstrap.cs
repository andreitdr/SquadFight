using Game.Core;
using Game.Data;
using Game.Save;
using Game.Services;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.App
{
    /// <summary>
    /// Composition root. Lives in the Boot scene, builds the <see cref="ServiceLocator"/>, then loads Main.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string firstScene = SceneNames.Main;

        /// <summary>Scene to open after boot instead of <see cref="firstScene"/> (set when a non-Boot scene is played directly in the Editor).</summary>
        internal static string PendingScene;

        private void Awake()
        {
            if (AppContext.IsBooted)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);

            var services = new ServiceLocator();
            var logger = new UnityLogger();
            services.Register<IGameLogger>(logger);
            services.Register<IClock>(new SystemClock());
            services.Register<IRandom>(SeededRandom.FromTime());
            services.Register(gameObject.AddComponent<SceneLoader>());

            var config = ConfigDatabase.LoadFromResources();
            if (config == null)
            {
                logger.Error($"[Boot] GameConfig not found in Resources/{GameConfig.ResourcePath}. Run Tools/Game/Content/Import CSV Content.");
                return;
            }
            services.Register(config);
            services.Register<IConfigProvider>(config);

            // Save + player state
            var clock = services.Get<IClock>();
            var saveManager = new SaveManager(new SaveStore(new FileSaveStorage(Application.persistentDataPath), clock), clock, logger);
            var db = config.Database;
            var load = saveManager.Load(() => NewGameFactory.Create(db, clock));
            foreach (var fix in SaveReconciler.Reconcile(load.Data, db)) logger.Warning("[Save] reconcile: " + fix);
            var session = new GameSession(db, load.Data, saveManager, services.Get<IRandom>(), clock, logger);
            saveManager.Bind(session.ExportForSave);
            if (load.Source == LoadSource.NewGame) saveManager.SaveNow();
            gameObject.AddComponent<SaveLifecycle>().Manager = saveManager;
            services.Register(saveManager);
            services.Register<ISaveScheduler>(saveManager);
            services.Register(session);
            services.Register(session.Bus);
            var localizer = LocalizationLoader.Load(session.State.Data.Settings.Language);
            services.Register<ILocalizer>(localizer);
            services.Register(localizer);
            AppContext.Set(services);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<DebugOverlay>();
            gameObject.AddComponent<DebugConsole>().Commands = new DebugCommands(session);
#endif
        }

        private void Start()
        {
            if (AppContext.Services == null) return;
            var target = string.IsNullOrEmpty(PendingScene) ? firstScene : PendingScene;
            PendingScene = null;
            if (SceneManager.GetActiveScene().name != target)
                AppContext.Services.Get<SceneLoader>().Load(target);
        }

        private void OnDestroy()
        {
            if (AppContext.Services != null && AppContext.Services.TryGet<SceneLoader>(out var loader) && loader != null && loader.gameObject == gameObject)
                AppContext.Reset();
        }
    }
}
