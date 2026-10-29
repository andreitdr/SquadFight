using System.IO;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.EditorTools;
using Game.EditorTools.Content;
using Game.Services;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Tests.EditMode.UnityTests
{
    public class BalanceToolTests
    {
        private static string TempCopyOfSources()
        {
            var dir = Path.Combine(Path.GetTempPath(), "squadfight-src-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(ContentPaths.Source, "*.csv")) File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
            return dir;
        }

        private static string Canon(GameDatabase db, System.Func<GameDatabase, object> part) =>
            JsonConvert.SerializeObject(part(db), new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });

        [Test]
        public void PureCsvLoader_MatchesAssetPipeline()
        {
            var fromAssets = new ConfigDatabase(ConfigValidatorMenu.LoadGameConfig()).Database;
            var fromCsv = ContentCsvLoader.Load(ContentPaths.Source);
            foreach (var r in fromAssets.Rarities) r.ColorHex = "";
            foreach (var r in fromCsv.Rarities) r.ColorHex = "";
            foreach (var f in fromAssets.Fights.Concat(fromCsv.Fights)) f.BackgroundKey = null;
            Assert.AreEqual(Canon(fromAssets, d => d.Rules), Canon(fromCsv, d => d.Rules));
            Assert.AreEqual(Canon(fromAssets, d => d.Combat), Canon(fromCsv, d => d.Combat));
            Assert.AreEqual(Canon(fromAssets, d => d.Rarities.OrderBy(r => r.Rarity)), Canon(fromCsv, d => d.Rarities.OrderBy(r => r.Rarity)));
            Assert.AreEqual(Canon(fromAssets, d => d.StarLevels), Canon(fromCsv, d => d.StarLevels));
            Assert.AreEqual(Canon(fromAssets, d => d.Heroes.OrderBy(h => h.Id)), Canon(fromCsv, d => d.Heroes.OrderBy(h => h.Id)));
            Assert.AreEqual(Canon(fromAssets, d => d.Enemies.OrderBy(h => h.Id)), Canon(fromCsv, d => d.Enemies.OrderBy(h => h.Id)));
            Assert.AreEqual(Canon(fromAssets, d => d.Skills.OrderBy(h => h.Id)), Canon(fromCsv, d => d.Skills.OrderBy(h => h.Id)));
            Assert.AreEqual(Canon(fromAssets, d => d.Equipment.OrderBy(h => h.Id)), Canon(fromCsv, d => d.Equipment.OrderBy(h => h.Id)));
            Assert.AreEqual(Canon(fromAssets, d => d.DropTables.OrderBy(h => h.Id)), Canon(fromCsv, d => d.DropTables.OrderBy(h => h.Id)));
            Assert.AreEqual(Canon(fromAssets, d => d.Chapters), Canon(fromCsv, d => d.Chapters));
            Assert.AreEqual(Canon(fromAssets, d => d.Fights), Canon(fromCsv, d => d.Fights));
            Assert.AreEqual(Canon(fromAssets, d => d.StoryBeats.OrderBy(h => h.Id)), Canon(fromCsv, d => d.StoryBeats.OrderBy(h => h.Id)));
        }

        [Test]
        public void EditingOneCsvRow_UpdatesExactlyThatAsset_NoDuplicates()
        {
            var dir = TempCopyOfSources();
            try
            {
                var path = Path.Combine(dir, "heroes.csv");
                var lines = File.ReadAllLines(path);
                lines[1] = lines[1].Replace(",600,40,", ",600,41,"); // hero_knight attack 40 → 41
                File.WriteAllLines(path, lines);
                var report = ContentImporter.Import(dryRun: true, dir);
                CollectionAssert.IsEmpty(report.Errors, report.ToString());
                CollectionAssert.IsEmpty(report.Created, report.ToString());
                CollectionAssert.AreEqual(new[] { "hero hero_knight" }, report.Updated, report.ToString());
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void Export_ThenImport_IsANoOp()
        {
            var dir = TempCopyOfSources();
            try
            {
                ContentExporter.Export(dir);
                var report = ContentImporter.Import(dryRun: true, dir);
                CollectionAssert.IsEmpty(report.Errors, report.ToString());
                CollectionAssert.IsEmpty(report.Created, report.ToString());
                CollectionAssert.IsEmpty(report.Updated, report.ToString());
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void ConfigDiff_ReportsOnlyChanges()
        {
            var db = ContentCsvLoader.Load(ContentPaths.Source);
            var before = BalanceReports.Snapshot(db);
            db.GetHero("hero_knight").BaseStats[StatType.Attack] = 99;
            var diff = BalanceReports.Diff(before, BalanceReports.Snapshot(db));
            StringAssert.Contains("Heroes[hero_knight]", diff);
            Assert.AreEqual(1, diff.Split('\n').Count(l => l.StartsWith("| Heroes")));
            StringAssert.Contains("(no changes)", BalanceReports.Diff(before, before));
        }

        [Test]
        public void Reports_AreDeterministic()
        {
            var db = ContentCsvLoader.Load(ContentPaths.Source);
            Assert.AreEqual(BalanceReports.Economy(db, 300), BalanceReports.Economy(db, 300));
            var f = db.Fights[3];
            var a = FightBalance.Run(FightBalance.Preset(db, db.Rules.StartingHeroIds, 1, null), f, 50, 4, "x").ToMarkdownRow();
            var b = FightBalance.Run(FightBalance.Preset(db, db.Rules.StartingHeroIds, 1, null), f, 50, 4, "x").ToMarkdownRow();
            Assert.AreEqual(a, b);
        }
    }
}
