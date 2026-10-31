using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Applies Player Settings for the mobile portrait game. Idempotent; run from menu or batch mode.</summary>
    public static class ProjectSetup
    {
        public const string CompanyName = "andreitdr";
        public const string ProductName = "SquadFight";
        public const string BundleId = "com.andreitdr.squadfight";
        public const int AndroidTargetApi = 36;
        /// <summary>Home-screen icon, 1024 px opaque (tools/art/gen_ui.py app_icon). Kept outside Art/Generated so it is not imported as a compressed sprite.</summary>
        public const string AppIconPath = "Assets/_Game/Art/AppIcon/app_icon.png";

        [MenuItem("Tools/Game/Setup/Apply Player Settings")]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)AndroidTargetApi;
            PlayerSettings.Android.forceInternetPermission = false;

            PlayerSettings.gcIncremental = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Medium);
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            // D7: Android Auto Backup / iCloud backup stay ON so progress survives a reinstall (offline-game friendly).
            PlayerSettings.runInBackground = false;
            ApplyAppIcon();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Player settings applied.");
        }

        /// <summary>One icon for every platform: the default icon (Android legacy/round scale it) plus every iOS slot.</summary>
        public static void ApplyAppIcon()
        {
            if (AssetImporter.GetAtPath(AppIconPath) is TextureImporter imp)
            {
                var changed = imp.textureType != TextureImporterType.Default || imp.textureCompression != TextureImporterCompression.Uncompressed
                    || imp.mipmapEnabled || imp.maxTextureSize != 1024 || !imp.isReadable;
                imp.textureType = TextureImporterType.Default;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.maxTextureSize = 1024;
                imp.isReadable = true;
                if (changed) imp.SaveAndReimport();
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (icon == null) { Debug.LogWarning("[ProjectSetup] App icon missing: " + AppIconPath); return; }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
                foreach (var i in icons) i.SetTexture(icon);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
            }
        }

        /// <summary>Batch entry: Unity -batchmode -quit -executeMethod Game.EditorTools.ProjectSetup.RunAllBatch</summary>
        public static void RunAllBatch()
        {
            ApplyPlayerSettings();
            UISetup.Run();
            AudioSetup.Build();
            SceneBuilder.BuildAllScenes();
            AssetDatabase.SaveAssets();
        }
    }
}
