using System;
using Game.Core;
using Game.Save;
using Game.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class TutorialTests
    {
        [Test]
        public void NewGame_StartsTutorial_AdvancesOnlyOnTheRightAnchor()
        {
            var s = TestSession.Create();
            Assert.IsFalse(s.Tutorial.IsComplete);
            Assert.AreEqual("home_continue", s.Tutorial.Current.Anchor);
            var changes = 0;
            s.Bus.Subscribe<TutorialChanged>(_ => changes++);
            Assert.IsFalse(s.Tutorial.OnAnchorTapped("tab_heroes"));
            Assert.IsTrue(s.Tutorial.OnAnchorTapped("home_continue"));
            Assert.AreEqual("preview_start", s.Tutorial.Current.Anchor);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Progress_IsSaved_AndResumesAfterRestart()
        {
            var s = TestSession.Create(out var saver);
            s.Tutorial.OnAnchorTapped("home_continue");
            s.Tutorial.OnAnchorTapped("preview_start");
            Assert.Greater(saver.DirtyMarks, 0);
            var ser = new SaveSerializer();
            var data = ser.Decode(ser.Encode(s.ExportForSave(), 1, DateTime.UtcNow)).Data;
            var again = new GameSession(s.Db, data, null, new SeededRandom(1), new ManualClock());
            Assert.AreEqual(2, again.Tutorial.StepIndex);
            Assert.AreEqual("result_home", again.Tutorial.Current.Anchor);
        }

        [Test]
        public void FullScript_Completes_SkipAndRestartWork()
        {
            var s = TestSession.Create();
            foreach (var step in TutorialService.Steps) Assert.IsTrue(s.Tutorial.OnAnchorTapped(step.Anchor), step.Id);
            Assert.IsTrue(s.Tutorial.IsComplete);
            Assert.IsNull(s.Tutorial.Current);
            s.Tutorial.Restart();
            Assert.AreEqual(0, s.Tutorial.StepIndex);
            s.Tutorial.Skip();
            Assert.IsTrue(s.Tutorial.IsComplete);
        }

        [Test]
        public void Tips_OnlyAfterTutorial_AndOnce()
        {
            var s = TestSession.Create();
            Assert.IsFalse(s.Tutorial.ShouldShowTip("tip_shards"), "no tips during the tutorial");
            s.Tutorial.Skip();
            Assert.IsTrue(s.Tutorial.ShouldShowTip("tip_shards"));
            s.Tutorial.MarkTipSeen("tip_shards");
            Assert.IsFalse(s.Tutorial.ShouldShowTip("tip_shards"));
            Assert.IsTrue(s.Tutorial.ShouldShowTip("tip_starup"));
        }

        [Test]
        public void ScriptAnchors_AreUnique()
        {
            Assert.AreEqual(TutorialService.Steps.Count, new System.Collections.Generic.HashSet<string>(System.Linq.Enumerable.Select(TutorialService.Steps, x => x.Id)).Count);
        }

        [Test]
        public void SaveV3Fixture_ExistingPlayersSkipTutorial()
        {
            var r = new SaveSerializer().Decode(Fixtures.Read("save_v3.json"));
            Assert.AreEqual(DecodeStatus.Ok, r.Status, r.Error);
            Assert.AreEqual(SaveVersions.Current, r.Data.Version);
            Assert.IsTrue(r.Data.Tutorial.Skipped, "players who already fought are not forced through onboarding");
            Assert.AreEqual("", r.Data.Daily.LastClaimDate);
            Assert.AreEqual(2, r.Data.Pity.FightsWithoutShard);
        }

        private static GameSession DailySession()
        {
            var db = TestContent.Create();
            db.Rules.DailyBonusEnabled = true;
            db.Rules.DailyBonusTableId = "drops_first";
            return TestSession.Create(db);
        }

        [Test]
        public void Daily_DisabledByDefault()
        {
            var s = TestSession.Create();
            Assert.IsFalse(s.Daily.CanClaim(DateTime.Now));
            Assert.AreEqual(ErrorCode.FeatureDisabled, s.Daily.Claim(DateTime.Now).Code);
        }

        [Test]
        public void Daily_OncePerDay_StreakAndClockTamper()
        {
            var s = DailySession();
            var day1 = new DateTime(2026, 11, 1, 9, 0, 0);
            Assert.IsTrue(s.Daily.Claim(day1).Ok);
            Assert.AreEqual(150, s.Currency.Get(TestContent.Gold));
            Assert.AreEqual(1, s.Daily.Streak);
            Assert.AreEqual(ErrorCode.AlreadyClaimed, s.Daily.Claim(day1.AddHours(10)).Code, "same day");
            Assert.AreEqual(ErrorCode.AlreadyClaimed, s.Daily.Claim(day1.AddDays(-3)).Code, "clock moved back");
            Assert.IsTrue(s.Daily.Claim(day1.AddDays(1)).Ok);
            Assert.AreEqual(2, s.Daily.Streak);
            Assert.IsTrue(s.Daily.Claim(day1.AddDays(5)).Ok, "gap allowed");
            Assert.AreEqual(1, s.Daily.Streak, "gap resets the streak");
            Assert.AreEqual(ErrorCode.AlreadyClaimed, s.Daily.Claim(day1.AddDays(4)).Code, "back to an earlier day after a forward jump");
        }
    }
}
