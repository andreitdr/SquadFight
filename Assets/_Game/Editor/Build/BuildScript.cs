using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Command-line builds (see build.sh / BUILD.md). Version name from VERSION.txt (not "VERSION": that name shadows the C++ <version> header in IL2CPP builds on case-insensitive disks); build number from the
    /// BUILD_NUMBER env var or yyMMddHH (UTC). Config validation runs as a pre-build hook and blocks invalid content.
    /// Release signing comes only from environment variables (never committed).
    /// </summary>
    public static class BuildScript
    {
        public const string OutputRoot = "Builds";

        public static string VersionName => File.Exists("VERSION.txt") ? File.ReadAllText("VERSION.txt").Trim() : "0.1.0";

        public static int BuildNumber
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("BUILD_NUMBER");
                if (int.TryParse(env, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0) return n;
                return int.Parse(DateTime.UtcNow.ToString("yyMMddHH", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            }
        }

        private static string[] Scenes => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        private static void ApplyCommon(bool release)
        {
            ProjectSetup.ApplyPlayerSettings();
            PlayerSettings.bundleVersion = VersionName;
            PlayerSettings.Android.bundleVersionCode = BuildNumber;
            PlayerSettings.iOS.buildNumber = BuildNumber.ToString(CultureInfo.InvariantCulture);
            var level = release ? ManagedStrippingLevel.Medium : ManagedStrippingLevel.Low;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, level);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, level);
        }

        [MenuItem("Tools/Game/Build/Android Dev APK")]
        public static void BuildAndroidDev() => BuildAndroid(release: false);

        [MenuItem("Tools/Game/Build/Android Release AAB")]
        public static void BuildAndroidRelease() => BuildAndroid(release: true);

        private static void BuildAndroid(bool release)
        {
            ApplyCommon(release);
            EditorUserBuildSettings.buildAppBundle = release;
            PlayerSettings.Android.useCustomKeystore = false;
            if (release)
            {
                var keystore = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PATH");
                var b64 = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_BASE64");
                if (string.IsNullOrEmpty(keystore) && !string.IsNullOrEmpty(b64))
                {
                    keystore = Path.Combine(Path.GetTempPath(), "squadfight-upload.keystore");
                    File.WriteAllBytes(keystore, Convert.FromBase64String(b64));
                }
                if (string.IsNullOrEmpty(keystore) || !File.Exists(keystore))
                    Fail("Release builds need ANDROID_KEYSTORE_PATH (or ANDROID_KEYSTORE_BASE64), ANDROID_KEYSTORE_PASS, ANDROID_KEYALIAS_NAME, ANDROID_KEYALIAS_PASS. See BUILD.md.");
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME");
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS");
            }
            var file = release ? $"SquadFight-{VersionName}-{BuildNumber}.aab" : $"SquadFight-{VersionName}-{BuildNumber}-dev.apk";
            Run(BuildTarget.Android, Path.Combine(OutputRoot, "Android", file), release);
        }

        [MenuItem("Tools/Game/Build/iOS Xcode Project")]
        public static void BuildIos()
        {
            ApplyCommon(release: true);
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            Run(BuildTarget.iOS, Path.Combine(OutputRoot, "iOS"), release: true);
        }

        /// <summary>Development Xcode project for the iOS Simulator (Apple Silicon, arm64); compile with xcodebuild -sdk iphonesimulator.</summary>
        [MenuItem("Tools/Game/Build/iOS Simulator Project (dev)")]
        public static void BuildIosSimulator()
        {
            ApplyCommon(release: false);
            var previous = PlayerSettings.iOS.sdkVersion;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
            PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            try { Run(BuildTarget.iOS, Path.Combine(OutputRoot, "iOS-Simulator"), release: false); }
            finally { PlayerSettings.iOS.sdkVersion = previous; }
        }

        private static void Run(BuildTarget target, string path, bool release)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? OutputRoot);
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = target,
                options = release ? BuildOptions.None : BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Build] {target} {(release ? "release" : "dev")} {s.result}: {path} — {s.totalSize / (1024f * 1024f):0.0} MB, {s.totalTime.TotalSeconds:0}s, {s.totalErrors} errors, {s.totalWarnings} warnings");
            WriteSizeReport(report, path);
            if (s.result != BuildResult.Succeeded) Fail($"Build {s.result}");
        }

        /// <summary>Largest assets in the build (Build Report) → Builds/size-report.md.</summary>
        private static void WriteSizeReport(BuildReport report, string path)
        {
            try
            {
                var rows = report.packedAssets.SelectMany(p => p.contents)
                    .GroupBy(c => c.sourceAssetPath).Select(g => (path: g.Key, size: g.Sum(c => (long)c.packedSize)))
                    .OrderByDescending(x => x.size).Take(30)
                    .Select(x => $"| {x.size / 1024f:0.0} KB | {x.path} |");
                File.WriteAllText(Path.Combine(OutputRoot, "size-report.md"),
                    $"# Build size: {path}\n\nTotal: {report.summary.totalSize / (1024f * 1024f):0.0} MB\n\n| Size | Asset |\n|---|---|\n" + string.Join("\n", rows) + "\n");
            }
            catch (Exception e) { Debug.LogWarning("[Build] size report failed: " + e.Message); }
        }

        private static void Fail(string message)
        {
            Debug.LogError("[Build] " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw new BuildFailedException(message);
        }
    }
}
