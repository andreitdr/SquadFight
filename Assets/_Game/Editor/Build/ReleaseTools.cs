using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.EditorTools.Content;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Release helpers: placeholder audit (labelled assets still shipping) and THIRD_PARTY_NOTICES.md generation.</summary>
    public static class ReleaseTools
    {
        public static List<string> PlaceholderAssets() =>
            AssetDatabase.FindAssets("l:" + PlaceholderArt.Label).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();

        [MenuItem("Tools/Game/Release/Placeholder Audit")]
        public static void AuditMenu() => Debug.Log(WriteAudit());

        public static string WriteAudit()
        {
            var list = PlaceholderAssets();
            var sb = new StringBuilder("# Placeholder audit\n\n");
            sb.AppendLine(list.Count == 0 ? "No placeholder assets remain." : $"**{list.Count} placeholder asset(s) still in the project** (label `{PlaceholderArt.Label}`). Replace them with final art/audio (same file name in `Art/Final` / `Audio/Final`, then re-run the setup tools) before 1.0.");
            sb.AppendLine();
            foreach (var group in list.GroupBy(p => Path.GetDirectoryName(p)))
            {
                sb.AppendLine($"## {group.Key}");
                foreach (var p in group) sb.AppendLine("- " + Path.GetFileName(p));
            }
            File.WriteAllText("PLACEHOLDER_AUDIT.md", sb.ToString());
            return sb.ToString();
        }

        [MenuItem("Tools/Game/Release/Generate Third-Party Notices")]
        public static void NoticesMenu() => Debug.Log(WriteNotices());

        public static string WriteNotices()
        {
            var sb = new StringBuilder("# Third-party notices\n\nSquadFight is built with Unity and includes the following third-party components.\n\n");
            sb.AppendLine("| Component | Version | License |\n|---|---|---|");
            var lockPath = "Packages/packages-lock.json";
            if (File.Exists(lockPath))
            {
                var deps = (JObject)JObject.Parse(File.ReadAllText(lockPath))["dependencies"];
                foreach (var p in deps.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (p.Name.StartsWith("com.unity.modules.")) continue;
                    var version = (string)p.Value["version"];
                    var license = p.Name == "com.unity.nuget.newtonsoft-json" ? "MIT (Newtonsoft.Json, © James Newton-King)" : "Unity Companion License";
                    sb.AppendLine($"| {p.Name} | {version} | {license} |");
                }
            }
            sb.AppendLine("| Liberation Sans font | 2.x | SIL Open Font License 1.1 (`Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`) |");
            sb.AppendLine("| Placeholder sounds | — | CC0 1.0, generated for this project (AUDIO_CREDITS.md) |");
            sb.AppendLine("| Placeholder sprites | — | CC0 1.0, generated for this project (`Tools/Game/Content/Generate Placeholder Art`) |");
            sb.AppendLine("\nUnity Runtime: governed by the Unity Terms of Service. Full license texts ship with each package under `Library/PackageCache/<package>/LICENSE.md`.");
            File.WriteAllText("THIRD_PARTY_NOTICES.md", sb.ToString());
            return sb.ToString();
        }
    }

    /// <summary>Release builds warn about remaining placeholders; set BLOCK_PLACEHOLDERS=1 to fail them instead (final 1.0 gate).</summary>
    public sealed class PlaceholderBuildHook : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report)
        {
            if ((report.summary.options & BuildOptions.Development) != 0) return;
            var count = ReleaseTools.PlaceholderAssets().Count;
            if (count == 0) return;
            var message = $"{count} placeholder asset(s) in a release build (see Tools/Game/Release/Placeholder Audit).";
            if (Environment.GetEnvironmentVariable("BLOCK_PLACEHOLDERS") == "1") throw new BuildFailedException(message);
            Debug.LogWarning("[Release] " + message);
        }
    }
}
