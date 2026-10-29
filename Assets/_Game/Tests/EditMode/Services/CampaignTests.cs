using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Save;
using Game.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Fixture campaign: chapter_1 = fight_1, fight_2, fight_3 (boss wave); chapter_2 = fight_4. Default star limit 10 rounds.</summary>
    public class CampaignTests
    {
        private GameSession _s;
        private RecordingSaver _saver;
        private FakeGranter _granter;

        private sealed class FakeGranter : IFightRewardGranter
        {
            public readonly List<(string fight, bool victory, bool first, string chapter)> Calls = new List<(string, bool, bool, string)>();
            public RewardResult GrantFightRewards(FightDef fight, bool victory, bool firstClear, string chapterCompletedId)
            {
                Calls.Add((fight.Id, victory, firstClear, chapterCompletedId));
                return new RewardResult();
            }
        }

        [SetUp]
        public void SetUp()
        {
            _s = TestSession.Create(out _saver);
            _granter = new FakeGranter();
            _s.Progress.Rewards = _granter;
        }

        private static BattleResult Win(int rounds = 3, int deaths = 0) => new BattleResult { Outcome = BattleOutcome.Victory, Rounds = rounds, PlayerDeaths = deaths };
        private static BattleResult Loss() => new BattleResult { Outcome = BattleOutcome.Defeat, Rounds = 5, PlayerDeaths = 2 };

        private List<string> Unlocked() => _s.Db.Fights.Where(f => _s.Progress.IsFightUnlocked(f.Id)).Select(f => f.Id).ToList();

        [Test]
        public void UnlockChain_FightByFight_ThenNextChapter()
        {
            CollectionAssert.AreEqual(new[] { "fight_1" }, Unlocked());
            Assert.IsFalse(_s.Progress.IsChapterUnlocked("chapter_2"));
            Assert.AreEqual(ErrorCode.FightLocked, _s.Progress.CompleteFight("fight_2", Win()).Code);
            var c1 = _s.Progress.CompleteFight("fight_1", Win()).Value;
            CollectionAssert.AreEqual(new[] { "fight_2" }, c1.NewlyUnlockedFights);
            _s.Progress.CompleteFight("fight_2", Win());
            var c3 = _s.Progress.CompleteFight("fight_3", Win()).Value;
            Assert.IsTrue(_s.Progress.IsChapterUnlocked("chapter_2"));
            CollectionAssert.AreEqual(new[] { "fight_4" }, c3.NewlyUnlockedFights);
            Assert.AreEqual("chapter_1", c3.ChapterCompletedId);
        }

        [Test]
        public void Defeat_DoesNotUnlockOrClear()
        {
            var r = _s.Progress.CompleteFight("fight_1", Loss()).Value;
            Assert.IsFalse(r.Victory);
            Assert.AreEqual(0, r.Stars);
            Assert.IsFalse(_s.Progress.IsFightCleared("fight_1"));
            CollectionAssert.AreEqual(new[] { "fight_1" }, Unlocked());
            Assert.AreEqual((false, false), (_granter.Calls[0].victory, _granter.Calls[0].first));
        }

        [Test]
        public void FirstClear_OnlyOnce_ReplaysCountClears()
        {
            Assert.IsTrue(_s.Progress.CompleteFight("fight_1", Win()).Value.FirstClear);
            Assert.IsFalse(_s.Progress.CompleteFight("fight_1", Win()).Value.FirstClear);
            Assert.IsFalse(_s.Progress.CompleteFight("fight_1", Win()).Value.FirstClear);
            var p = _s.Progress.GetFightProgress("fight_1");
            Assert.AreEqual(3, p.Clears);
            Assert.IsTrue(p.FirstClearClaimed);
            Assert.AreEqual(1, _granter.Calls.Count(c => c.first));
            Assert.AreEqual(3, _granter.Calls.Count, "replays still roll normal drops");
        }

        [TestCase(3, 0, 3)]
        [TestCase(3, 1, 2)]
        [TestCase(11, 0, 2)]
        [TestCase(11, 2, 1)]
        public void Stars_Conditions(int rounds, int deaths, int expected) =>
            Assert.AreEqual(expected, _s.Progress.CompleteFight("fight_1", Win(rounds, deaths)).Value.Stars);

        [Test]
        public void Stars_PerFightLimitAndBestKept()
        {
            _s.Db.GetFight("fight_1").StarTurnLimit = 2;
            Assert.AreEqual(2, _s.Progress.CompleteFight("fight_1", Win(3)).Value.Stars);
            Assert.AreEqual(3, _s.Progress.CompleteFight("fight_1", Win(2)).Value.Stars);
            Assert.AreEqual(1, _s.Progress.CompleteFight("fight_1", Win(5, 1)).Value.Stars);
            Assert.AreEqual(3, _s.Progress.GetFightProgress("fight_1").BestStars);
            Assert.AreEqual(3, _s.Progress.ChapterStars("chapter_1"));
        }

        [Test]
        public void ChapterReward_ClaimedOnce()
        {
            foreach (var f in new[] { "fight_1", "fight_2", "fight_3" }) _s.Progress.CompleteFight(f, Win());
            Assert.IsNull(_s.Progress.CompleteFight("fight_3", Win()).Value.ChapterCompletedId);
            Assert.AreEqual(1, _granter.Calls.Count(c => c.chapter == "chapter_1"));
        }

        [Test]
        public void UnlockConditions_OverrideDefaults()
        {
            var db = TestContent.Create();
            db.GetFight("fight_2").UnlockConditions.Add(new UnlockConditionDef { Type = UnlockConditionType.HeroOwned, RefId = "hero_c" });
            db.GetChapter("chapter_2").UnlockConditions.Add(new UnlockConditionDef { Type = UnlockConditionType.MinPartyPower, Value = 1 });
            var s = TestSession.Create(db);
            Assert.IsFalse(s.Progress.IsFightUnlocked("fight_2"));
            Assert.IsTrue(s.Progress.IsFightUnlocked("fight_4"), "power condition met without clearing chapter 1");
            s.Roster.AddShards("hero_c", 20);
            s.Roster.UnlockHero("hero_c");
            Assert.IsTrue(s.Progress.IsFightUnlocked("fight_2"));
            db.GetChapter("chapter_2").UnlockConditions[0].Value = 999999;
            Assert.IsFalse(s.Progress.IsFightUnlocked("fight_4"));
        }

        [Test]
        public void ReorderingChapterAsset_ChangesOrder()
        {
            var db = TestContent.Create();
            db.GetChapter("chapter_1").FightIds.Reverse();
            db.Index();
            var s = TestSession.Create(db);
            Assert.IsTrue(s.Progress.IsFightUnlocked("fight_3"));
            Assert.IsFalse(s.Progress.IsFightUnlocked("fight_1"));
        }

        [Test]
        public void NextRecommendedFight()
        {
            Assert.AreEqual("fight_1", _s.Progress.GetNextRecommendedFight());
            _s.Progress.CompleteFight("fight_1", Win());
            Assert.AreEqual("fight_2", _s.Progress.GetNextRecommendedFight());
            new DebugCommands(_s).Execute("clear all");
            Assert.AreEqual("fight_4", _s.Progress.GetNextRecommendedFight(), "everything cleared: last fight");
        }

        [Test]
        public void StoryBeats_ShownOnceUnlessReplayed()
        {
            Assert.AreEqual(1, _s.Progress.GetBeats("fight_1", post: false).Count);
            _s.Progress.MarkBeatsSeen(new[] { "beat_1" });
            Assert.AreEqual(0, _s.Progress.GetBeats("fight_1", post: false).Count);
            Assert.AreEqual(1, _s.Progress.GetBeats("fight_1", post: false, replay: true).Count);
            Assert.AreEqual(1, _s.Progress.GetSeenBeats().Count);
        }

        [Test]
        public void Progress_PersistsAcrossRestart_AndIsSavedImmediately()
        {
            _s.Progress.CompleteFight("fight_1", Win(3, 1));
            Assert.AreEqual(1, _saver.Saves, "completion saves immediately");
            var ser = new SaveSerializer();
            var data = ser.Decode(ser.Encode(_s.ExportForSave(), 1, System.DateTime.UtcNow)).Data;
            var s2 = new GameSession(_s.Db, data, new RecordingSaver(), new SeededRandom(1), new ManualClock());
            Assert.IsTrue(s2.Progress.IsFightCleared("fight_1"));
            Assert.IsTrue(s2.Progress.IsFightUnlocked("fight_2"));
            Assert.AreEqual(2, s2.Progress.GetFightProgress("fight_1").BestStars);
        }

        [Test]
        public void BattleFlow_StartFinish_RetreatAndEmptyParty()
        {
            var start = _s.Battles.Start("fight_1", 7);
            Assert.IsTrue(start.Ok);
            Assert.AreEqual(7UL, start.Value.Seed);
            start.Value.Simulator.Step();
            var done = _s.Battles.Finish();
            Assert.IsTrue(done.Ok);
            Assert.AreEqual(BattleOutcome.Victory, done.Value.Battle.Outcome, "2 starter heroes beat one weak enemy");
            Assert.IsNull(_s.Battles.Current);
            Assert.IsFalse(_s.Battles.Finish().Ok);

            _s.Battles.Start("fight_2");
            var retreat = _s.Battles.Retreat().Value;
            Assert.IsFalse(retreat.Victory);
            Assert.IsFalse(_s.Progress.IsFightCleared("fight_2"));

            _s.Party.Remove(0);
            _s.Party.Remove(1);
            Assert.AreEqual(ErrorCode.InvalidArgument, _s.Battles.Start("fight_1").Code);
            Assert.AreEqual(ErrorCode.FightLocked, _s.Battles.CanStart("fight_4").Code);
        }

        [Test]
        public void BattleFlow_SameSeed_SameOutcome_SkipEqualsStepping()
        {
            var a = _s.Battles.Start("fight_1", 42).Value;
            while (a.Simulator.Step()) { }
            var stepped = BattleSimulator.Hash(a.Simulator.Events);
            _s.Battles.Finish();
            var b = _s.Battles.Start("fight_1", 42).Value;
            var skipped = _s.Battles.Finish().Value;
            Assert.AreEqual(stepped, BattleSimulator.Hash(b.Simulator.Events));
            Assert.AreEqual(BattleOutcome.Victory, skipped.Battle.Outcome);
        }

        [Test]
        public void DebugConsole_PlaysWholeCampaignInOrder()
        {
            var dbg = new DebugCommands(_s);
            StringAssert.Contains("Victory", dbg.Execute("fight fight_1 3"));
            StringAssert.StartsWith("Failed", dbg.Execute("fight fight_4"));
            StringAssert.Contains("Cleared 4", dbg.Execute("clear all"));
            Assert.IsTrue(_s.Db.Fights.All(f => _s.Progress.IsFightCleared(f.Id)));
        }
    }
}
