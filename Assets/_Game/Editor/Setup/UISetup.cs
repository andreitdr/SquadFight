using System.IO;
using System.Linq;
using Game.EditorTools.Content;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Imports TextMeshPro essentials (once) and creates/updates Resources/Theme.asset from placeholder art.</summary>
    public static class UISetup
    {
        public const string ThemePath = "Assets/_Game/Resources/Theme.asset";
        public const string TmpFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        [MenuItem("Tools/Game/Setup/Import TMP Essentials + Build Theme")]
        public static void Run()
        {
            ImportTmpEssentials();
            BuildTheme();
        }

        public static void ImportTmpEssentials()
        {
            if (File.Exists(TmpFontPath)) return;
            var package = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories)
                .FirstOrDefault(p => p.Replace('\\', '/').Contains("com.unity.ugui"));
            if (package == null)
            {
                Debug.LogError("[UISetup] TMP Essential Resources.unitypackage not found in the ugui package.");
                return;
            }
            // Package import is asynchronous in batch mode; tools/extract_unitypackage.py does the same synchronously.
            UnityEditor.AssetPackage.Package.Import(package, false);
            Debug.Log("[UISetup] Imported " + package);
        }

        public static Theme BuildTheme()
        {
            PlaceholderArt.EnsureAll();
            var theme = AssetDatabase.LoadAssetAtPath<Theme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<Theme>();
                Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            theme.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            theme.rounded = PlaceholderArt.Get("rounded");
            theme.pixel = PlaceholderArt.Get("pixel");
            theme.circle = PlaceholderArt.Get("circle");
            theme.ring = PlaceholderArt.Get("ring");
            theme.diamond = PlaceholderArt.Get("diamond");
            theme.triangle = PlaceholderArt.Get("triangle");
            theme.star = PlaceholderArt.Get("star");
            theme.shard = PlaceholderArt.Get("shard");
            theme.softDot = PlaceholderArt.Get("soft_dot");
            theme.iconWeapon = PlaceholderArt.Get("icon_weapon");
            theme.iconHelmet = PlaceholderArt.Get("icon_helmet");
            theme.iconChest = PlaceholderArt.Get("icon_chest");
            theme.iconGloves = PlaceholderArt.Get("icon_gloves");
            theme.iconBoots = PlaceholderArt.Get("icon_boots");
            theme.iconRing = PlaceholderArt.Get("icon_ring");
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            if (theme.font == null) Debug.LogWarning("[UISetup] TMP font missing; run the TMP import first.");
            return theme;
        }
    }
}
