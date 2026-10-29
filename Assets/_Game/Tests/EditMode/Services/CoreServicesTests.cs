using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Save;
using Game.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class CoreServicesTests
    {
        private GameSession _s;
        private RecordingSaver _saver;

        [SetUp]
        public void SetUp() => _s = TestSession.Create(out _saver);

        // ---------- new game ----------

        [Test]
        public void NewGame_StartingHeroesCurrenciesAndParty()
        {
            Assert.IsTrue(_s.Roster.IsUnlocked("hero_a"));
            Assert.IsTrue(_s.Roster.IsUnlocked("hero_b"));
            Assert.IsFalse(_s.Roster.IsUnlocked("hero_c"));
            Assert.AreEqual(1, _s.Roster.GetStar("hero_a"));
            Assert.AreEqual(0, _s.Roster.GetStar("hero_c"));
            Assert.AreEqual(100, _s.Currency.Get(TestContent.Gold));
            CollectionAssert.AreEqual(new[] { "hero_a", "hero_b", null, null, null }, _s.Party.GetSlots());
            Assert.AreEqual(_s.Db.Heroes.Count, _s.State.Data.Heroes.Count, "every hero has a save row (locked heroes track shards)");
        }

        // ---------- currency ----------

        [Test]
        public void Currency_AddSpend_NeverNegative()
        {
            var events = new List<CurrencyChanged>();
            _s.Bus.Subscribe<CurrencyChanged>(events.Add);
            Assert.IsTrue(_s.Currency.Add(TestContent.Gold, 50).Ok);
            Assert.AreEqual(150, _s.Currency.Get(TestContent.Gold));
            var fail = _s.Currency.Spend(TestContent.Gold, 151);
            Assert.AreEqual(ErrorCode.InsufficientFunds, fail.Code);
            Assert.AreEqual(150, _s.Currency.Get(TestContent.Gold), "failed spend leaves balance unchanged");
            Assert.IsTrue(_s.Currency.Spend(TestContent.Gold, 150).Ok);
            Assert.AreEqual(0, _s.Currency.Get(TestContent.Gold));
            Assert.AreEqual(ErrorCode.InvalidArgument, _s.Currency.Add(TestContent.Gold, -5).Code);
            Assert.AreEqual(ErrorCode.InvalidArgument, _s.Currency.Spend(TestContent.Gold, -5).Code);
            Assert.AreEqual(ErrorCode.NotFound, _s.Currency.Add("gems", 5).Code);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(150, events[0].New);
            Assert.AreEqual(2, _saver.DirtyMarks);
        }

        [Test]
        public void Currency_SaturatesInsteadOfOverflowing()
        {
            _s.Currency.Add(TestContent.Gold, long.MaxValue);
            Assert.AreEqual(long.MaxValue, _s.Currency.Get(TestContent.Gold));
        }

        // ---------- party ----------

        [Test]
        public void Party_OnlyUnlockedHeroes()
        {
            Assert.AreEqual(ErrorCode.HeroLocked, _s.Party.Add("hero_c").Code);
            Assert.AreEqual(ErrorCode.NotFound, _s.Party.Assign(2, "hero_nope").Code);
            CollectionAssert.DoesNotContain(_s.Party.GetSlots(), "hero_c");
        }

        [Test]
        public void Party_NoDuplicates_AssignMovesHero()
        {
            Assert.AreEqual(ErrorCode.DuplicateInParty, _s.Party.Add("hero_a").Code);
            Assert.IsTrue(_s.Party.Assign(3, "hero_a").Ok);
            CollectionAssert.AreEqual(new[] { null, "hero_b", null, "hero_a", null }, _s.Party.GetSlots());
            Assert.IsTrue(_s.Party.Assign(1, "hero_a").Ok, "assigning onto an occupied slot swaps");
            CollectionAssert.AreEqual(new[] { null, "hero_a", null, "hero_b", null }, _s.Party.GetSlots());
            Assert.AreEqual(1, _s.Party.GetSlots().Count(h => h == "hero_a"));
        }

        [Test]
        public void Party_SizeNeverExceeded()
        {
            foreach (var h in _s.Db.Heroes) _s.State.GetOrCreateHero(h.Id).Unlocked = true;
            _s.State.GetOrCreateHero("hero_x").Unlocked = true;
            foreach (var h in _s.Db.Heroes) _s.Party.Add(h.Id);
            Assert.AreEqual(5, _s.Party.GetSlots().Length);
            Assert.AreEqual(5, _s.Party.Members.Count);
            _s.Db.Heroes.Add(TestContent.Hero("hero_f", Rarity.Common, TestContent.Stats(1, 1, 1, 1), null));
            _s.Db.Index();
            _s.State.GetOrCreateHero("hero_f").Unlocked = true;
            Assert.AreEqual(ErrorCode.PartyFull, _s.Party.Add("hero_f").Code);
            Assert.AreEqual(ErrorCode.InvalidSlot, _s.Party.Assign(5, "hero_f").Code);
            Assert.AreEqual(ErrorCode.InvalidSlot, _s.Party.Assign(-1, "hero_f").Code);
        }

        [Test]
        public void Party_RemoveSwapAndEvents()
        {
            var changes = 0;
            _s.Bus.Subscribe<PartyChanged>(_ => changes++);
            Assert.IsTrue(_s.Party.Swap(0, 4).Ok);
            Assert.IsTrue(_s.Party.RemoveHero("hero_b").Ok);
            CollectionAssert.AreEqual(new[] { null, null, null, null, "hero_a" }, _s.Party.GetSlots());
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Party_NormalizeRepairsBadSaves()
        {
            var data = NewGameFactory.Create(_s.Db, new ManualClock());
            data.Party = new List<string> { "hero_a", "hero_a", "hero_c", "", "hero_unknown", "hero_b", "x" };
            var s = new GameSession(_s.Db, data, new RecordingSaver(), new SeededRandom(1), new ManualClock());
            CollectionAssert.AreEqual(new[] { "hero_a", null, null, "hero_b", null }, s.Party.GetSlots(), "overflow hero moved into a free slot, duplicates/locked/unknown dropped");
        }

        [Test]
        public void Party_AutoFillByPower()
        {
            _s.Party.Remove(0);
            _s.Party.Remove(1);
            _s.Party.AutoFill(id => id == "hero_b" ? 10 : 5);
            CollectionAssert.AreEqual(new[] { "hero_b", "hero_a", null, null, null }, _s.Party.GetSlots());
        }

        // ---------- state export ----------

        [Test]
        public void State_ExportsIdenticalFormat_AndRoundTrips()
        {
            _s.Currency.Add(TestContent.Gold, 33);
            _s.Party.Swap(0, 2);
            var exported = _s.ExportForSave();
            var serializer = new SaveSerializer();
            var decoded = serializer.Decode(serializer.Encode(exported, 1, System.DateTime.UtcNow));
            Assert.AreEqual(DecodeStatus.Ok, decoded.Status);
            var rebuilt = new GameSession(_s.Db, decoded.Data, new RecordingSaver(), new SeededRandom(99), new ManualClock());
            Assert.AreEqual(SaveSerializer.ToCanonicalJson(exported), SaveSerializer.ToCanonicalJson(rebuilt.ExportForSave()));
            Assert.AreEqual(_s.Random.NextInt(0, 1000000), rebuilt.Random.NextInt(0, 1000000), "RNG state restored from save");
        }

        // ---------- transactions ----------

        [Test]
        public void Transaction_Rollback_RestoresStateAndDropsEvents()
        {
            var events = 0;
            _s.Bus.Subscribe<CurrencyChanged>(_ => events++);
            var before = SaveSerializer.ToCanonicalJson(_s.State.Data);
            using (_s.Context.BeginTransaction())
            {
                _s.Currency.Add(TestContent.Gold, 500);
                _s.Party.Swap(0, 1);
                Assert.AreEqual(600, _s.Currency.Get(TestContent.Gold));
            }
            Assert.AreEqual(before, SaveSerializer.ToCanonicalJson(_s.State.Data));
            Assert.AreEqual(0, events);
            Assert.AreEqual(0, _saver.DirtyMarks);
        }

        [Test]
        public void Transaction_Complete_DefersEventsAndSavesOnce()
        {
            var events = 0;
            _s.Bus.Subscribe<CurrencyChanged>(_ => events++);
            using (var tx = _s.Context.BeginTransaction())
            {
                _s.Currency.Add(TestContent.Gold, 1);
                _s.Currency.Add(TestContent.Gold, 2);
                Assert.AreEqual(0, events, "events wait for commit");
                tx.Complete(saveNow: true);
            }
            Assert.AreEqual(2, events);
            Assert.AreEqual(1, _saver.DirtyMarks);
            Assert.AreEqual(1, _saver.Saves);
            Assert.AreEqual(103, _s.Currency.Get(TestContent.Gold));
        }

        // ---------- event bus ----------

        [Test]
        public void EventBus_HandlerExceptionsAreIsolated_UnsubscribeDuringPublish()
        {
            var bus = new EventBus();
            var calls = new List<string>();
            System.IDisposable sub = null;
            bus.Subscribe<int>(_ => throw new System.Exception("boom"));
            sub = bus.Subscribe<int>(_ => { calls.Add("a"); sub.Dispose(); });
            bus.Subscribe<int>(_ => calls.Add("b"));
            Assert.DoesNotThrow(() => bus.Publish(1));
            bus.Publish(2);
            CollectionAssert.AreEqual(new[] { "a", "b", "b" }, calls);
        }

        // ---------- reconcile ----------

        [Test]
        public void Reconcile_AddsNewHeroesAndRepairsLinks()
        {
            var data = NewGameFactory.Create(_s.Db, new ManualClock());
            data.Heroes.RemoveAll(h => h.HeroId == "hero_e");
            data.Heroes.Single(h => h.HeroId == "hero_a").Stars = 99;
            data.Currencies[TestContent.Gold] = -5;
            data.Equipment.Add(new EquipmentSave { InstanceId = "i1", TemplateId = "eq_sword", EquippedByHeroId = "hero_c", EquippedSlotIndex = 0 });
            data.Equipment.Add(new EquipmentSave { InstanceId = "i2", TemplateId = "eq_sword", EquippedByHeroId = "hero_a", EquippedSlotIndex = 1 });
            data.Equipment.Add(new EquipmentSave { InstanceId = "i3", TemplateId = "eq_sword", EquippedByHeroId = "hero_a", EquippedSlotIndex = 0 });
            data.Equipment.Add(new EquipmentSave { InstanceId = "i3", TemplateId = "eq_sword" });
            var fixes = SaveReconciler.Reconcile(data, _s.Db);
            Assert.IsNotEmpty(fixes);
            Assert.IsTrue(data.Heroes.Any(h => h.HeroId == "hero_e" && !h.Unlocked));
            Assert.AreEqual(_s.Db.MaxStar, data.Heroes.Single(h => h.HeroId == "hero_a").Stars);
            Assert.AreEqual(0, data.Currencies[TestContent.Gold]);
            Assert.IsNull(data.Equipment.Single(e => e.InstanceId == "i1").EquippedByHeroId, "locked hero cannot wear items");
            Assert.IsNull(data.Equipment.Single(e => e.InstanceId == "i2").EquippedByHeroId, "weapon in helmet slot");
            Assert.AreEqual("hero_a", data.Equipment.Single(e => e.InstanceId == "i3").EquippedByHeroId);
            Assert.AreEqual(3, data.Equipment.Count, "duplicate instance id removed");
        }

        // ---------- debug commands ----------

        [Test]
        public void DebugCommands_GiveListReset()
        {
            var dbg = new DebugCommands(_s);
            StringAssert.Contains("gold = 600", dbg.Execute("give gold 500"));
            StringAssert.StartsWith("Unknown command", dbg.Execute("fly"));
            StringAssert.StartsWith("Failed", dbg.Execute("give gems 5"));
            StringAssert.StartsWith("Error", dbg.Execute("give gold lots"));
            StringAssert.Contains("hero_a", dbg.Execute("list heroes"));
            dbg.Execute("reset");
            Assert.AreEqual(100, _s.Currency.Get(TestContent.Gold));
        }
    }
}
