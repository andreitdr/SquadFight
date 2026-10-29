using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Data;
using Game.EditorTools.Content;
using Game.Services;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Fight balance simulator: one fight × party preset × N seeded battles.</summary>
    public sealed class FightBalanceWindow : EditorWindow
    {
        private int _fight, _preset, _star = 1, _gear, _runs = 200;
        private string _output = "";
        private Vector2 _scroll;
        private static readonly string[] Presets = { "Starting party", "Casual player at first attempt (seed 1)", "Best 5 heroes at star N + gear" };

        [MenuItem("Tools/Game/Balance/Fight Balance Simulator")]
        public static void Open() => GetWindow<FightBalanceWindow>("Fight Balance");

        private void OnGUI()
        {
            var db = new ConfigDatabase(ConfigValidatorMenu.LoadGameConfig()).Database;
            _fight = EditorGUILayout.Popup("Fight", Mathf.Clamp(_fight, 0, db.Fights.Count - 1), db.Fights.Select(f => f.Id).ToArray());
            _preset = EditorGUILayout.Popup("Party", _preset, Presets);
            if (_preset == 2)
            {
                _star = EditorGUILayout.IntSlider("Star", _star, 1, db.MaxStar);
                _gear = EditorGUILayout.Popup("Gear rarity", _gear, new[] { "none" }.Concat(Enum.GetNames(typeof(Rarity))).ToArray());
            }
            _runs = EditorGUILayout.IntSlider("Runs", _runs, 10, 2000);
            if (GUILayout.Button("Run")) _output = Run(db, db.Fights[_fight], _preset, _star, _gear == 0 ? (Rarity?)null : (Rarity)(_gear - 1), _runs);
            if (GUILayout.Button("All fights (casual first attempt) → BalanceReports/fights.md")) _output = AllFightsReport(db, _runs);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_output, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        public static string Run(GameDatabase db, FightDef fight, int preset, int star, Rarity? gear, int runs)
        {
            GameSession party;
            if (preset == 1)
            {
                var snaps = FightBalance.SnapshotsAtFirstAttempt(db, new CasualPolicy(), 1);
                if (!snaps.TryGetValue(fight.Id, out var data)) return "The simulated player never reached this fight.";
                party = new GameSession(db, data, null, new SeededRandom(1), new ManualClock());
            }
            else if (preset == 2)
            {
                var best = db.Heroes.OrderByDescending(h => PowerRating.Compute(h.BaseStats, db.Combat)).Take(db.Rules.PartySize).Select(h => h.Id);
                party = FightBalance.Preset(db, best, star, gear);
            }
            else party = FightBalance.Preset(db, db.Rules.StartingHeroIds, 1, null);
            return FightBalance.Table(new[] { FightBalance.Run(party, fight, runs, 1, Presets[preset]) });
        }

        public static string AllFightsReport(GameDatabase db, int runs)
        {
            var snaps = FightBalance.SnapshotsAtFirstAttempt(db, new CasualPolicy(), 1);
            var results = db.Fights.Where(f => snaps.ContainsKey(f.Id))
                .Select(f => FightBalance.Run(new GameSession(db, snaps[f.Id], null, new SeededRandom(1), new ManualClock()), f, runs, 1, "casual first try"));
            var text = "# Fight balance (casual player, first attempt)\n\n" + FightBalance.Table(results);
            BalanceReports.Write("fights.md", text);
            return text;
        }
    }

    /// <summary>Report files, economy report and config snapshots/diffs (all under BalanceReports/, git-ignored).</summary>
    public static class BalanceReports
    {
        public const string Folder = "BalanceReports";

        public static void Write(string name, string text)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, name), text);
        }

        [MenuItem("Tools/Game/Balance/Economy Report")]
        public static void EconomyMenu() => Debug.Log(Economy(new ConfigDatabase(ConfigValidatorMenu.LoadGameConfig()).Database));

        /// <summary>Expected gold/shards/items per clear for every fight (repeat drops) and the gold sinks.</summary>
        public static string Economy(GameDatabase db, int rolls = 4000)
        {
            var sb = new StringBuilder("# Economy report\n\n| Fight | Gold/clear | Shards/clear | Items/clear | Item rarities |\n|---|---|---|---|---|\n");
            foreach (var f in db.Fights)
            {
                if (!db.TryGetDropTable(f.DropTableId, out var t)) continue;
                var rng = new SeededRandom(1);
                double gold = 0, shards = 0, items = 0;
                var rar = new int[5];
                for (var i = 0; i < rolls; i++)
                {
                    var b = DropRoller.Roll(t, rng, db);
                    gold += b.Currencies.Values.Sum();
                    shards += b.Shards.Values.Sum();
                    items += b.Items.Count;
                    foreach (var it in b.Items) rar[(int)it.Rarity]++;
                }
                var total = Math.Max(1, rar.Sum());
                sb.AppendLine($"| {f.Id} | {gold / rolls:0.0} | {shards / rolls:0.00} | {items / rolls:0.00} | {string.Join(" ", rar.Select((n, i) => n == 0 ? null : $"{(Rarity)i} {100.0 * n / total:0}%").Where(x => x != null))} |");
            }
            sb.AppendLine("\n## Gold sinks (star-ups, per hero)\n\n| Rarity | Shards to max star | Gold to max star | Sell value per item |\n|---|---|---|---|");
            foreach (var r in db.Rarities)
            {
                var hero = db.Heroes.FirstOrDefault(h => h.Rarity == r.Rarity);
                if (hero == null) continue;
                var gold = db.StarLevels.Where(s => s.Star > 1).Sum(s => s.CurrencyCost);
                sb.AppendLine($"| {r.Rarity} | {HeroProgressionRules.TotalShardsToMax(hero, db)} | {gold} | {r.ItemSellValue} |");
            }
            var text = sb.ToString();
            Write("economy.md", text);
            return text;
        }

        public static string Snapshot(GameDatabase db) => JsonConvert.SerializeObject(db, Formatting.Indented, new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter(), new StatBlockConverter() },
        });

        /// <summary>StatBlock has no public members; write it as { "Attack": 50, ... }.</summary>
        private sealed class StatBlockConverter : JsonConverter<StatBlock>
        {
            public override void WriteJson(JsonWriter writer, StatBlock value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                foreach (var s in StatBlock.All) { writer.WritePropertyName(s.ToString()); writer.WriteValue(value[s]); }
                writer.WriteEndObject();
            }

            public override StatBlock ReadJson(JsonReader reader, System.Type objectType, StatBlock existingValue, bool hasExistingValue, JsonSerializer serializer) =>
                throw new System.NotSupportedException();
        }

        [MenuItem("Tools/Game/Balance/Save Config Snapshot")]
        public static void SnapshotMenu()
        {
            var path = Path.Combine(Folder, "snapshots", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Snapshot(new ConfigDatabase(ConfigValidatorMenu.LoadGameConfig()).Database));
            Debug.Log("[Balance] snapshot saved to " + path);
        }

        [MenuItem("Tools/Game/Balance/Diff Last Two Snapshots")]
        public static void DiffMenu()
        {
            var dir = Path.Combine(Folder, "snapshots");
            var files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json").OrderBy(f => f).ToList() : new List<string>();
            if (files.Count < 2) { Debug.LogWarning("[Balance] need two snapshots (Save Config Snapshot)."); return; }
            var text = Diff(File.ReadAllText(files[files.Count - 2]), File.ReadAllText(files[files.Count - 1]));
            Write("config-diff.md", text);
            Debug.Log(text);
        }

        /// <summary>Line-level diff of two snapshot JSONs, keyed by JSON path (balance patch notes).</summary>
        public static string Diff(string oldJson, string newJson)
        {
            var a = Flatten(Newtonsoft.Json.Linq.JToken.Parse(oldJson));
            var b = Flatten(Newtonsoft.Json.Linq.JToken.Parse(newJson));
            var sb = new StringBuilder("# Config diff\n\n| Path | Old | New |\n|---|---|---|\n");
            var changes = 0;
            foreach (var key in a.Keys.Union(b.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                a.TryGetValue(key, out var ov);
                b.TryGetValue(key, out var nv);
                if (ov == nv) continue;
                changes++;
                sb.AppendLine($"| {key} | {ov ?? "—"} | {nv ?? "—"} |");
            }
            if (changes == 0) sb.AppendLine("| (no changes) | | |");
            return sb.ToString();
        }

        private static Dictionary<string, string> Flatten(Newtonsoft.Json.Linq.JToken token)
        {
            var map = new Dictionary<string, string>();
            void Walk(Newtonsoft.Json.Linq.JToken t)
            {
                if (t is Newtonsoft.Json.Linq.JValue v) { map[KeyPath(t)] = v.ToString(CultureInfo.InvariantCulture); return; }
                foreach (var c in t.Children()) Walk(c);
            }
            Walk(token);
            return map;
        }

        /// <summary>Uses "Id" values instead of array indexes so reordering does not show as a change.</summary>
        private static string KeyPath(Newtonsoft.Json.Linq.JToken t)
        {
            var parts = new List<string>();
            for (var cur = t; cur != null && cur.Parent != null; cur = cur.Parent)
            {
                if (cur is Newtonsoft.Json.Linq.JProperty p) parts.Add(p.Name);
                else if (cur.Parent is Newtonsoft.Json.Linq.JArray arr)
                {
                    var id = cur is Newtonsoft.Json.Linq.JObject o && o["Id"] != null ? (string)o["Id"] : null;
                    parts.Add(id != null ? $"[{id}]" : $"[{arr.IndexOf(cur)}]");
                }
            }
            parts.Reverse();
            return string.Join(".", parts).Replace(".[", "[");
        }
    }

    /// <summary>Visualizes star multipliers and a hero's stat growth per star (bars).</summary>
    public sealed class CurveVisualizerWindow : EditorWindow
    {
        private int _hero;

        [MenuItem("Tools/Game/Balance/Star & Stat Curves")]
        public static void Open() => GetWindow<CurveVisualizerWindow>("Curves");

        private void OnGUI()
        {
            var db = new ConfigDatabase(ConfigValidatorMenu.LoadGameConfig()).Database;
            if (db.StarLevels.Count == 0) return;
            GUILayout.Label("Star stat multiplier", EditorStyles.boldLabel);
            Bars(db.StarLevels.Select(s => ($"★{s.Star}", s.StatMultiplier)).ToList(), db.StarLevels.Max(s => s.StatMultiplier));
            GUILayout.Label("Equipment multiplier by rarity", EditorStyles.boldLabel);
            Bars(db.Rarities.Select(r => (r.Rarity.ToString(), r.EquipmentStatMultiplier)).ToList(), db.Rarities.Max(r => r.EquipmentStatMultiplier));
            _hero = EditorGUILayout.Popup("Hero", Mathf.Clamp(_hero, 0, db.Heroes.Count - 1), db.Heroes.Select(h => h.Id).ToArray());
            var hero = db.Heroes[_hero];
            GUILayout.Label("Power per star (no gear)", EditorStyles.boldLabel);
            var powers = db.StarLevels.Select(s => ($"★{s.Star}", (float)PowerRating.Compute(StatCalculator.Compute(hero, s.Star, null, db), db.Combat))).ToList();
            Bars(powers, powers.Max(p => p.Item2));
        }

        private static void Bars(List<(string label, float value)> values, float max)
        {
            foreach (var (label, value) in values)
            {
                var r = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.LabelField(new Rect(r.x, r.y, 90, r.height), label);
                var bar = new Rect(r.x + 95, r.y + 2, (r.width - 160) * (max <= 0 ? 0 : value / max), r.height - 4);
                EditorGUI.DrawRect(bar, new Color(0.3f, 0.6f, 0.95f));
                EditorGUI.LabelField(new Rect(r.xMax - 60, r.y, 60, r.height), value.ToString("0.##"));
            }
        }
    }
}
