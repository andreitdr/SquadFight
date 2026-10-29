using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Save;
using Game.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class RewardTests
    {
        private GameDatabase _db;

        [SetUp]
        public void SetUp() => _db = TestContent.Create();

        private static DropEntryDef Cur(int min, int max, int weight = 1) => new DropEntryDef { Type = DropType.Currency, RefId = TestContent.Gold, AmountMin = min, AmountMax = max, Weight = weight };

        // ---------- roller ----------

        [Test]
        public void Guaranteed_AlwaysGiven_AmountsInRange()
        {
            var t = new DropTableDef { Id = "t", Guaranteed = { Cur(5, 9) } };
            var rng = new SeededRandom(1);
            var seen = new HashSet<long>();
            for (var i = 0; i < 500; i++)
            {
                var g = DropRoller.Roll(t, rng, _db).Currencies[TestContent.Gold];
                Assert.That(g, Is.InRange(5, 9));
                seen.Add(g);
            }
            Assert.AreEqual(5, seen.Count, "every amount in [min,max] occurs");
        }

        [Test]
        public void Multipliers_ScaleCurrencyAndShards()
        {
            _db.Rules.CurrencyDropMultiplier = 2f;
            _db.Rules.ShardDropMultiplier = 3f;
            var t = new DropTableDef { Id = "t", Guaranteed = { Cur(10, 10), new DropEntryDef { Type = DropType.HeroShard, RefId = "hero_c", AmountMin = 2, AmountMax = 2 } } };
            var b = DropRoller.Roll(t, new SeededRandom(1), _db);
            Assert.AreEqual(20, b.Currencies[TestContent.Gold]);
            Assert.AreEqual(6, b.Shards["hero_c"]);
        }

        [Test]
        public void Statistical_ObservedMatchesWeights()
        {
            var a = new DropEntryDef { Type = DropType.HeroShard, RefId = "hero_a", Weight = 60 };
            var b = new DropEntryDef { Type = DropType.HeroShard, RefId = "hero_b", Weight = 30 };
            var c = new DropEntryDef { Type = DropType.HeroShard, RefId = "hero_c", Weight = 10 };
            var zero = new DropEntryDef { Type = DropType.HeroShard, RefId = "hero_d", Weight = 0 };
            var t = new DropTableDef { Id = "t", Groups = { new RollGroupDef { MinRolls = 1, MaxRolls = 3, Entries = { a, b, c, zero } } } };
            var counts = new Dictionary<DropEntryDef, int> { [a] = 0, [b] = 0, [c] = 0, [zero] = 0 };
            const int n = 10000;
            var rng = new SeededRandom(12345);
            for (var i = 0; i < n; i++) DropRoller.Roll(t, rng, _db, e => counts[e]++);
            var expected = DropRoller.ExpectedPicks(t);
            Assert.AreEqual(1.2, expected[a], 1e-9);
            foreach (var e in new[] { a, b, c })
                Assert.AreEqual(expected[e], counts[e] / (double)n, 0.03, e.RefId);
            Assert.AreEqual(0, counts[zero], "zero-weight entries are skipped");
        }

        [Test]
        public void WithoutReplacement_NoDuplicatePicks()
        {
            var entries = Enumerable.Range(0, 3).Select(i => new DropEntryDef { Type = DropType.HeroShard, RefId = "hero_" + (char)('a' + i), Weight = 1 }).ToList();
            var g = new RollGroupDef { MinRolls = 5, MaxRolls = 5, WithoutReplacement = true };
            g.Entries.AddRange(entries);
            var t = new DropTableDef { Id = "t", Groups = { g } };
            var rng = new SeededRandom(3);
            for (var i = 0; i < 200; i++)
            {
                var picks = new List<DropEntryDef>();
                DropRoller.Roll(t, rng, _db, picks.Add);
                Assert.AreEqual(3, picks.Count, "extra rolls give nothing");
                Assert.AreEqual(3, picks.Distinct().Count());
            }
        }

        [Test]
        public void RandomEquipment_RespectsSlotAndRarityWeights()
        {
            var e = new DropEntryDef { Type = DropType.RandomEquipment, SlotFilter = EquipmentSlot.Ring, RarityWeights = { new RarityWeight(Rarity.Epic, 1), new RarityWeight(Rarity.Common, 0) }, AmountMin = 2, AmountMax = 2 };
            var b = DropRoller.Roll(new DropTableDef { Id = "t", Guaranteed = { e } }, new SeededRandom(1), _db);
            Assert.AreEqual(2, b.Items.Count);
            Assert.IsTrue(b.Items.All(i => i.TemplateId == "eq_ring" && i.Rarity == Rarity.Epic));
        }

        [Test]
        public void RandomHeroShardByRarity_PicksHeroOfThatRarity()
        {
            var e = new DropEntryDef { Type = DropType.RandomHeroShardByRarity, Rarity = Rarity.Common, AmountMin = 1, AmountMax = 1 };
            var rng = new SeededRandom(8);
            var heroes = new HashSet<string>();
            for (var i = 0; i < 100; i++) heroes.UnionWith(DropRoller.Roll(new DropTableDef { Id = "t", Guaranteed = { e } }, rng, _db).Shards.Keys);
            CollectionAssert.AreEquivalent(new[] { "hero_a", "hero_b" }, heroes);
        }

        [Test]
        public void Roll_IsDeterministicForSeed()
        {
            var t = _db.GetDropTable("drops_basic");
            for (ulong seed = 0; seed < 50; seed++)
                Assert.AreEqual(DropRoller.Roll(t, new SeededRandom(seed), _db).ToString(), DropRoller.Roll(t, new SeededRandom(seed), _db).ToString());
        }

        // ---------- service ----------

        [Test]
        public void Apply_GrantsEverything_SavesOnce_ReportsHighlights()
        {
            var s = TestSession.Create(out var saver);
            var bundle = new RewardBundle();
            bundle.AddCurrency(TestContent.Gold, 40);
            bundle.AddShards("hero_c", 20);
            bundle.AddShards("hero_a", 10);
            bundle.Items.Add(EquipmentFactory.Create(s.Db.GetEquipment("eq_sword"), Rarity.Rare, s.Random, s.Db));
            var granted = 0;
            s.Bus.Subscribe<RewardsGranted>(_ => granted++);
            var r = s.Rewards.Apply(bundle);
            Assert.IsTrue(r.Ok, r.ToString());
            Assert.AreEqual(140, s.Currency.Get(TestContent.Gold));
            Assert.AreEqual(20, s.Roster.GetShards("hero_c"));
            Assert.AreEqual(1, s.Inventory.Count);
            CollectionAssert.AreEqual(new[] { "hero_c" }, r.Value.NowUnlockable);
            CollectionAssert.AreEqual(new[] { "hero_a" }, r.Value.StarUpAvailable);
            Assert.AreEqual(1, r.Value.BetterItems.Count, "a sword beats an empty weapon slot");
            Assert.AreEqual((0, 20), r.Value.ShardProgress["hero_c"]);
            Assert.AreEqual(1, saver.Saves);
            Assert.AreEqual(1, granted);
        }

        [Test]
        public void Apply_InvalidBundle_LeavesStateUnchanged()
        {
            var s = TestSession.Create(out var saver);
            var before = SaveSerializer.ToCanonicalJson(s.State.Data);
            var bundle = new RewardBundle();
            bundle.AddCurrency(TestContent.Gold, 40);
            bundle.AddShards("hero_c", 5);
            bundle.AddShards("hero_missing", 5);
            Assert.AreEqual(ErrorCode.NotFound, s.Rewards.Apply(bundle).Code);
            Assert.AreEqual(before, SaveSerializer.ToCanonicalJson(s.State.Data));
            Assert.AreEqual(0, saver.Saves);
        }

        private sealed class ExplodingGranter : IFightRewardGranter
        {
            private readonly RewardService _inner;
            public ExplodingGranter(RewardService inner) => _inner = inner;
            public RewardResult GrantFightRewards(FightDef fight, bool victory, bool firstClear, string chapterCompletedId)
            {
                _inner.GrantFightRewards(fight, victory, firstClear, chapterCompletedId); // rewards applied...
                throw new System.InvalidOperationException("simulated failure after partial apply");
            }
        }

        [Test]
        public void CompleteFight_FailureMidway_RollsBackProgressAndRewards()
        {
            var s = TestSession.Create(out var saver);
            s.Progress.Rewards = new ExplodingGranter(s.Rewards);
            var events = 0;
            s.Bus.Subscribe<CurrencyChanged>(_ => events++);
            s.Bus.Subscribe<FightCompleted>(_ => events++);
            var before = SaveSerializer.ToCanonicalJson(s.State.Data);
            var r = s.Progress.CompleteFight("fight_1", new BattleResult { Outcome = BattleOutcome.Victory, Rounds = 1 });
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(before, SaveSerializer.ToCanonicalJson(s.State.Data), "no progress, gold, shards or items kept");
            Assert.AreEqual(0, saver.Saves);
            Assert.AreEqual(0, events, "no events for rolled-back changes");
            Assert.IsFalse(s.Context.InTransaction);
        }

        [Test]
        public void FullInventory_ConvertsItemsToGold_NothingLost()
        {
            var db = TestContent.Create();
            db.Rules.InventoryLimit = 1;
            var s = TestSession.Create(db);
            var bundle = new RewardBundle();
            bundle.Items.Add(EquipmentFactory.Create(db.GetEquipment("eq_sword"), Rarity.Common, s.Random, db));
            bundle.Items.Add(EquipmentFactory.Create(db.GetEquipment("eq_sword"), Rarity.Rare, s.Random, db));
            var r = s.Rewards.Apply(bundle).Value;
            Assert.AreEqual(1, s.Inventory.Count);
            Assert.AreEqual(1, r.AutoSold.Count);
            Assert.AreEqual(160, s.Currency.Get(TestContent.Gold));
        }

        [Test]
        public void FirstClearTable_OnlyOnce_ThroughProgress()
        {
            // drops_basic: 10 gold guaranteed (no gold in its roll group); drops_first: +50 gold and an Uncommon sword.
            var s = TestSession.Create();
            var win = new BattleResult { Outcome = BattleOutcome.Victory, Rounds = 2 };
            var first = s.Progress.CompleteFight("fight_1", win).Value;
            Assert.AreEqual(60, first.Rewards.Bundle.Currencies[TestContent.Gold]);
            for (var i = 0; i < 20; i++)
                Assert.AreEqual(10, s.Progress.CompleteFight("fight_1", win).Value.Rewards.Bundle.Currencies[TestContent.Gold]);
        }

        [Test]
        public void ChapterClearReward_GrantedOnce()
        {
            var s = TestSession.Create();
            var win = new BattleResult { Outcome = BattleOutcome.Victory, Rounds = 2 };
            s.Progress.CompleteFight("fight_1", win);
            s.Progress.CompleteFight("fight_2", win);
            var last = s.Progress.CompleteFight("fight_3", win).Value;
            Assert.AreEqual("chapter_1", last.ChapterCompletedId);
            Assert.GreaterOrEqual(last.Rewards.Bundle.Currencies[TestContent.Gold], 60, "drops + chapter clear 50");
        }

        [Test]
        public void Defeat_GetsConsolationOnlyWhenConfigured()
        {
            var s = TestSession.Create();
            var loss = new BattleResult { Outcome = BattleOutcome.Defeat };
            Assert.IsTrue(s.Progress.CompleteFight("fight_1", loss).Value.Rewards.IsEmpty);
            var db = TestContent.Create();
            db.Rules.DefeatConsolationTableId = "drops_basic";
            var s2 = TestSession.Create(db);
            Assert.IsFalse(s2.Progress.CompleteFight("fight_1", loss).Value.Rewards.IsEmpty);
        }

        [Test]
        public void Pity_GuaranteesShardAfterNDryFights()
        {
            var db = TestContent.Create();
            db.Rules.PityEnabled = true;
            db.Rules.PityFightsWithoutShard = 3;
            db.GetDropTable("drops_basic").Groups[0].Entries.ForEach(e => e.Weight = e.Type == DropType.RandomEquipment ? 1 : 0);
            db.GetDropTable("drops_basic").Groups[0].Entries.Add(new DropEntryDef { Type = DropType.HeroShard, RefId = "hero_c", Weight = 0 });
            var s = TestSession.Create(db);
            var win = new BattleResult { Outcome = BattleOutcome.Victory, Rounds = 2 };
            s.Progress.CompleteFight("fight_1", win);
            var shardsPerFight = new List<int>();
            for (var i = 0; i < 6; i++) shardsPerFight.Add(s.Progress.CompleteFight("fight_1", win).Value.Rewards.Bundle.Shards.Values.Sum());
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 0, 1, 0 }, shardsPerFight, "first fight counted too: 3rd, 6th dry victories trigger");
            Assert.AreEqual(1, s.State.Data.Pity.FightsWithoutShard, "one dry victory since the last pity shard");
        }

        [Test]
        public void Pity_DisabledByDefault()
        {
            var s = TestSession.Create();
            Assert.IsFalse(s.Db.Rules.PityEnabled);
            Assert.AreEqual(0, s.State.Data.Pity.FightsWithoutShard);
        }

        [Test]
        public void SaveV2Fixture_MigratesPityCounters()
        {
            var r = new SaveSerializer().Decode(Fixtures.Read("save_v2.json"));
            Assert.AreEqual(DecodeStatus.Ok, r.Status, r.Error);
            Assert.AreEqual(SaveVersions.Current, r.Data.Version);
            Assert.IsNotNull(r.Data.Pity);
            Assert.AreEqual(0, r.Data.Pity.FightsWithoutShard);
            Assert.AreEqual(3, r.Data.Heroes[0].Stars);
            CollectionAssert.AreEqual(new ulong[] { 12345, 67890 }, r.Data.RngState);
        }

        [Test]
        public void RewardsPersist_SaveHasCorrectCountsAfterFight()
        {
            var clock = new ManualClock();
            var storage = new InMemorySaveStorage();
            var manager = new SaveManager(new SaveStore(storage, clock), clock);
            var load = manager.Load(() => NewGameFactory.Create(_db, clock));
            var s = new GameSession(_db, load.Data, manager, new SeededRandom(4), clock);
            manager.Bind(s.ExportForSave);
            var c = s.Battles.RunInstant("fight_1", 9).Value;
            Assert.IsTrue(c.Victory);
            // No Tick/Flush: the completion must already be on disk.
            var reloaded = new SaveStore(storage, clock).Load(() => null).Data;
            Assert.AreEqual(s.Currency.Get(TestContent.Gold), reloaded.Currencies[TestContent.Gold]);
            Assert.AreEqual(s.Inventory.Count, reloaded.Equipment.Count);
            Assert.AreEqual(s.State.Data.Heroes.Sum(h => h.Shards), reloaded.Heroes.Sum(h => h.Shards));
            Assert.IsTrue(reloaded.Progress.Fights.Single(f => f.FightId == "fight_1").FirstClearClaimed);
            Assert.Greater(reloaded.Currencies[TestContent.Gold], 100);
        }

        // ---------- step 10 review findings ----------

        [Test]
        public void DuplicateItemIds_AreReRolled_NotFatal()
        {
            var s = TestSession.Create();
            var item = EquipmentFactory.Create(s.Db.GetEquipment("eq_sword"), Rarity.Rare, s.Random, s.Db);
            var twin = EquipmentFactory.Create(s.Db.GetEquipment("eq_sword"), Rarity.Rare, s.Random, s.Db);
            twin.InstanceId = item.InstanceId;
            var b = new RewardBundle();
            b.Items.Add(item);
            b.Items.Add(twin);
            Assert.IsTrue(s.Rewards.Apply(b).Ok);
            Assert.AreEqual(2, s.Inventory.Count);
        }

        [Test]
        public void RolledBackTransaction_RestoresRng()
        {
            var s = TestSession.Create();
            var state = s.Random.GetState();
            using (s.Context.BeginTransaction()) s.Random.NextInt(0, 1000);
            CollectionAssert.AreEqual(state, s.Random.GetState());
        }

        [Test]
        public void FightPath_EmitsRewardsGranted()
        {
            var s = TestSession.Create();
            var n = 0;
            s.Bus.Subscribe<RewardsGranted>(_ => n++);
            s.Progress.CompleteFight("fight_1", new BattleResult { Outcome = BattleOutcome.Victory, Rounds = 1 });
            Assert.AreEqual(1, n);
        }

        [Test]
        public void GrantOutsideTransaction_IsAtomicAndSaved()
        {
            var s = TestSession.Create(out var saver);
            s.Rewards.GrantFightRewards(s.Db.GetFight("fight_1"), true, true, null);
            Assert.AreEqual(1, saver.Saves);
            Assert.AreEqual(160, s.Currency.Get(TestContent.Gold));
        }

        [Test]
        public void BattleFlow_SecondStartRejected_WhileBattleInProgress()
        {
            var s = TestSession.Create();
            Assert.IsTrue(s.Battles.Start("fight_1", 1).Ok);
            Assert.IsFalse(s.Battles.Start("fight_1", 2).Ok);
            Assert.AreEqual(1UL, s.Battles.Current.Seed);
        }

        [Test]
        public void BattleFlow_RetreatAfterVictory_RecordsVictory()
        {
            var s = TestSession.Create();
            var b = s.Battles.Start("fight_1", 3).Value;
            while (b.Simulator.Step()) { }
            Assert.AreEqual(BattleOutcome.Victory, b.Simulator.Outcome);
            var r = s.Battles.Retreat();
            Assert.IsTrue(r.Value.Victory);
            Assert.IsTrue(s.Progress.IsFightCleared("fight_1"));
        }

        [Test]
        public void BattleFlow_WinRecordedEvenIfPartyPowerDropsMidBattle()
        {
            var db = TestContent.Create();
            var gate = new UnlockConditionDef { Type = UnlockConditionType.MinPartyPower };
            db.GetFight("fight_1").UnlockConditions.Add(gate);
            var s = TestSession.Create(db);
            gate.Value = s.PartyPower;
            var b = s.Battles.Start("fight_1", 3).Value;
            s.Party.Remove(1); // power drops below the gate during the battle
            Assert.IsFalse(s.Progress.IsFightUnlocked("fight_1"));
            var r = s.Battles.Finish();
            Assert.IsTrue(r.Ok, r.ToString());
            Assert.IsTrue(s.Progress.IsFightCleared("fight_1"));
        }

        [Test]
        public void BattleFlow_FailedRecording_KeepsBattleForRetry()
        {
            var s = TestSession.Create();
            var real = s.Progress.Rewards;
            var fail = true;
            s.Progress.Rewards = new FlakyGranter(() => fail, real);
            s.Battles.Start("fight_1", 3);
            Assert.IsFalse(s.Battles.Finish().Ok);
            Assert.IsNotNull(s.Battles.Current, "battle kept so the victory is not lost");
            fail = false;
            Assert.IsTrue(s.Battles.Finish().Value.Victory);
            Assert.IsNull(s.Battles.Current);
        }

        private sealed class FlakyGranter : IFightRewardGranter
        {
            private readonly System.Func<bool> _fail;
            private readonly IFightRewardGranter _inner;
            public FlakyGranter(System.Func<bool> fail, IFightRewardGranter inner) { _fail = fail; _inner = inner; }
            public RewardResult GrantFightRewards(FightDef f, bool v, bool first, string ch)
            {
                if (_fail()) throw new System.InvalidOperationException("flaky");
                return _inner.GrantFightRewards(f, v, first, ch);
            }
        }
    }
}
