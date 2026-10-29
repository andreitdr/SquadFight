using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class CombatTests
    {
        private static readonly CombatRules Rules = new CombatRules { MinDamage = 1, DefenseK = 1, MaxRounds = 100, CritChanceCap = 1, AiTargeting = AiTargeting.LowestHp, PowerWeights = new StatBlock((StatType.Attack, 1f)) };

        private static SkillDef Skill(string id, TargetingRule t, params EffectDef[] effects) => new SkillDef { Id = id, NameKey = id, Targeting = t, Effects = effects.ToList() };
        private static EffectDef Dmg(float p) => new EffectDef { Type = EffectType.Damage, Power = p };
        private static readonly SkillDef Hit = Skill("hit", TargetingRule.SingleEnemy, Dmg(1));

        private static CombatantSetup Unit(string id, Team team, int slot, float hp, float atk, float def, float spd, float crit = 0, SkillDef basic = null, SkillDef active = null) => new CombatantSetup
        {
            DefId = id, Team = team, Slot = slot, PreStats = TestContent.Stats(hp, atk, def, spd, crit, 1.5f), Basic = basic ?? Hit, Active = active,
        };

        private static BattleSetup Setup(IEnumerable<CombatantSetup> players, params CombatantSetup[][] waves)
        {
            var s = new BattleSetup { Rules = Rules };
            s.Players.AddRange(players);
            foreach (var w in waves) s.EnemyWaves.Add(w.ToList());
            return s;
        }

        [Test]
        public void Scripted1v1_MatchesHandCalculation()
        {
            // hero 50 atk vs 10 def: 50×100/110 = 45.45 → 45 per hit; 300 HP → 7 hits.
            // enemy 30 atk vs 20 def: 30×100/120 = 25 per hit; hero acts first each round, so it takes 6 hits.
            var sim = new BattleSimulator(Setup(new[] { Unit("hero", Team.Player, 0, 500, 50, 20, 100) }, new[] { Unit("foe", Team.Enemy, 0, 300, 30, 10, 90) }), new SeededRandom(1));
            var result = sim.RunToEnd();
            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            Assert.AreEqual(7, result.Rounds);
            Assert.AreEqual(500 - 6 * 25, sim.Players[0].Hp);
            var hits = sim.Events.Where(e => e.Type == BattleEventType.DamageDealt && e.Actor == 0).Select(e => e.Amount).ToList();
            Assert.AreEqual(7, hits.Count);
            Assert.IsTrue(hits.All(h => h == 45));
            Assert.AreEqual(300, result.Combatants[0].DamageDealt, "overkill not counted");
            Assert.AreEqual(150, result.Combatants[0].DamageTaken);
            var log = BattleLog.Format(sim);
            StringAssert.Contains("foe[E0] takes 45 → 255 HP", log);
            StringAssert.Contains("Victory after 7 round(s)", log);
        }

        [Test]
        public void TurnOrder_SpeedThenSlotThenId()
        {
            var sim = new BattleSimulator(Setup(new[]
            {
                Unit("b", Team.Player, 1, 999, 1, 0, 100),
                Unit("a", Team.Player, 2, 999, 1, 0, 100),
                Unit("fast", Team.Player, 3, 999, 1, 0, 150),
            }, new[] { Unit("e", Team.Enemy, 1, 9999, 1, 0, 100), Unit("d", Team.Enemy, 0, 9999, 1, 0, 50) }), new SeededRandom(1));
            for (var i = 0; i < 5; i++) sim.Step();
            var actors = sim.Events.Where(e => e.Type == BattleEventType.TurnStarted).Select(e => sim.Get(e.Actor).DefId).ToList();
            CollectionAssert.AreEqual(new[] { "fast", "b", "e", "a", "d" }, actors);
        }

        [Test]
        public void SpeedBuff_ChangesOrderNextRound()
        {
            var haste = Skill("haste", TargetingRule.Self, new EffectDef { Type = EffectType.ApplyStatus, Stat = StatType.Speed, Value = 1f, Duration = 3 });
            var sim = new BattleSimulator(Setup(new[] { Unit("slow", Team.Player, 0, 999, 1, 0, 50, active: haste) }, new[] { Unit("e", Team.Enemy, 0, 9999, 1, 0, 80) }), new SeededRandom(1));
            for (var i = 0; i < 4; i++) sim.Step();
            var actors = sim.Events.Where(e => e.Type == BattleEventType.TurnStarted).Select(e => sim.Get(e.Actor).DefId).ToList();
            CollectionAssert.AreEqual(new[] { "e", "slow", "slow", "e" }, actors, "100 speed beats 80 in round 2");
        }

        [Test]
        public void Targeting_AllRules()
        {
            var all = Skill("all", TargetingRule.AllEnemies, Dmg(1));
            var sim = new BattleSimulator(Setup(new[] { Unit("p", Team.Player, 0, 999, 10, 0, 200, active: all) },
                new[] { Unit("e0", Team.Enemy, 0, 999, 1, 0, 1), Unit("e1", Team.Enemy, 1, 999, 1, 0, 1), Unit("e2", Team.Enemy, 2, 999, 1, 0, 1) }), new SeededRandom(1));
            sim.Step();
            Assert.AreEqual(3, sim.Events.Count(e => e.Type == BattleEventType.DamageDealt));

            var rnd = Skill("rnd", TargetingRule.RandomEnemies, Dmg(1));
            rnd.TargetCount = 4;
            sim = new BattleSimulator(Setup(new[] { Unit("p", Team.Player, 0, 999, 10, 0, 200, active: rnd) },
                new[] { Unit("e0", Team.Enemy, 0, 999, 1, 0, 1), Unit("e1", Team.Enemy, 1, 999, 1, 0, 1) }), new SeededRandom(3));
            sim.Step();
            Assert.AreEqual(4, sim.Events.Count(e => e.Type == BattleEventType.DamageDealt));

            var heal = Skill("heal", TargetingRule.LowestHpAlly, new EffectDef { Type = EffectType.Heal, Power = 1 });
            var hurt = Unit("hurt", Team.Player, 1, 1000, 1, 0, 1);
            sim = new BattleSimulator(Setup(new[] { Unit("healer", Team.Player, 0, 1000, 50, 0, 200, active: heal), hurt }, new[] { Unit("e", Team.Enemy, 0, 9999, 1, 0, 100) }), new SeededRandom(1));
            sim.Players[1].Hp = 400;
            sim.Step();
            var h = sim.Events.Single(e => e.Type == BattleEventType.HealApplied);
            Assert.AreEqual(1, h.Target);
            Assert.AreEqual(50, h.Amount);

            var party = Skill("party", TargetingRule.AllAllies, new EffectDef { Type = EffectType.Heal, Power = 1 });
            var selfS = Skill("self", TargetingRule.Self, new EffectDef { Type = EffectType.Heal, Power = 1 });
            var ally = Skill("ally", TargetingRule.SingleAlly, new EffectDef { Type = EffectType.Heal, Power = 1 });
            foreach (var (skill, expected) in new[] { (party, 2), (selfS, 1), (ally, 1) })
            {
                sim = new BattleSimulator(Setup(new[] { Unit("c", Team.Player, 0, 1000, 10, 0, 200, active: skill), Unit("o", Team.Player, 1, 1000, 1, 0, 1) }, new[] { Unit("e", Team.Enemy, 0, 9999, 1, 0, 100) }), new SeededRandom(1));
                sim.Step();
                Assert.AreEqual(expected, sim.Events.Count(e => e.Type == BattleEventType.HealApplied), skill.Id);
                if (skill == selfS) Assert.AreEqual(0, sim.Events.Single(e => e.Type == BattleEventType.HealApplied).Target);
            }
        }

        [Test]
        public void AiTargetsLowestHpEnemy()
        {
            var sim = new BattleSimulator(Setup(new[] { Unit("p", Team.Player, 0, 999, 10, 0, 200) },
                new[] { Unit("full", Team.Enemy, 0, 999, 1, 0, 1), Unit("weak", Team.Enemy, 1, 500, 1, 0, 1) }), new SeededRandom(1));
            sim.Step();
            Assert.AreEqual("weak", sim.Get(sim.Events.First(e => e.Type == BattleEventType.DamageDealt).Target).DefId);
        }

        [Test]
        public void Shield_AbsorbsThenExpires()
        {
            var shield = Skill("shield", TargetingRule.Self, new EffectDef { Type = EffectType.Shield, Power = 1, Duration = 1 });
            var sim = new BattleSimulator(Setup(new[] { Unit("p", Team.Player, 0, 1000, 30, 0, 200, active: shield) }, new[] { Unit("e", Team.Enemy, 0, 9999, 50, 0, 100) }), new SeededRandom(1));
            sim.Step(); // shield 30, applied on own turn: lasts through the next own turn
            sim.Step(); // enemy hits 50: 30 absorbed, 20 HP
            var hit = sim.Events.Last(e => e.Type == BattleEventType.DamageDealt);
            Assert.AreEqual(30, hit.Absorbed);
            Assert.AreEqual(980, sim.Players[0].Hp);
            Assert.AreEqual(0, sim.Players[0].Shield);
        }

        [Test]
        public void Stun_SkipsTurn()
        {
            var bash = Skill("bash", TargetingRule.SingleEnemy, Dmg(1), new EffectDef { Type = EffectType.Stun, Duration = 1 });
            var sim = new BattleSimulator(Setup(new[] { Unit("p", Team.Player, 0, 1000, 10, 0, 200, active: bash) }, new[] { Unit("e", Team.Enemy, 0, 9999, 10, 0, 100) }), new SeededRandom(1));
            sim.Step();
            sim.Step();
            Assert.AreEqual(BattleEventType.Stunned, sim.Events.Last(e => e.Type == BattleEventType.Stunned || e.Type == BattleEventType.ActionChosen).Type);
            Assert.AreEqual(1000, sim.Players[0].Hp);
        }

        [Test]
        public void Status_ModifiesStatAndExpires_StackingRules()
        {
            StatusEffect Buff(StackingRule rule, int maxStacks, int uses, out Combatant c)
            {
                var buff = Skill("buff", TargetingRule.Self, new EffectDef { Type = EffectType.ApplyStatus, Stat = StatType.Attack, Value = 0.5f, Duration = 2, Stacking = rule, MaxStacks = maxStacks });
                var sim = new BattleSimulator(Setup(new[] { Unit("p", Team.Player, 0, 1000, 100, 0, 200, active: buff) }, new[] { Unit("e", Team.Enemy, 0, 99999, 1, 0, 100) }), new SeededRandom(1));
                for (var i = 0; i < uses * 2 - 1; i++) sim.Step(); // player turn, enemy turn, ...
                c = sim.Players[0];
                return c.Statuses.FirstOrDefault();
            }
            Buff(StackingRule.Refresh, 1, 3, out var refreshed);
            Assert.AreEqual(1, refreshed.Statuses.Count);
            Assert.AreEqual(150, refreshed.Stat(StatType.Attack), 1e-3f);
            Buff(StackingRule.Stack, 2, 3, out var stacked);
            Assert.AreEqual(2, stacked.Statuses.Count, "capped at maxStacks");
            Assert.AreEqual(200, stacked.Stat(StatType.Attack), 1e-3f);
            Buff(StackingRule.Ignore, 1, 2, out var ignored);
            Assert.AreEqual(1, ignored.Statuses.Count);

            // Debuff on enemy expires after 2 of its turns.
            var weaken = Skill("weaken", TargetingRule.SingleEnemy, new EffectDef { Type = EffectType.ApplyStatus, Stat = StatType.Defense, Value = -0.5f, Duration = 2 });
            weaken.Cooldown = 10;
            var sim2 = new BattleSimulator(Setup(new[] { Unit("p", Team.Player, 0, 1000, 10, 0, 200, active: weaken) }, new[] { Unit("e", Team.Enemy, 0, 99999, 1, 100, 100) }), new SeededRandom(1));
            sim2.Step();
            Assert.AreEqual(50, sim2.Enemies[0].Stat(StatType.Defense), 1e-3f);
            for (var i = 0; i < 4; i++) sim2.Step();
            Assert.AreEqual(100, sim2.Enemies[0].Stat(StatType.Defense), 1e-3f);
            Assert.IsTrue(sim2.Events.Any(e => e.Type == BattleEventType.StatusExpired));
        }

        [Test]
        public void Cooldown_ActiveUsedEveryNTurns()
        {
            var big = Skill("big", TargetingRule.SingleEnemy, Dmg(2));
            big.Cooldown = 3;
            big.InitialCooldown = 1;
            var sim = new BattleSimulator(Setup(new[] { Unit("p", Team.Player, 0, 1000, 10, 0, 200, active: big) }, new[] { Unit("e", Team.Enemy, 0, 99999, 1, 0, 100) }), new SeededRandom(1));
            for (var i = 0; i < 20; i++) sim.Step();
            var mine = sim.Events.Where(e => e.Type == BattleEventType.ActionChosen && e.Actor == 0).Select(e => e.SkillId).ToList();
            CollectionAssert.AreEqual(new[] { "hit", "big", "hit", "hit", "big", "hit", "hit", "big", "hit", "hit" }, mine);
        }

        [Test]
        public void ManualMode_OnlyQueuedSkillsUsed()
        {
            var big = Skill("big", TargetingRule.SingleEnemy, Dmg(2));
            var sim = new BattleSimulator(Setup(new[] { Unit("p", Team.Player, 0, 1000, 10, 0, 200, active: big) }, new[] { Unit("e", Team.Enemy, 0, 99999, 1, 0, 100) }), new SeededRandom(1)) { AutoSkills = false };
            sim.Step(); sim.Step();
            Assert.IsTrue(sim.QueuePlayerSkill(0));
            Assert.IsFalse(sim.QueuePlayerSkill(1), "cannot queue enemy skills");
            sim.Step(); sim.Step(); sim.Step();
            var mine = sim.Events.Where(e => e.Type == BattleEventType.ActionChosen && e.Actor == 0).Select(e => e.SkillId).ToList();
            CollectionAssert.AreEqual(new[] { "hit", "big", "hit" }, mine);
            Assert.IsFalse(sim.IsSkillQueued(0));
        }

        [Test]
        public void Waves_SpawnInOrder_ThenVictory()
        {
            var sim = new BattleSimulator(Setup(new[] { Unit("p", Team.Player, 0, 1000, 100, 0, 200) },
                new[] { Unit("w1", Team.Enemy, 0, 50, 1, 0, 1) }, new[] { Unit("w2a", Team.Enemy, 0, 50, 1, 0, 1), Unit("w2b", Team.Enemy, 1, 50, 1, 0, 1) }), new SeededRandom(1));
            var r = sim.RunToEnd();
            Assert.AreEqual(BattleOutcome.Victory, r.Outcome);
            Assert.AreEqual(2, sim.Events.Count(e => e.Type == BattleEventType.WaveCleared));
            Assert.AreEqual(2, sim.Events.Count(e => e.Type == BattleEventType.WaveStarted));
            Assert.AreEqual(4, sim.All.Count);
            Assert.AreEqual(1, sim.Events.Count(e => e.Type == BattleEventType.BattleEnded));
        }

        [Test]
        public void Defeat_WhenPartyDies()
        {
            var r = new BattleSimulator(Setup(new[] { Unit("p", Team.Player, 0, 100, 1, 0, 1) }, new[] { Unit("e", Team.Enemy, 0, 9999, 500, 0, 100) }), new SeededRandom(1)).RunToEnd();
            Assert.AreEqual(BattleOutcome.Defeat, r.Outcome);
            Assert.AreEqual(1, r.PlayerDeaths);
            CollectionAssert.IsEmpty(r.Survivors);
        }

        [Test]
        public void Timeout_AfterMaxRounds_NoInfiniteLoop()
        {
            var setup = Setup(new[] { Unit("p", Team.Player, 0, 1e9f, 1, 1e6f, 10) }, new[] { Unit("e", Team.Enemy, 0, 1e9f, 1, 1e6f, 10) });
            setup.Rules = new CombatRules { MinDamage = 0, DefenseK = 1, MaxRounds = 7, CritChanceCap = 1, PowerWeights = Rules.PowerWeights };
            var sim = new BattleSimulator(setup, new SeededRandom(1));
            var r = sim.RunToEnd();
            Assert.AreEqual(BattleOutcome.Timeout, r.Outcome);
            Assert.AreEqual(7, r.Rounds);
            Assert.IsFalse(sim.Step());
        }

        private static BattleSetup FiveVsFive(GameDatabase db)
        {
            var s = new BattleSetup { Rules = db.Combat };
            var heroes = db.Heroes.Take(5).ToList();
            for (var i = 0; i < heroes.Count; i++) s.Players.Add(BattleSetupBuilder.Hero(heroes[i], 2, null, i, db));
            foreach (var p in s.Players) p.PreStats[StatType.CritChance] = 0.25f;
            s.EnemyWaves.Add(Enumerable.Range(0, 5).Select(i => BattleSetupBuilder.Enemy(db.GetEnemy(i == 4 ? "enemy_boss" : "enemy_x"), 1.6f, i, db)).ToList());
            return s;
        }

        [Test]
        public void Determinism_1000Seeds_IdenticalEventStreams()
        {
            var db = TestContent.Create();
            db.Combat.AiTargeting = AiTargeting.Random;
            var outcomes = new HashSet<BattleOutcome>();
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                var a = new BattleSimulator(FiveVsFive(db), new SeededRandom(seed));
                var b = new BattleSimulator(FiveVsFive(db), new SeededRandom(seed));
                a.RunToEnd();
                b.RunToEnd();
                Assert.AreEqual(BattleSimulator.Hash(a.Events), BattleSimulator.Hash(b.Events), $"seed {seed}");
                outcomes.Add(a.Outcome);
            }
            Assert.AreNotEqual(BattleSimulator.Hash(new BattleSimulator(FiveVsFive(db), new SeededRandom(1)).RunToEndEvents()),
                BattleSimulator.Hash(new BattleSimulator(FiveVsFive(db), new SeededRandom(2)).RunToEndEvents()), "different seeds differ");
        }

        [Test]
        public void Performance_5v5_Under5ms()
        {
            var db = TestContent.Create();
            new BattleSimulator(FiveVsFive(db), new SeededRandom(0)).RunToEnd(); // warm-up / JIT
            const int runs = 200;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < runs; i++) new BattleSimulator(FiveVsFive(db), new SeededRandom((ulong)i)).RunToEnd();
            sw.Stop();
            var avg = sw.Elapsed.TotalMilliseconds / runs;
            TestContext.WriteLine($"avg 5v5 simulation: {avg:0.000} ms");
            Assert.Less(avg, 5.0);
        }

        [Test]
        public void SetupFactory_UsesPartyStarsAndGear()
        {
            var s = TestSession.Create();
            var item = EquipmentFactory.Create(s.Db.GetEquipment("eq_sword"), Rarity.Common, s.Random, s.Db);
            s.Inventory.AddItem(item);
            s.Inventory.Equip("hero_a", item.InstanceId);
            s.Party.Swap(0, 3);
            var setup = BattleSetupFactory.Create(s, s.Db.GetFight("fight_3"));
            Assert.AreEqual(2, setup.Players.Count);
            Assert.AreEqual(3, setup.Players.Single(p => p.DefId == "hero_a").Slot);
            var sim = new BattleSimulator(setup, new SeededRandom(1));
            Assert.AreEqual(60, sim.Players.Single(p => p.DefId == "hero_a").Stat(StatType.Attack), 1e-3f);
            Assert.AreEqual(2, setup.EnemyWaves.Count);
            Assert.IsTrue(setup.EnemyWaves[1][0].IsBoss);
        }
    }

    internal static class SimTestExtensions
    {
        public static IReadOnlyList<BattleEvent> RunToEndEvents(this BattleSimulator sim)
        {
            sim.RunToEnd();
            return sim.Events;
        }
    }
}
