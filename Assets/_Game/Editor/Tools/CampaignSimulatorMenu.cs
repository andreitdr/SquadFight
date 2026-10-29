using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Data;
using Game.EditorTools.Content;
using Game.Services;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Headless full-campaign playthroughs with the casual/greedy/hardcore policies → BalanceReports/progression.md.</summary>
    public static class CampaignSimulatorMenu
    {
        [MenuItem("Tools/Game/Balance/Run Progression Simulator")]
        public static void RunMenu() => Debug.Log(RunAll(new ulong[] { 1, 2, 3 }));

        /// <summary>Batch: -executeMethod Game.EditorTools.CampaignSimulatorMenu.RunBatch</summary>
        public static void RunBatch() => Debug.Log(RunAll(new ulong[] { 1, 2, 3 }));

        public static string RunAll(IEnumerable<ulong> seeds)
        {
            var db = new ConfigDatabase(ConfigValidatorMenu.LoadGameConfig()).Database;
            var names = new Dictionary<string, string>();
            if (File.Exists(ContentPaths.ContentStrings))
                foreach (var row in Csv.Read(ContentPaths.ContentStrings).Rows) names[row.Str("key")] = row.Str("en");
            var sb = new StringBuilder("# Progression simulation\n\n");
            foreach (var policy in new IPlayerPolicy[] { new GreedyPolicy(), new CasualPolicy(), new HardcorePolicy() })
                foreach (var seed in seeds)
                {
                    var report = CampaignSimulator.Run(db, policy, seed);
                    sb.AppendLine(report.ToMarkdown(id => names.TryGetValue(id + "_name", out var n) ? n : id));
                }
            Directory.CreateDirectory("BalanceReports");
            File.WriteAllText("BalanceReports/progression.md", sb.ToString());
            return sb.ToString();
        }
    }
}
