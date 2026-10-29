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
            Assert.AreEqual(s.Db.Fights.Count, s.Progress.TotalClearedFights, "a farming player clears the campaign within 500 fights");
            var decoded = new SaveSerializer().Decode(h.Storage.Files[SaveStore.MainKey]);
            Assert.AreEqual(DecodeStatus.Ok, decoded.Status);
            Assert.AreEqual(0, SaveReconciler.Reconcile(decoded.Data, s.Db).Count, "save never drifts into an inconsistent state");
        }

        [Test]
        public void FightBalance_And_Snapshots_OnRealContent()
        {
            var db = Db();
            var snaps = FightBalance.SnapshotsAtFirstAttempt(db, new CasualPolicy(), 1);
            Assert.AreEqual(db.Fights.Count, snaps.Count, "simulated player reaches every fight");
            var first = FightBalance.Run(FightBalance.Preset(db, db.Rules.StartingHeroIds, 1, Rarity.Common), db.Fights[0], 30, 1, "starters");
            Assert.Greater(first.WinRate, 0.9, "fight 1-1 is a tutorial fight");
            StringAssert.Contains("| fight_1_1 |", FightBalance.Table(new[] { first }));
        }
    }
}
