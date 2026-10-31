using System.Linq;
using Game.Core;
using Game.Services;
using Game.ViewModels;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Side quests: optional zones that never count toward chapter completion, stars or the unlock chain.</summary>
    public class SideQuestTests
    {
        private static BattleResult Win() => new BattleResult { Outcome = BattleOutcome.Victory, Rounds = 3 };

        /// <summary>Fixture + side_1 (opens after fight_1) and side_2 (opens after fight_3) in chapter_1.</summary>
        private static GameDatabase Db()
        {
            var db = TestContent.Create();
            var s1 = TestContent.Fight("side_1", 1, "drops_first");
            s1.IsSideQuest = true;
            s1.UnlockConditions.Add(new UnlockConditionDef { Type = UnlockConditionType.ClearFight, RefId = "fight_1" });
            s1.PostBeatIds.Add("beat_lore");
            var s2 = TestContent.Fight("side_2", 1, null);
            s2.IsSideQuest = true;
            s2.UnlockConditions.Add(new UnlockConditionDef { Type = UnlockConditionType.ClearFight, RefId = "fight_3" });
            db.Fights.Add(s1);
            db.Fights.Add(s2);
            db.StoryBeats.Add(new StoryBeatDef { Id = "beat_lore", SpeakerKey = "k", TextKey = "k" });
            db.Chapters[0].SideQuestIds.Add("side_1");
            db.Chapters[0].SideQuestIds.Add("side_2");
            return db.Index();
        }

        [Test]
        public void SideQuest_BelongsToChapter_AfterMainPathInOrder()
        {
            var db = Db();
            Assert.AreEqual("chapter_1", db.GetFight("side_1").ChapterId);
            Assert.Greater(db.GetFightOrder("side_1"), db.GetFightOrder("fight_4"), "side quests come after every main fight");
            Assert.IsFalse(ConfigValidation.Validate(db).HasErrors, ConfigValidation.Validate(db).ToString());
        }

        [Test]
        public void SideQuest_OpensOnlyByItsCondition()
        {
            var s = TestSession.Create(Db());
            Assert.IsFalse(s.Progress.IsFightUnlocked("side_1"));
            var c = s.Progress.CompleteFight("fight_1", Win()).Value;
            CollectionAssert.AreEquivalent(new[] { "fight_2", "side_1" }, c.NewlyUnlockedFights);
            Assert.IsFalse(s.Progress.IsFightUnlocked("side_2"));
            Assert.AreEqual(ErrorCode.FightLocked, s.Progress.CompleteFight("side_2", Win()).Code);
        }

        [Test]
        public void SideQuest_DoesNotCountTowardCompletionOrStars()
        {
            var s = TestSession.Create(Db());
            s.Progress.CompleteFight("fight_1", Win());
            s.Progress.CompleteFight("side_1", Win());
            Assert.AreEqual(3, s.Progress.ChapterStars("chapter_1"), "only fight_1's stars count");
            s.Progress.CompleteFight("fight_2", Win());
            var last = s.Progress.CompleteFight("fight_3", Win()).Value;
            Assert.AreEqual("chapter_1", last.ChapterCompletedId, "the chapter completes without side_2");
            Assert.IsTrue(s.Progress.IsChapterCleared("chapter_1"));
            Assert.IsTrue(s.Progress.IsChapterUnlocked("chapter_2"));
            var side = s.Progress.CompleteFight("side_2", Win()).Value;
            Assert.IsNull(side.ChapterCompletedId, "clearing a side quest never re-completes the chapter");
        }

        [Test]
        public void SideQuest_ClearingItDoesNotOpenTheNextMainFight()
        {
            var db = Db();
            db.GetFight("side_1").UnlockConditions.Clear();
            db.GetFight("side_1").UnlockConditions.Add(new UnlockConditionDef { Type = UnlockConditionType.HeroOwned, RefId = "hero_a" });
            var s = TestSession.Create(db);
            s.Progress.CompleteFight("side_1", Win());
            Assert.IsFalse(s.Progress.IsFightUnlocked("fight_2"));
            Assert.AreEqual("fight_1", s.Progress.GetNextRecommendedFight(), "side quests are never the recommended next fight");
        }

        [Test]
        public void Campaign_ShowsSideQuestsSeparately_WithUnlockHint_AndJournal()
        {
            var s = TestSession.Create(Db());
            using (var vm = new CampaignViewModel(s))
            {
                CollectionAssert.AreEqual(new[] { "fight_1", "fight_2", "fight_3" }, vm.Fights.Select(f => f.Def.Id).ToArray());
                CollectionAssert.AreEqual(new[] { "side_1", "side_2" }, vm.SideQuests.Select(f => f.Def.Id).ToArray());
                Assert.AreEqual("fight_1", vm.SideQuests[0].UnlockedBy.Id);
                Assert.AreEqual(FightState.Locked, vm.SideQuests[0].State);
                Assert.AreEqual(9, vm.Chapters[0].MaxStars, "max stars ignore side quests");
                s.Progress.CompleteFight("fight_1", Win());
                s.Progress.CompleteFight("side_1", Win());
                s.Progress.MarkBeatsSeen(new[] { "beat_1", "beat_lore" });
                CollectionAssert.AreEqual(new[] { "beat_1", "beat_lore" }, vm.ChapterJournal.Select(b => b.Id).ToArray());
            }
        }

        [Test]
        public void Validator_SideQuestRules()
        {
            var db = Db();
            db.GetFight("side_2").UnlockConditions.Clear();
            Assert.IsTrue(ConfigValidation.Validate(db).Errors.Any(e => e.Message.Contains("side quest needs an unlock condition")));

            db = Db();
            db.Chapters[0].FightIds.Add("side_2");
            db.Chapters[0].SideQuestIds.Remove("side_2");
            Assert.IsTrue(ConfigValidation.Validate(db).Errors.Any(e => e.Message.Contains("is in the main fight path")));

            db = Db();
            db.Chapters[0].SideQuestIds.Add("fight_4");
            Assert.IsTrue(ConfigValidation.Validate(db).Errors.Any(e => e.Message.Contains("also listed")));
        }
    }
}
