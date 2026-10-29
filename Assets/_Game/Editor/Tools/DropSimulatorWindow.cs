using System.IO;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Rolls a drop table N times and compares observed vs configured rates.</summary>
    public sealed class DropSimulatorWindow : EditorWindow
    {
        private int _tableIndex;
        private int _rolls = 10000;
        private int _seed = 1;
        private string _output = "";
        private Vector2 _scroll;

        [MenuItem("Tools/Game/Balance/Drop Simulator")]
        public static void Open() => GetWindow<DropSimulatorWindow>("Drop Simulator");

        private void OnGUI()
        {
            var cdb = new ConfigDatabase(ConfigValidatorMenu.LoadGameConfig());
            var tables = cdb.Database.DropTables;
            if (tables.Count == 0) { EditorGUILayout.HelpBox("No drop tables.", MessageType.Warning); return; }
            _tableIndex = EditorGUILayout.Popup("Table", Mathf.Clamp(_tableIndex, 0, tables.Count - 1), tables.Select(t => t.Id).ToArray());
            _rolls = EditorGUILayout.IntSlider("Rolls", _rolls, 100, 100000);
            _seed = EditorGUILayout.IntField("Seed", _seed);
            if (GUILayout.Button("Run")) _output = Simulate(cdb.Database, tables[_tableIndex], _rolls, (ulong)_seed);
            if (GUILayout.Button("Report all tables → BalanceReports/drops.md")) _output = WriteAllReport(cdb.Database, _rolls, (ulong)_seed);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_output, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        public static string Simulate(GameDatabase db, DropTableDef table, int rolls, ulong seed)
        {
            var rng = new SeededRandom(seed);
            var picks = table.Guaranteed.Concat(table.Groups.SelectMany(g => g.Entries)).ToDictionary(e => e, e => 0);
            long gold = 0, shards = 0, items = 0;
            var rarity = new int[System.Enum.GetValues(typeof(Rarity)).Length];
            for (var i = 0; i < rolls; i++)
            {
                var b = DropRoller.Roll(table, rng, db, e => picks[e]++);
                gold += b.Currencies.Values.Sum();
                shards += b.Shards.Values.Sum();
                items += b.Items.Count;
                foreach (var it in b.Items) rarity[(int)it.Rarity]++;
            }
            var expected = DropRoller.ExpectedPicks(table);
            var sb = new StringBuilder();
            sb.AppendLine($"### {table.Id} — {rolls} rolls, seed {seed}");
            sb.AppendLine("| Entry | Expected picks/clear | Observed | Δ |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var kv in picks)
            {
                var e = kv.Key;
                var obs = kv.Value / (double)rolls;
                var label = $"{e.Type} {e.RefId}{(e.Type == DropType.RandomHeroShardByRarity || e.Type == DropType.Equipment ? " " + e.Rarity : "")} ({e.AmountMin}-{e.AmountMax})";
                sb.AppendLine($"| {label} | {expected[e]:0.000} | {obs:0.000} | {obs - expected[e]:+0.000;-0.000} |");
            }
            sb.AppendLine();
            sb.AppendLine($"Per clear: gold {gold / (double)rolls:0.0}, shards {shards / (double)rolls:0.00}, items {items / (double)rolls:0.00}");
            if (items > 0) sb.AppendLine("Item rarities: " + string.Join(", ", rarity.Select((n, i) => $"{(Rarity)i} {100.0 * n / items:0.0}%")));
            return sb.ToString();
        }

        public static string WriteAllReport(GameDatabase db, int rolls, ulong seed)
        {
            var text = "# Drop tables report\n\n" + string.Join("\n", db.DropTables.Select(t => Simulate(db, t, rolls, seed)));
            Directory.CreateDirectory("BalanceReports");
            File.WriteAllText("BalanceReports/drops.md", text);
            return text;
        }

        /// <summary>Batch: -executeMethod Game.EditorTools.DropSimulatorWindow.ReportBatch</summary>
        public static void ReportBatch() => Debug.Log(WriteAllReport(new ConfigDatabase(ConfigValidatorMenu.LoadGameConfig()).Database, 10000, 1));
    }
}
