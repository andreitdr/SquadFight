using System.Collections.Generic;
using Game.Core;
using Game.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Fixture numbers: Common unlock 10 / Uncommon 20; star 2 = 10 shards + 50 gold, star 3 = 20 shards + 100 gold (max);
    /// Uncommon star cost ×1.5; overflow 5 gold/shard (Common), 10 (Uncommon). Start: 100 gold.</summary>
    public class HeroProgressionTests
    {
        private GameSession _s;

        [SetUp]
        public void SetUp() => _s = TestSession.Create();

        private long Gold => _s.Currency.Get(TestContent.Gold);

        [Test]
        public void Unlock_ExactThreshold_ConsumesShards()
        {
            var unlocked = new List<string>();
            _s.Bus.Subscribe<HeroUnlocked>(e => unlocked.Add(e.HeroId));
            _s.Roster.AddShards("hero_c", 19);
            Assert.IsFalse(_s.Roster.GetUnlockInfo("hero_c").CanUnlock);
            Assert.AreEqual(ErrorCode.InsufficientShards, _s.Roster.UnlockHero("hero_c").Code);
            Assert.IsFalse(_s.Roster.IsUnlocked("hero_c"));
            _s.Roster.AddShards("hero_c", 1);
            var info = _s.Roster.GetUnlockInfo("hero_c");
            Assert.AreEqual(20, info.Have);
            Assert.AreEqual(20, info.Need);
            Assert.IsTrue(info.CanUnlock);
            Assert.IsTrue(_s.Roster.UnlockHero("hero_c").Ok);
            Assert.IsTrue(_s.Roster.IsUnlocked("hero_c"));
            Assert.AreEqual(1, _s.Roster.GetStar("hero_c"));
            Assert.AreEqual(0, _s.Roster.GetShards("hero_c"));
            CollectionAssert.AreEqual(new[] { "hero_c" }, unlocked);
        }

        [Test]
        public void Unlock_IsManual_NotAutomatic()
        {
            _s.Roster.AddShards("hero_c", 50);
            Assert.IsFalse(_s.Roster.IsUnlocked("hero_c"));
            Assert.IsTrue(_s.Roster.CanUnlockOrUpgrade("hero_c"));
        }

        [Test]
        public void Unlock_Twice_Rejected()
        {
            _s.Roster.AddShards("hero_c", 40);
            Assert.IsTrue(_s.Roster.UnlockHero("hero_c").Ok);
            Assert.AreEqual(ErrorCode.AlreadyUnlocked, _s.Roster.UnlockHero("hero_c").Code);
            Assert.AreEqual(20, _s.Roster.GetShards("hero_c"), "second attempt must not consume shards");
        }

        [Test]
        public void Unlock_KeepShardsWhenConfigured()
        {
            var db = TestContent.Create();
            db.Rules.ConsumeShardsOnUnlock = false;
            var s = TestSession.Create(db);
            s.Roster.AddShards("hero_c", 25);
            s.Roster.UnlockHero("hero_c");
            Assert.AreEqual(25, s.Roster.GetShards("hero_c"));
        }

        [Test]
        public void ShardsForLockedHero_Accumulate_UnknownHeroRejected()
        {
            _s.Roster.AddShards("hero_e", 100);
            _s.Roster.AddShards("hero_e", 100);
            Assert.AreEqual(200, _s.Roster.GetShards("hero_e"));
            Assert.AreEqual(ErrorCode.NotFound, _s.Roster.AddShards("hero_nope", 1).Code);
            Assert.AreEqual(ErrorCode.InvalidArgument, _s.Roster.AddShards("hero_e", 0).Code);
            Assert.AreEqual(ErrorCode.InvalidArgument, _s.Roster.AddShards("hero_e", -3).Code);
        }

        [Test]
        public void StarUp_CostsShardsAndCurrency()
        {
            var info = _s.Roster.GetStarUpInfo("hero_a");
            Assert.AreEqual(10, info.NeedShards);
            Assert.AreEqual(50, info.CurrencyCost);
            Assert.AreEqual(1.2f, info.NextMultiplier, 1e-5f);
            Assert.IsFalse(info.CanUpgrade);
            Assert.AreEqual(ErrorCode.InsufficientShards, _s.Roster.UpgradeStar("hero_a").Code);
            _s.Roster.AddShards("hero_a", 12);
            Assert.IsTrue(_s.Roster.UpgradeStar("hero_a").Ok);
            Assert.AreEqual(2, _s.Roster.GetStar("hero_a"));
            Assert.AreEqual(2, _s.Roster.GetShards("hero_a"));
            Assert.AreEqual(50, Gold);
        }

        [Test]
        public void StarUp_InsufficientCurrency_LeavesStateUnchanged()
        {
            _s.Currency.Spend(TestContent.Gold, 60);
            _s.Roster.AddShards("hero_a", 10);
            Assert.AreEqual(ErrorCode.InsufficientFunds, _s.Roster.UpgradeStar("hero_a").Code);
            Assert.AreEqual(1, _s.Roster.GetStar("hero_a"));
            Assert.AreEqual(10, _s.Roster.GetShards("hero_a"));
            Assert.AreEqual(40, Gold);
        }

        [Test]
        public void StarUp_RarityMultipliesShardCost()
        {
            _s.Roster.AddShards("hero_c", 20);
            _s.Roster.UnlockHero("hero_c");
            Assert.AreEqual(15, _s.Roster.GetStarUpInfo("hero_c").NeedShards, "10 × 1.5");
        }

        [Test]
        public void StarCap_ThenOverflowConvertsToCurrency()
        {
            _s.Currency.Add(TestContent.Gold, 1000);
            _s.Roster.AddShards("hero_a", 35);
            Assert.IsTrue(_s.Roster.UpgradeStar("hero_a").Ok);
            var before = Gold;
            Assert.IsTrue(_s.Roster.UpgradeStar("hero_a").Ok, "star 3 = max");
            Assert.AreEqual(3, _s.Roster.GetStar("hero_a"));
            Assert.AreEqual(0, _s.Roster.GetShards("hero_a"), "5 leftover shards converted at max star");
            Assert.AreEqual(before - 100 + 5 * 5, Gold);
            Assert.AreEqual(ErrorCode.MaxStarReached, _s.Roster.UpgradeStar("hero_a").Code);
            Assert.IsTrue(_s.Roster.GetStarUpInfo("hero_a").IsMaxStar);

            var g = Gold;
            Assert.IsTrue(_s.Roster.AddShards("hero_a", 4, out var converted).Ok);
            Assert.AreEqual(4, converted);
            Assert.AreEqual(0, _s.Roster.GetShards("hero_a"));
            Assert.AreEqual(g + 20, Gold);
        }

        [Test]
        public void ChangingRarityConfig_ChangesBehaviour()
        {
            var db = TestContent.Create();
            db.GetRarity(Rarity.Uncommon).ShardsToUnlock = 3;
            var s = TestSession.Create(db);
            s.Roster.AddShards("hero_c", 3);
            Assert.IsTrue(s.Roster.UnlockHero("hero_c").Ok);
        }

        [Test]
        public void Party_LockedRejected_UnlockedAccepted()
        {
            Assert.AreEqual(ErrorCode.HeroLocked, _s.Party.Add("hero_c").Code);
            _s.Roster.AddShards("hero_c", 20);
            _s.Roster.UnlockHero("hero_c");
            Assert.IsTrue(_s.Party.Add("hero_c").Ok);
            Assert.IsTrue(_s.Party.Contains("hero_c"));
        }

        [Test]
        public void Rules_TotalShardsToMax()
        {
            var db = TestContent.Create();
            Assert.AreEqual(10 + 10 + 20, HeroProgressionRules.TotalShardsToMax(db.GetHero("hero_a"), db));
            Assert.AreEqual(20 + 15 + 30, HeroProgressionRules.TotalShardsToMax(db.GetHero("hero_c"), db));
            Assert.AreEqual(1f, HeroProgressionRules.StarMultiplier(0, db));
            Assert.AreEqual(1.5f, HeroProgressionRules.StarMultiplier(99, db), 1e-6f);
        }

        [Test]
        public void DebugConsole_ShardsAndUnlockWork()
        {
            var dbg = new DebugCommands(_s);
            StringAssert.Contains("now 20", dbg.Execute("give shards hero_c 20"));
            StringAssert.Contains("unlocked", dbg.Execute("unlock hero_c"));
            Assert.IsTrue(_s.Roster.IsUnlocked("hero_c"));
            StringAssert.Contains("unlocked", dbg.Execute("unlock hero_d force"));
            StringAssert.StartsWith("Failed", dbg.Execute("starup hero_d"));
            dbg.Execute("give shards hero_d 20");
            StringAssert.Contains("★2", dbg.Execute("starup hero_d"));
        }
    }
}
