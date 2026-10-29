using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Data;
using Game.EditorTools.Content;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Finds localization keys used by code (T/F/Plural literals + keys built from enums) and by content assets, and reports
    /// keys missing in English (errors) or in other languages (missing translations). Can write stub rows to fill in.
    /// </summary>
    public static class LocalizationTools
    {
        public const string UiTable = "Assets/_Game/Resources/Localization/strings_ui.csv";
        public const string StubFile = "Assets/_Game/Resources/Localization/strings_stubs.csv";
        private static readonly string[] CodeFolders = { "Assets/_Game/UI", "Assets/_Game/BattleView", "Assets/_Game/App", "Assets/_Game/ViewModels" };
        private static readonly Regex KeyCall = new Regex(@"\.(?:T|F|Plural)\(\s*""([a-z0-9_.]+)""\s*[,)]", RegexOptions.Compiled);

        public sealed class Report
        {
            public readonly SortedSet<string> UsedKeys = new SortedSet<string>(StringComparer.Ordinal);
            public readonly SortedSet<string> MissingEnglish = new SortedSet<string>(StringComparer.Ordinal);
            public readonly SortedDictionary<string, SortedSet<string>> MissingTranslations = new SortedDictionary<string, SortedSet<string>>();

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Localization: {UsedKeys.Count} keys used, {MissingEnglish.Count} missing in English");
                foreach (var k in MissingEnglish) sb.AppendLine("  MISSING en: " + k);
                foreach (var kv in MissingTranslations) sb.AppendLine($"  {kv.Key}: {kv.Value.Count} untranslated (falls back to English)");
                return sb.ToString();
            }
        }

        public static TableLocalizer LoadTables()
        {
            var loc = new TableLocalizer();
            foreach (var f in Directory.GetFiles("Assets/_Game/Resources/Localization", "*.csv"))
                if (!f.EndsWith("strings_stubs.csv")) loc.LoadCsv(File.ReadAllText(f));
            return loc;
        }

        public static IEnumerable<string> CodeKeys()
        {
            foreach (var folder in CodeFolders)
                foreach (var file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                    foreach (Match m in KeyCall.Matches(File.ReadAllText(file)))
                        yield return m.Groups[1].Value;
            // Keys composed at runtime from enums / ids.
            foreach (Rarity r in Enum.GetValues(typeof(Rarity))) yield return "rarity_" + r.ToString().ToLowerInvariant();
            foreach (StatType s in Enum.GetValues(typeof(StatType))) yield return "stat_" + s.ToString().ToLowerInvariant();
            foreach (EquipmentSlot s in Enum.GetValues(typeof(EquipmentSlot))) yield return "slot_" + s.ToString().ToLowerInvariant();
            foreach (HeroClass c in Enum.GetValues(typeof(HeroClass))) yield return "class_" + c.ToString().ToLowerInvariant();
            foreach (ErrorCode c in Enum.GetValues(typeof(ErrorCode))) if (c != ErrorCode.None) yield return "error_" + c.ToString().ToLowerInvariant();
            foreach (var t in new[] { "home", "campaign", "heroes", "inventory", "settings", "party" }) yield return "tab_" + t;
        }

        public static IEnumerable<string> ContentKeys(ConfigDatabase cdb)
        {
            var db = cdb.Database;
            foreach (var h in db.Heroes) { yield return h.NameKey; if (!string.IsNullOrEmpty(h.LoreKey)) yield return h.LoreKey; }
            foreach (var e in db.Enemies) yield return e.NameKey;
            foreach (var s in db.Skills) { yield return s.NameKey; if (!string.IsNullOrEmpty(s.DescriptionKey)) yield return s.DescriptionKey; }
            foreach (var e in db.Equipment) yield return e.NameKey;
            foreach (var c in db.Chapters) yield return c.NameKey;
            foreach (var f in db.Fights) yield return f.NameKey;
            foreach (var b in db.StoryBeats) { yield return b.SpeakerKey; yield return b.TextKey; }
            foreach (var c in db.Rules.Currencies) { yield return c.NameKey; yield return "currency_" + c.Id + "_name"; }
        }

        public static Report Analyze()
        {
            var loc = LoadTables();
            var report = new Report();
            var cdb = new ConfigDatabase(ConfigValidatorMenu.LoadGameConfig());
            foreach (var k in CodeKeys().Concat(ContentKeys(cdb)).Where(k => !string.IsNullOrEmpty(k))) report.UsedKeys.Add(k);
            bool Has(string lang, string k) => loc.HasIn(lang, k) || loc.HasIn(lang, k + ".other"); // plural keys
            foreach (var k in report.UsedKeys)
            {
                if (!Has(TableLocalizer.Fallback, k)) report.MissingEnglish.Add(k);
                foreach (var lang in loc.Languages.Where(l => l != TableLocalizer.Fallback))
                    if (!Has(lang, k))
                    {
                        if (!report.MissingTranslations.TryGetValue(lang, out var set)) report.MissingTranslations[lang] = set = new SortedSet<string>(StringComparer.Ordinal);
                        set.Add(k);
                    }
            }
            return report;
        }

        [MenuItem("Tools/Game/Localization/Find Missing Keys")]
        public static void FindMissingMenu()
        {
            var r = Analyze();
            if (r.MissingEnglish.Count > 0) Debug.LogError(r.ToString());
            else Debug.Log(r.ToString());
        }

        /// <summary>Writes key stubs (English missing → "TODO", other languages → English text) to strings_stubs.csv for translators.</summary>
        [MenuItem("Tools/Game/Localization/Generate Key Stubs")]
        public static void GenerateStubs()
        {
            var r = Analyze();
            var loc = LoadTables();
            var langs = loc.Languages.ToList();
            var rows = new List<string[]>();
            foreach (var k in r.UsedKeys)
            {
                var missingSomewhere = r.MissingEnglish.Contains(k) || r.MissingTranslations.Values.Any(s => s.Contains(k));
                if (!missingSomewhere) continue;
                rows.Add(new[] { k }.Concat(langs.Select(l => loc.HasIn(l, k) ? "" : l == TableLocalizer.Fallback ? "TODO " + k : "TODO " + loc.Get(k))).ToArray());
            }
            File.WriteAllText(StubFile, Csv.Write(new[] { "key" }.Concat(langs), rows));
            AssetDatabase.Refresh();
            Debug.Log($"[Localization] wrote {rows.Count} stub rows to {StubFile}");
        }
    }
}
