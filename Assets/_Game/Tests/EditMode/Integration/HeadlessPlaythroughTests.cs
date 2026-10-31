using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Save;
using Game.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Real content, real services, real save system — no Unity. The "headless full loop" from the QA plan.</summary>
    public class HeadlessPlaythroughTests
    {
        private static GameDatabase Db() => ContentCsvLoader.Load(Fixtures.ContentSource());

        private sealed class Harness
        {
            public readonly ManualClock Clock = new ManualClock();
            public readonly InMemorySaveStorage Storage = new InMemorySaveStorage();
            public SaveManager Manager;
            public GameSession Session;

            public Harness(GameDatabase db, ulong seed)
            {
                Manager = new SaveManager(new SaveStore(Storage, Clock), Clock);
                var load = Manager.Load(() => NewGameFactory.Create(db, Clock));
                Session = new GameSession(db, load.Data, Manager, new SeededRandom(seed), Clock);
                Manager.Bind(Session.ExportForSave);
            }
        }

        [Test]
        public void RealContent_IsValid()
        {
            var r = ConfigValidation.Validate(Db());
            Assert.IsFalse(r.HasErrors, r.ToString());
        }

        [Test]
        public void RealContent_HundredHeroes_FortyChapters()
        {
            var db = Db();
            Assert.AreEqual(100, db.Heroes.Count);
            foreach (HeroClass c in System.Enum.GetValues(typeof(HeroClass)))
            {
                var counts = System.Enum.GetValues(typeof(Rarity)).Cast<Rarity>().Select(r => db.Heroes.Count(h => h.Class == c && h.Rarity == r)).ToArray();
                Assert.AreEqual(20, counts.Sum(), c.ToString());
                for (var i = 1; i < counts.Length; i++) Assert.Less(counts[i], counts[i - 1], $"{c}: each rarity must be rarer than the one below");
                Assert.GreaterOrEqual(counts[counts.Length - 1], 1, $"{c} has a Legendary");
            }
            Assert.IsTrue(db.Heroes.All(h => h.PassiveSkillId != null && !string.IsNullOrEmpty(h.Origin)));
            Assert.AreEqual(40, db.Chapters.Count);
            Assert.IsTrue(db.Chapters.All(c => c.FightIds.Count >= 9 && c.FightIds.Count <= 11), "9-11 main fights per chapter");
            Assert.IsTrue(db.Chapters.All(c => c.SideQuestIds.Count >= 1 && c.SideQuestIds.Count <= 3), "1-3 side quests per chapter");
            Assert.IsTrue(db.Chapters.All(c => db.GetFight(c.FightIds.Last()).Waves.Any(w => w.Spawns.Any(sp => db.GetEnemy(sp.EnemyId).IsBoss))), "every chapter ends with a boss");
            var itemOnly = db.Fights.Count(f => !f.IsSideQuest && db.GetDropTable(f.DropTableId).Groups.SelectMany(g => g.Entries).All(e => e.Type != DropType.RandomHeroShardByRarity && e.Type != DropType.HeroShard));
            Assert.Greater(itemOnly, 40, "some fights give items only");
        }

        [Test]
        public void RealContent_EnemyRanks()
        {
            var db = Db();
            var used = new HashSet<string>(db.Fights.SelectMany(f => f.Waves.SelectMany(w => w.Spawns.Select(s => s.EnemyId))));
            Assert.GreaterOrEqual(db.Enemies.Count, 185, "121 enemies before the ranks + 64 new ones");
            foreach (EnemyRank rank in System.Enum.GetValues(typeof(EnemyRank)))
                Assert.GreaterOrEqual(db.Enemies.Count(e => e.Rank == rank && used.Contains(e.Id)), 16, $"{rank}s appear in fights");
            Assert.IsTrue(db.Enemies.Where(e => e.IsBoss).All(e => e.SpecialSkillIds.Count >= 2 && e.ExtraSkillIds.Count >= 1), "every boss has an extra skill and special attacks");
            Assert.IsTrue(db.Enemies.All(e => used.Contains(e.Id)), "every enemy is fought somewhere");
            var hp = db.Ranks.Select(r => r.HpMultiplier).ToList();
            var xp = db.Ranks.Select(r => r.Xp).ToList();
            for (var i = 1; i < hp.Count; i++) { Assert.Greater(hp[i], hp[i - 1]); Assert.Greater(xp[i], xp[i - 1]); }
            Assert.GreaterOrEqual(db.PlayerLevels.Count, 50);
        }

        [Test]
        public void SaveFromTheOldThreeChapters_ContinuesIntoTheNewCampaign()
        {
            var db = Db();
            var clock = new ManualClock();
            var data = NewGameFactory.Create(db, clock);
            var s = new GameSession(db, data, null, new SeededRandom(3), clock);
            // The original 24 fights (fight_1_1..fight_3_8) cleared, as in a save made before "The Hundred".
            foreach (var c in new[] { 1, 2, 3 })
                for (var i = 1; i <= 8; i++)
                {
                    var f = s.State.GetOrCreateFight($"fight_{c}_{i}");
                    f.Cleared = true;
                    f.FirstClearClaimed = true;
                    f.BestStars = 3;
                }
            Assert.IsTrue(s.Progress.IsChapterUnlocked("chapter_4"), "chapter 3's boss is still its last fight");
            Assert.IsTrue(s.Progress.IsFightUnlocked("fight_1_9"), "new fights inside old chapters open for old saves");
            Assert.IsFalse(s.Progress.IsChapterCleared("chapter_1"), "until the new fights are cleared");
            var json = new SaveSerializer().Encode(s.ExportForSave(), 1, new System.DateTime(2026, 10, 31, 0, 0, 0, System.DateTimeKind.Utc));
            var back = new SaveSerializer().Decode(json);
            Assert.AreEqual(DecodeStatus.Ok, back.Status, back.Error);
            var again = new GameSession(db, back.Data, null, new SeededRandom(3), clock);
            Assert.IsTrue(again.Progress.IsChapterUnlocked("chapter_4"));
        }

        [Test]
        public void NewGame_Fights_Rewards_Unlock_Equip_Save_Reload_StateIdentical()
        {
            var db = Db();
            var h = new Harness(db, 42);
            var s = h.Session;
            var policy = new GreedyPolicy();
            var unlockedBefore = s.Roster.UnlockedHeroIds.Count();
            for (var i = 0; i < 25; i++)
            {
                var next = s.Progress.GetNextRecommendedFight();
                var r = s.Battles.RunInstant(next);
                Assert.IsTrue(r.Ok, r.ToString());
                policy.Manage(s); // unlock, star-up, equip, party
                h.Clock.Advance(0.5);
                h.Manager.Tick();
            }
            Assert.Greater(s.Roster.UnlockedHeroIds.Count(), unlockedBefore, "heroes were unlocked from shard rewards");
            Assert.Greater(s.Inventory.All().Count(i => i.IsEquipped), 0, "items were equipped");
            Assert.Greater(s.Progress.TotalClearedFights, 3);
            h.Manager.Flush();

            var reloaded = new SaveStore(h.Storage, h.Clock).Load(() => null);
            Assert.AreEqual(LoadSource.Main, reloaded.Source);
            Assert.AreEqual(SaveSerializer.ToCanonicalJson(s.ExportForSave()), SaveSerializer.ToCanonicalJson(reloaded.Data));
            var again = new GameSession(db, reloaded.Data, null, new SeededRandom(1), h.Clock);
            Assert.AreEqual(s.PartyPower, again.PartyPower);
            Assert.AreEqual(s.Progress.GetNextRecommendedFight(), again.Progress.GetNextRecommendedFight());
        }

        [Test]
        public void Playthrough_IsDeterministic()
        {
            string Run()
            {
                var h = new Harness(Db(), 7);
                for (var i = 0; i < 15; i++)
                {
                    h.Session.Battles.RunInstant(h.Session.Progress.GetNextRecommendedFight());
                    new GreedyPolicy().Manage(h.Session);
                }
                return SaveSerializer.ToCanonicalJson(h.Session.ExportForSave());
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void Soak_500Fights_NoErrors_SaveStaysHealthy()
        {
            var h = new Harness(Db(), 99);
            var s = h.Session;
            var policy = new CasualPolicy();
            var errors = 0;
            string lastCleared = null;
            var farm = false;
            for (var i = 0; i < 500; i++)
            {
                var next = s.Progress.GetNextRecommendedFight();
                var target = farm && lastCleared != null ? lastCleared : next;
                var r = s.Battles.RunInstant(target);
                if (!r.Ok) errors++;
                else if (r.Value.Victory) { if (target == next) lastCleared = next; farm = false; }
                else farm = true;
                policy.Manage(s);
                h.Clock.Advance(1);
                h.Manager.Tick();
            }
            Assert.AreEqual(0, errors);
            Assert.IsNull(h.Manager.LastError);
            Assert.AreEqual(500, s.State.Data.Progress.TotalFights);
            Assert.IsTrue(s.Db.Chapters.Take(4).All(c => s.Progress.IsChapterCleared(c.Id)), "a farming player clears the first acts' chapters within 500 fights");
            var decoded = new SaveSerializer().Decode(h.Storage.Files[SaveStore.MainKey]);
            Assert.AreEqual(DecodeStatus.Ok, decoded.Status);
            Assert.AreEqual(0, SaveReconciler.Reconcile(decoded.Data, s.Db).Count, "save never drifts into an inconsistent state");
        }

        [Test]
        public void FightBalance_And_Snapshots_OnRealContent()
        {
            var db = Db();
            var snaps = FightBalance.SnapshotsAtFirstAttempt(db, new CasualPolicy(), 1);
            Assert.AreEqual(db.Fights.Count, snaps.Count, "simulated player reaches every fight, side quests included");
            var first = FightBalance.Run(FightBalance.Preset(db, db.Rules.StartingHeroIds, 1, Rarity.Common), db.Fights[0], 30, 1, "starters");
            Assert.Greater(first.WinRate, 0.9, "fight 1-1 is a tutorial fight");
            StringAssert.Contains("| fight_1_1 |", FightBalance.Table(new[] { first }));
        }
    }
}
