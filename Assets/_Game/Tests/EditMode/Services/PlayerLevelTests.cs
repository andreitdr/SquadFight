using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Save;
using Game.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Player level: XP from defeated enemies by rank, level-ups and their rewards, saves.</summary>
    public class PlayerLevelTests
    {
        /// <summary>Fixture content plus 4 levels (10, 20, 30 XP; level 4 is the cap), each worth 25 gold.</summary>
        private static GameDatabase Db()
        {
            var db = TestContent.Create();
            db.DropTables.Add(new DropTableDef { Id = "level_gold", Guaranteed = { new DropEntryDef { Type = DropType.Currency, RefId = TestContent.Gold, AmountMin = 25, AmountMax = 25 } } });
            db.PlayerLevels = new List<PlayerLevelDef>
            {
                new PlayerLevelDef { Level = 1, XpToNext = 10 },
                new PlayerLevelDef { Level = 2, XpToNext = 20, RewardTableId = "level_gold" },
                new PlayerLevelDef { Level = 3, XpToNext = 30, RewardTableId = "level_gold" },
                new PlayerLevelDef { Level = 4, RewardTableId = "level_gold" },
            };
            return db.Index();
        }

        private static BattleResult Result(GameDatabase db, BattleOutcome outcome, params (string id, bool survived)[] enemies)
        {
            var r = new BattleResult { Outcome = outcome };
            foreach (var (id, survived) in enemies) r.Combatants.Add(new CombatantStats { DefId = id, Team = Team.Enemy, Survived = survived });
            r.Combatants.Add(new CombatantStats { DefId = "hero_a", Team = Team.Player, Survived = true });
            return r;
        }

        [Test]
        public void Xp_ComesFromDefeatedEnemies_ByRank()
        {
            var db = Db();
            var minion = db.GetRank(EnemyRank.Minion).Xp;
            var boss = db.GetRank(EnemyRank.Boss).Xp;
            Assert.Greater(boss, minion, "bosses give more experience");
            Assert.AreEqual(2 * minion + boss, PlayerLevelRules.XpForBattle(Result(db, BattleOutcome.Victory, ("enemy_x", false), ("enemy_x", false), ("enemy_boss", false)), db));
            Assert.AreEqual(minion, PlayerLevelRules.XpForBattle(Result(db, BattleOutcome.Defeat, ("enemy_x", false), ("enemy_boss", true)), db), "a defeat still pays for what was defeated");
            Assert.AreEqual(2 * minion + boss, PlayerLevelRules.XpForFight(db.GetFight("fight_3"), db));
        }

        [Test]
        public void AddXp_CanReachSeveralLevels_AndStopsAtTheCap()
        {
            var db = Db();
            int level = 1, xp = 0;
            CollectionAssert.AreEqual(new[] { 2, 3 }, PlayerLevelRules.AddXp(ref level, ref xp, 35, db));
            Assert.AreEqual(3, level);
            Assert.AreEqual(5, xp);
            CollectionAssert.AreEqual(new[] { 4 }, PlayerLevelRules.AddXp(ref level, ref xp, 1000, db));
            Assert.AreEqual(4, level);
            Assert.AreEqual(0, xp, "no XP is kept at the cap");
            CollectionAssert.IsEmpty(PlayerLevelRules.AddXp(ref level, ref xp, 50, db));
        }

        [Test]
        public void LevelUp_GrantsEachLevelsReward_InOneSave()
        {
            var s = TestSession.Create(out var saver, Db());
            var gold = s.Currency.Get(TestContent.Gold);
            var r = s.Levels.AddXp(35);
            Assert.AreEqual(1, r.LevelBefore);
            Assert.AreEqual(3, r.LevelAfter);
            CollectionAssert.AreEqual(new[] { 2, 3 }, r.LevelsReached);
            Assert.AreEqual(gold + 50, s.Currency.Get(TestContent.Gold), "levels 2 and 3 pay 25 gold each");
            Assert.AreEqual(35, s.Levels.TotalXp);
            Assert.AreEqual(5, s.Levels.Xp);
            Assert.AreEqual(1, saver.Saves, "level-ups save at once");
        }

        [Test]
        public void CompletingAFight_GivesXp_AndLevelRewards()
        {
            var s = TestSession.Create(Db());
            var fired = new List<PlayerLevelChanged>();
            s.Bus.Subscribe<PlayerLevelChanged>(e => fired.Add(e));
            var c = s.Battles.RunInstant("fight_1", seed: 3).Value;
            Assert.IsTrue(c.Victory);
            Assert.AreEqual(s.Db.GetRank(EnemyRank.Minion).Xp, c.Level.XpGained);
            Assert.AreEqual(c.Level.XpGained, s.State.Data.Player.TotalXp);
            Assert.AreEqual(1, fired.Count);
        }

        [Test]
        public void RetreatAtTheStart_GivesNoXp()
        {
            var s = TestSession.Create(Db());
            Assert.IsTrue(s.Battles.Start("fight_1", seed: 3).Ok);
            var c = s.Battles.Retreat().Value;
            Assert.IsFalse(c.Victory);
            Assert.AreEqual(0, c.Level.XpGained);
            Assert.AreEqual(1, s.Levels.Level);
        }

        [Test]
        public void FailedRewardApply_RollsBackTheXpToo()
        {
            var db = Db();
            db.GetDropTable("level_gold").Guaranteed.Add(new DropEntryDef { Type = DropType.HeroShard, RefId = "hero_missing", AmountMin = 1, AmountMax = 1 });
            var s = TestSession.Create(db);
            Assert.Throws<System.InvalidOperationException>(() =>
            {
                using (var tx = s.Context.BeginTransaction()) { s.Levels.AddXp(15); tx.Complete(); }
            });
            Assert.AreEqual(1, s.Levels.Level);
            Assert.AreEqual(0, s.Levels.TotalXp);
        }

        [Test]
        public void V5Save_MigratesToLevelOne()
        {
            var payload = JObject.Parse("{\"version\":5,\"progress\":{\"totalFights\":12}}");
            MigrationRunner.Default().Migrate(payload, 5, SaveVersions.Current);
            Assert.AreEqual(1, (int)payload["player"]["level"]);
            Assert.AreEqual(0, (int)payload["player"]["xp"]);
        }

        [Test]
        public void Reconciler_ClampsLevelAndXp_ToTheContent()
        {
            var db = Db();
            var data = NewGameFactory.Create(db, new ManualClock());
            data.Player.Level = 9;
            data.Player.Xp = 3;
            var fixes = SaveReconciler.Reconcile(data, db);
            Assert.AreEqual(4, data.Player.Level);
            Assert.AreEqual(0, data.Player.Xp);
            Assert.IsTrue(fixes.Any(f => f.Contains("level")));
            data.Player.Level = 2;
            data.Player.Xp = 50;
            SaveReconciler.Reconcile(data, db);
            Assert.AreEqual(19, data.Player.Xp, "XP stays below what the level needs");
        }
    }
}
