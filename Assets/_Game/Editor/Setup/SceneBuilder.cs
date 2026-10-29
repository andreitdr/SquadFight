using System.Collections.Generic;
using System.Linq;
using Game.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Generates the Boot/Main/Battle scenes from code so they are reproducible. Scenes are generated artifacts:
    /// re-running overwrites them (GUIDs are kept because the files are saved at the same path).
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenesFolder = "Assets/_Game/Scenes";
        public const string BootPath = ScenesFolder + "/Boot.unity";
        public const string MainPath = ScenesFolder + "/Main.unity";
        public const string BattlePath = ScenesFolder + "/Battle.unity";
        public const string GalleryPath = ScenesFolder + "/Dev/UIGallery.unity";
        private const string TemplateSample = "Assets/Scenes/SampleScene.unity";

        /// <summary>Extra builders registered by later steps (UI root, battle root) – keyed by scene path.</summary>
        public static readonly Dictionary<string, List<System.Action<Scene>>> Decorators = new Dictionary<string, List<System.Action<Scene>>>();

        [MenuItem("Tools/Game/Setup/Rebuild Scenes")]
        public static void BuildAllScenes()
        {
            MigrateTemplateSampleScene();
            BuildScene(BootPath, BuildBoot);
            BuildScene(MainPath, scene =>
            {
                BuildContentScene(scene, new Color(0.10f, 0.11f, 0.15f));
                new GameObject("MainSceneRoot").AddComponent<MainSceneRoot>();
            });
            BuildScene(BattlePath, scene => BuildContentScene(scene, new Color(0.08f, 0.09f, 0.12f)));
            System.IO.Directory.CreateDirectory(ScenesFolder + "/Dev");
            BuildScene(GalleryPath, scene =>
            {
                CreateCamera(new Color(0.08f, 0.09f, 0.13f));
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
                new GameObject("UIGalleryRoot").AddComponent<Game.UI.UIGalleryRoot>();
            });
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(MainPath, true),
                new EditorBuildSettingsScene(BattlePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] Boot, Main, Battle rebuilt and registered in Build Settings.");
        }

        /// <summary>The template's untouched SampleScene becomes Boot (plan step 01 task 0).</summary>
        private static void MigrateTemplateSampleScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootPath) != null) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplateSample) == null) return;
            var error = AssetDatabase.MoveAsset(TemplateSample, BootPath);
            if (!string.IsNullOrEmpty(error)) Debug.LogWarning("[SceneBuilder] Could not move SampleScene: " + error);
        }

        private static void BuildScene(string path, System.Action<Scene> build)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            build(scene);
            if (Decorators.TryGetValue(path, out var extra))
                foreach (var d in extra) d(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static Camera CreateCamera(Color background)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9.6f; // 1080x1920 reference at 100 px/unit
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.transform.position = new Vector3(0, 0, -10);
            go.AddComponent<AudioListener>();
            return cam;
        }

        private static void BuildBoot(Scene scene)
        {
            CreateCamera(Color.black);
            var boot = new GameObject("GameBootstrap");
            boot.AddComponent<GameBootstrap>();
        }

        private static void BuildContentScene(Scene scene, Color background)
        {
            var guard = new GameObject("SceneEntryGuard");
            guard.AddComponent<SceneEntryGuard>();
            CreateCamera(background);
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        public static IEnumerable<string> AllScenePaths => new[] { BootPath, MainPath, BattlePath };

        public static bool ScenesExist() => AllScenePaths.All(p => AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null);
    }
}
