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
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Player settings applied.");
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
