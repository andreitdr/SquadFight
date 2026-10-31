using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Enemy ranks: rank HP, extra skills, boss specials, and the validator rules that keep ranks meaningful.</summary>
    public class EnemyRankTests
    {
        private static readonly CombatRules Rules = new CombatRules { MinDamage = 1, DefenseK = 1, MaxRounds = 100, CritChanceCap = 1, AiTargeting = AiTargeting.LowestHp, PowerWeights = new StatBlock((StatType.Attack, 1f)) };

        private static SkillDef Skill(string id, TargetingRule t, string effects, int cooldown = 0, int initial = 0, float threshold = 0) =>
            new SkillDef { Id = id, NameKey = id, Targeting = t, Cooldown = cooldown, InitialCooldown = initial, Threshold = threshold, Effects = EffectSpec.Parse(effects) };

        private static readonly SkillDef Hit = Skill("hit", TargetingRule.SingleEnemy, "Damage:1");

        private static CombatantSetup Unit(string id, Team team, int slot, float hp, float atk, float spd, SkillDef active = null, List<SkillDef> extras = null, List<SkillDef> specials = null) => new CombatantSetup
        {
            DefId = id, Team = team, Slot = slot, PreStats = TestContent.Stats(hp, atk, 0, spd), Basic = Hit, Active = active,
            Extras = extras ?? new List<SkillDef>(), Specials = specials ?? new List<SkillDef>(), IsBoss = specials != null, Rank = specials != null ? EnemyRank.Boss : EnemyRank.Soldier,
        };

        private static BattleSimulator Sim(CombatantSetup player, params CombatantSetup[] enemies)
        {
            var s = new BattleSetup { Rules = Rules };
            s.Players.Add(player);
            s.EnemyWaves.Add(enemies.ToList());
            return new BattleSimulator(s, new SeededRandom(7));
        }

        private static List<BattleEvent> Of(BattleSimulator sim, BattleEventType t) => sim.Events.Where(e => e.Type == t).ToList();

        private static void Turns(BattleSimulator sim, int n) { for (var i = 0; i < n && sim.Step(); i++) { } }

        // ---------------------------------------------------------------- rank HP

        [Test]
        public void RankMultipliesMaxHp_AfterTheSpawnMultiplier()
        {
            var db = TestContent.Create();
            var boss = db.GetEnemy("enemy_boss");
            var setup = BattleSetupBuilder.Enemy(boss, 1.5f, 0, db);
            Assert.AreEqual(750 * 1.5f * db.GetRank(EnemyRank.Boss).HpMultiplier, setup.PreStats[StatType.MaxHp], 0.01f);
            Assert.AreEqual(60 * 1.5f, setup.PreStats[StatType.Attack], 0.01f, "rank changes HP only");
            Assert.AreEqual(EnemyRank.Boss, setup.Rank);
            Assert.AreEqual(1, setup.Extras.Count);
            Assert.AreEqual(1, setup.Specials.Count);
        }

        [Test]
        public void MissingRankRow_IsNeutral()
        {
            var db = TestContent.Create();
            db.Ranks.Clear();
            Assert.AreEqual(1f, db.GetRank(EnemyRank.Elite).HpMultiplier);
            Assert.AreEqual(0, db.GetRank(EnemyRank.Elite).Xp);
        }

        // ---------------------------------------------------------------- extra skills

        [Test]
        public void ExtraSkill_IsUsedWhileTheActiveIsOnCooldown_WithItsOwnCooldown()
        {
            var active = Skill("active", TargetingRule.SingleEnemy, "Damage:0.1", cooldown: 3);
            var extra = Skill("extra", TargetingRule.SingleEnemy, "Damage:0.1", cooldown: 3);
            var sim = Sim(Unit("p", Team.Player, 0, 100000, 1, 50), Unit("e", Team.Enemy, 0, 100000, 10, 100, active, new List<SkillDef> { extra }));
            Turns(sim, 12);
            var used = Of(sim, BattleEventType.ActionChosen).Where(e => e.Actor == 1).Select(e => e.SkillId).ToList();
            Assert.AreEqual("active", used[0]);
            Assert.AreEqual("extra", used[1], "the active is on cooldown: the extra skill comes next");
            Assert.AreEqual("hit", used[2], "both on cooldown: basic attack");
            Assert.AreEqual("active", used[3]);
            Assert.AreEqual("extra", used[4]);
        }

        [Test]
        public void Silence_BlocksExtraSkills()
        {
            var extra = Skill("extra", TargetingRule.SingleEnemy, "Damage:0.1", cooldown: 2);
            var mute = Skill("mute", TargetingRule.SingleEnemy, "Silence:2", cooldown: 9);
            var sim = Sim(Unit("p", Team.Player, 0, 100000, 1, 200, mute), Unit("e", Team.Enemy, 0, 100000, 10, 100, null, new List<SkillDef> { extra }));
            Turns(sim, 2);
            Assert.AreEqual(1, Of(sim, BattleEventType.Silenced).Count(e => e.Actor == 1 && e.SkillId == "extra"));
            Assert.AreEqual("hit", Of(sim, BattleEventType.ActionChosen).Last(e => e.Actor == 1).SkillId);
        }

        [Test]
        public void CooldownShift_MovesExtraSkillsToo()
        {
            var extra = Skill("extra", TargetingRule.SingleEnemy, "Damage:0.1", cooldown: 2);
            var delay = Skill("delay", TargetingRule.SingleEnemy, "Cooldown:3", cooldown: 9);
            var sim = Sim(Unit("p", Team.Player, 0, 100000, 1, 200, delay), Unit("e", Team.Enemy, 0, 100000, 10, 100, null, new List<SkillDef> { extra }));
            Turns(sim, 1);
            Assert.AreEqual(3, sim.Get(1).ExtraCooldowns[0]);
            Assert.AreEqual(1, Of(sim, BattleEventType.CooldownChanged).Count(e => e.Target == 1));
            Turns(sim, 1);
            Assert.AreEqual("hit", Of(sim, BattleEventType.ActionChosen).Last(e => e.Actor == 1).SkillId);
        }

        // ---------------------------------------------------------------- boss specials

        [Test]
        public void Special_ChargesAtItsThreshold_AndFiresOnTheNextTurn_Once()
        {
            var quake = Skill("quake", TargetingRule.AllEnemies, "Damage:0.01", threshold: 0.5f);
            var boss = Unit("boss", Team.Enemy, 0, 1000, 10, 50, specials: new List<SkillDef> { quake });
            var sim = Sim(Unit("p", Team.Player, 0, 100000, 120, 100), boss);
            var charged = -1;
            while (sim.Step() && charged < 0)
                charged = sim.Events.ToList().FindIndex(e => e.Type == BattleEventType.SpecialCharged);
            Assert.GreaterOrEqual(charged, 0, "charged");
            Assert.LessOrEqual(sim.Get(1).HpRatio, 0.5f);
            Turns(sim, 3);
            var actions = Of(sim, BattleEventType.ActionChosen).Where(e => e.Actor == 1).ToList();
            Assert.AreEqual(1, actions.Count(a => a.SkillId == "quake"), "fires exactly once");
            var fired = sim.Events.ToList().FindIndex(e => e.Type == BattleEventType.ActionChosen && e.SkillId == "quake");
            Assert.Greater(fired, charged);
            Assert.IsFalse(sim.Events.Skip(charged + 1).Take(fired - charged).Any(e => e.Type == BattleEventType.ActionChosen && e.Actor == 1 && e.SkillId != "quake"), "the boss's next action is the special");
        }

        [Test]
        public void Special_IgnoresSilence_ButAStunDelaysIt()
        {
            var quake = Skill("quake", TargetingRule.AllEnemies, "Damage:0.01", threshold: 0.99f);
            var shut = Skill("shut", TargetingRule.SingleEnemy, "Damage:0.2|Silence:3|Stun:1", cooldown: 99);
            var boss = Unit("boss", Team.Enemy, 0, 1000, 10, 50, specials: new List<SkillDef> { quake });
            var sim = Sim(Unit("p", Team.Player, 0, 100000, 100, 100, shut), boss);
            Turns(sim, 2); // hero: charge + silence + stun; boss: stunned
            Assert.AreEqual(1, Of(sim, BattleEventType.SpecialCharged).Count);
            Assert.AreEqual(1, Of(sim, BattleEventType.Stunned).Count(e => e.Actor == 1));
            Assert.IsFalse(Of(sim, BattleEventType.ActionChosen).Any(e => e.SkillId == "quake"));
            Turns(sim, 2);
            Assert.AreEqual(1, Of(sim, BattleEventType.ActionChosen).Count(e => e.SkillId == "quake"), "silenced, yet the special fires");
        }

        [Test]
        public void TwoThresholdsCrossedAtOnce_QueueBothSpecials()
        {
            var a = Skill("a", TargetingRule.AllEnemies, "Damage:0.01", threshold: 0.6f);
            var b = Skill("b", TargetingRule.AllEnemies, "Damage:0.01", threshold: 0.3f);
            var nuke = Skill("nuke", TargetingRule.SingleEnemy, "Damage:8", cooldown: 99);
            var sim = Sim(Unit("p", Team.Player, 0, 100000, 100, 100, nuke), Unit("boss", Team.Enemy, 0, 1000, 10, 50, specials: new List<SkillDef> { a, b }));
            Turns(sim, 6);
            CollectionAssert.AreEqual(new[] { "a", "b" }, Of(sim, BattleEventType.SpecialCharged).Select(e => e.SkillId).ToArray());
            CollectionAssert.AreEqual(new[] { "a", "b" }, Of(sim, BattleEventType.ActionChosen).Where(e => e.Actor == 1).Select(e => e.SkillId).Take(2).ToArray());
        }

        [Test]
        public void BossFight_IsDeterministic()
        {
            var db = TestContent.Create();
            string Run()
            {
                var s = new BattleSetup { Rules = db.Combat };
                s.Players.Add(BattleSetupBuilder.Hero(db.GetHero("hero_e"), 1, null, 0, db));
                s.Players.Add(BattleSetupBuilder.Hero(db.GetHero("hero_a"), 1, null, 1, db));
                s.EnemyWaves.Add(new List<CombatantSetup> { BattleSetupBuilder.Enemy(db.GetEnemy("enemy_boss"), 1f, 0, db), BattleSetupBuilder.Enemy(db.GetEnemy("enemy_x"), 1f, 1, db) });
                var sim = new BattleSimulator(s, new SeededRandom(42));
                sim.RunToEnd();
                Assert.IsTrue(sim.Events.Any(e => e.Type == BattleEventType.SpecialCharged), "the fixture boss reaches its special");
                return BattleSimulator.Hash(sim.Events);
            }
            Assert.AreEqual(Run(), Run());
        }

        // ---------------------------------------------------------------- validator

        private static void AssertError(GameDatabase db, string fragment)
        {
            var r = ConfigValidation.Validate(db);
            Assert.IsTrue(r.Errors.Any(e => (e.Context + " " + e.Message).Contains(fragment)), $"Expected an error containing '{fragment}'.\n{r}");
        }

        [Test]
        public void Validator_RankNeedsItsSkillCount()
        {
            var db = TestContent.Create();
            db.GetEnemy("enemy_x").Rank = EnemyRank.Elite;
            AssertError(db, "Elite needs at least 2 skill(s)");
            db = TestContent.Create();
            db.GetEnemy("enemy_x").ActiveSkillId = "skill_aoe";
            AssertError(db, "Minion must have fewer than 1 skill(s)");
        }

        [Test]
        public void Validator_OnlyBossesHaveSpecials_AndEveryBossHasOne()
        {
            var db = TestContent.Create();
            var boss = db.GetEnemy("enemy_boss");
            boss.SpecialSkillIds.Clear();
            boss.ExtraSkillIds.Add("skill_heal");
            AssertError(db, "a boss needs at least one special attack");

            db = TestContent.Create();
            db.GetEnemy("enemy_boss").Rank = EnemyRank.Champion;
            AssertError(db, "only bosses have special attacks");
        }

        [Test]
        public void Validator_SpecialNeedsAThreshold()
        {
            var db = TestContent.Create();
            db.GetSkill("special_boss_quake").Threshold = 0;
            AssertError(db, "needs an HP threshold");
        }

        [Test]
        public void Validator_RanksMustClimb()
        {
            var db = TestContent.Create();
            db.Ranks.Single(r => r.Rank == EnemyRank.Champion).HpMultiplier = 1f;
            AssertError(db, "hpMultiplier must be higher than Elite's");
            db = TestContent.Create();
            db.Ranks.Single(r => r.Rank == EnemyRank.Boss).Xp = 1;
            AssertError(db, "xp must be higher than Champion's");
            db = TestContent.Create();
            db.Ranks.RemoveAll(r => r.Rank == EnemyRank.Soldier);
            AssertError(db, "rank:Soldier");
        }

        [Test]
        public void Validator_HigherRankInAWave_NeedsMoreHp()
        {
            var db = TestContent.Create();
            // A Minion at ×10 next to the boss at ×1: the Minion would have more HP than the Boss.
            db.GetFight("fight_3").Waves[1].Spawns.Add(new EnemySpawnDef { EnemyId = "enemy_x", StatMultiplier = 10f });
            AssertError(db, "must have more HP than Minion");
        }

        [Test]
        public void Validator_PlayerLevelsWithoutGaps()
        {
            var db = TestContent.Create();
            db.PlayerLevels.Add(new PlayerLevelDef { Level = 1, XpToNext = 10 });
            db.PlayerLevels.Add(new PlayerLevelDef { Level = 3, XpToNext = 10, RewardTableId = "nope" });
            var r = ConfigValidation.Validate(db);
            Assert.IsTrue(r.Errors.Any(e => e.Message.Contains("gap")), r.ToString());
            Assert.IsTrue(r.Errors.Any(e => e.Message.Contains("unknown reward table")), r.ToString());
        }
    }
}
