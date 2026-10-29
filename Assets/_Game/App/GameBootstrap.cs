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

        /// <summary>Tests point the save folder somewhere temporary so they never touch the player's save.</summary>
        public static string SaveFolderOverride;

        private void Awake()
        {
            if (AppContext.IsBooted)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);
            QualityTiers.Apply();
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            var services = new ServiceLocator();
            var logCapture = LogCapture.Install(gameObject, SaveFolderOverride ?? Application.persistentDataPath);
            services.Register(logCapture);
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
            var saveManager = new SaveManager(new SaveStore(new FileSaveStorage(SaveFolderOverride ?? Application.persistentDataPath), clock), clock, logger);
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
            services.Register(new BattleHandoff());

            // Audio + haptics, driven by settings.
            var library = Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);
            var audio = AudioService.Create(gameObject, library);
            audio.SetMusicVolume(session.Settings.MusicVolume);
            audio.SetSfxVolume(session.Settings.SfxVolume);
            services.Register<IAudioService>(audio);
            services.Register<IHaptics>(new MobileHaptics(() => session.Settings.Vibration));
            session.Bus.Subscribe<SettingsChanged>(_ =>
            {
                audio.SetMusicVolume(session.Settings.MusicVolume);
                audio.SetSfxVolume(session.Settings.SfxVolume);
                Tween.ReduceMotion = session.Settings.ReduceMotion;
            });
            ButtonFeedback.OnPressed = () => audio.PlaySfx("ui_click");
            Tween.ReduceMotion = session.State.Data.Settings.ReduceMotion;
            AppContext.Set(services);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<DebugOverlay>();
            var commands = new DebugCommands(session);
            // "battle <fightId> [seed]": play a fight in the Battle scene (replay a specific seed for debugging).
            commands.Register("battle", a =>
            {
                if (a.Length < 1) return "Usage: battle <fightId> [seed]";
                var seed = a.Length > 1 ? (ulong?)ulong.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture) : null;
                var start = session.Battles.Start(a[0], seed);
                if (!start.Ok) return "Failed: " + start;
                AppContext.Services.Get<SceneLoader>().Load(SceneNames.Battle);
                return $"Battle {a[0]} seed {start.Value.Seed}";
            });
            gameObject.AddComponent<DebugConsole>().Commands = commands;
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
