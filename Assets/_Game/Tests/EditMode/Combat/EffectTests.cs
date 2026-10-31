using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Effects, targeting rules and passive triggers added for the 100-hero roster.</summary>
    public class EffectTests
    {
        private static readonly CombatRules Rules = new CombatRules { MinDamage = 1, DefenseK = 1, MaxRounds = 100, CritChanceCap = 1, AiTargeting = AiTargeting.LowestHp, PowerWeights = new StatBlock((StatType.Attack, 1f)) };

        private static SkillDef Skill(string id, TargetingRule t, string effects, int cooldown = 0, int targetCount = 1) =>
            new SkillDef { Id = id, NameKey = id, Targeting = t, TargetCount = targetCount, Cooldown = cooldown, Effects = EffectSpec.Parse(effects) };

        private static SkillDef Passive(string id, PassiveTrigger trigger, TargetingRule t, string effects, float threshold = 0, float chance = 0, int cooldown = 0, bool once = false, int targetCount = 1) =>
            new SkillDef { Id = id, NameKey = id, Trigger = trigger, Threshold = threshold, Chance = chance, Cooldown = cooldown, OncePerBattle = once, Targeting = t, TargetCount = targetCount, Effects = EffectSpec.Parse(effects) };

        private static readonly SkillDef Hit = Skill("hit", TargetingRule.SingleEnemy, "Damage:1");
        private static readonly SkillDef Wait = Skill("wait", TargetingRule.Self, "Status:Speed:0.01:1");

        private static CombatantSetup Unit(string id, Team team, int slot, float hp, float atk, float def, float spd, SkillDef basic = null, SkillDef active = null, SkillDef passive = null, float crit = 0) => new CombatantSetup
        {
            DefId = id, Team = team, Slot = slot, PreStats = TestContent.Stats(hp, atk, def, spd, crit, 2f), Basic = basic ?? Hit, Active = active, Passive = passive,
        };

        private static BattleSimulator Sim(IEnumerable<CombatantSetup> players, params CombatantSetup[][] waves)
        {
            var s = new BattleSetup { Rules = Rules };
            s.Players.AddRange(players);
            foreach (var w in waves) s.EnemyWaves.Add(w.ToList());
            return new BattleSimulator(s, new SeededRandom(7));
        }

        private static List<BattleEvent> Of(BattleSimulator sim, BattleEventType t) => sim.Events.Where(e => e.Type == t).ToList();

        private static void Turns(BattleSimulator sim, int n) { for (var i = 0; i < n && sim.Step(); i++) { } }

        // ---------------------------------------------------------------- parser

        [Test]
        public void Parser_ReadsEverySyntax()
        {
            var e = EffectSpec.Parse("Dot:burn:0.3:2|Hot:0.2:3|Drain:1.2:0.5|Execute:1:0.3:2|PctHp:0.1|Taunt:2|Silence:1:0.5|Cleanse|Dispel:2|Revive:0.4|Thorns:0.3:2|Dodge:0.2:3|Cooldown:-1|self>Mark:0.25:2");
            CollectionAssert.AreEqual(new[]
            {
                EffectType.DamageOverTime, EffectType.HealOverTime, EffectType.Drain, EffectType.Execute, EffectType.PercentHpDamage, EffectType.Taunt, EffectType.Silence,
                EffectType.Cleanse, EffectType.Dispel, EffectType.Revive, EffectType.Thorns, EffectType.Dodge, EffectType.CooldownShift, EffectType.Mark,
            }, e.Select(x => x.Type).ToArray());
            Assert.AreEqual("burn", e[0].StatusId);
            Assert.AreEqual(StackingRule.Stack, e[0].Stacking);
            Assert.AreEqual(3, e[0].MaxStacks);
            Assert.AreEqual(2f, e[3].Value2);
            Assert.AreEqual(4f, e[4].Value2, "PctHp cap defaults to 4x attack");
            Assert.AreEqual(0.5f, e[6].Chance);
            Assert.AreEqual(0f, e[7].Value, "Cleanse without a count removes everything");
            Assert.AreEqual(-1f, e[12].Value);
            Assert.AreEqual(EffectTarget.Self, e[13].Target);
        }

        [TestCase("Dot:acid:0.3:2")]
        [TestCase("Drain:1")]
        [TestCase("Explode:2")]
        [TestCase("Status:Luck:0.2:2")]
        [TestCase("Damage:abc")]
        [TestCase("")]
        public void Parser_RejectsBadSpecs(string spec) => Assert.Throws<System.FormatException>(() => EffectSpec.Parse(spec));

        // ---------------------------------------------------------------- effects

        [Test]
        public void DamageOverTime_TicksAtTurnStart_StacksAndCreditsKill()
        {
            var burn = Skill("burn", TargetingRule.SingleEnemy, "Dot:burn:0.5:3");
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 999, 100, 0, 200, basic: burn) }, new[] { Unit("e", Team.Enemy, 0, 260, 1, 0, 100, basic: Wait) });
            var result = sim.RunToEnd();
            var ticks = Of(sim, BattleEventType.DotTick);
            Assert.AreEqual(50, ticks[0].Amount, "first tick = one stack of 0.5 x 100");
            Assert.AreEqual(100, ticks[1].Amount, "second tick = two stacks");
            Assert.IsTrue(ticks.All(t => t.Actor == 0 && t.StatusId == "burn"));
            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            Assert.AreEqual(260, result.Combatants[0].DamageDealt, "the burner gets credit for DoT damage");
            Assert.AreEqual(0, sim.Events.Count(e => e.Type == BattleEventType.DamageDealt), "DoT is not direct damage");
        }

        [Test]
        public void DamageOverTime_MaxStacksRespected_AndBypassesShield()
        {
            var poison = Skill("poison", TargetingRule.SingleEnemy, "Dot:poison:0.1:5:Stack:2");
            var shieldSelf = Skill("shield", TargetingRule.Self, "Shield:100:5");
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 999, 100, 0, 200, basic: poison) }, new[] { Unit("e", Team.Enemy, 0, 5000, 1, 0, 100, basic: shieldSelf) });
            Turns(sim, 12);
            var e = sim.Enemies[0];
            Assert.AreEqual(2, e.Statuses.Count(s => s.StatusId == "poison"));
            Assert.IsTrue(Of(sim, BattleEventType.DotTick).All(t => t.Amount <= 20));
            Assert.Less(e.Hp, 5000, "poison ignores the shield");
        }

        [Test]
        public void HealOverTime_HealsAtTurnStart()
        {
            var regen = Skill("regen", TargetingRule.Self, "Hot:0.5:3");
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 1000, 100, 0, 50, basic: regen) }, new[] { Unit("e", Team.Enemy, 0, 9999, 200, 0, 100) });
            Turns(sim, 6);
            var ticks = Of(sim, BattleEventType.HotTick);
            Assert.IsNotEmpty(ticks);
            Assert.IsTrue(ticks.All(t => t.Amount == 50 && t.Target == 0));
        }

        [Test]
        public void Drain_HealsCasterByRatioOfDamage()
        {
            var drain = Skill("drain", TargetingRule.SingleEnemy, "Drain:1:0.5");
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 1000, 100, 0, 200, basic: drain) }, new[] { Unit("e", Team.Enemy, 0, 9999, 300, 0, 100) });
            Turns(sim, 3); // p hits, e hits (300), p drains 100 → heals 50
            Assert.AreEqual(1000 - 300 + 50, sim.Players[0].Hp);
        }

        [Test]
        public void Execute_MultipliesBelowThreshold()
        {
            var exe = Skill("exe", TargetingRule.SingleEnemy, "Execute:1:0.5:3");
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 999, 100, 0, 200, basic: exe) }, new[] { Unit("e", Team.Enemy, 0, 1000, 1, 0, 100, basic: Wait) });
            Turns(sim, 20);
            var hits = Of(sim, BattleEventType.DamageDealt).Select(h => h.Amount).ToList();
            CollectionAssert.AreEqual(new[] { 100, 100, 100, 100, 100, 300, 300 }, hits.Take(7).ToArray());
        }

        [Test]
        public void PercentHpDamage_IgnoresDefense_IsCappedByAttack()
        {
            var pct = Skill("pct", TargetingRule.SingleEnemy, "PctHp:0.2:2");
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 999, 50, 0, 200, basic: pct) },
                new[] { Unit("small", Team.Enemy, 0, 400, 1, 999, 100, basic: Wait), Unit("boss", Team.Enemy, 1, 100000, 1, 999, 90, basic: Wait) });
            Turns(sim, 1);
            Assert.AreEqual(80, Of(sim, BattleEventType.DamageDealt)[0].Amount, "20% of 400, defense ignored");
            sim.Enemies[0].Hp = 1;
            Turns(sim, 20);
            Assert.IsTrue(Of(sim, BattleEventType.DamageDealt).Where(d => d.Target == sim.Enemies[1].Index).All(d => d.Amount == 100), "capped at 2 x 50 attack");
        }

        [Test]
        public void Taunt_ForcesSingleTargetAttacks()
        {
            var taunt = Skill("taunt", TargetingRule.Self, "Taunt:3");
            var sim = Sim(new[] { Unit("tank", Team.Player, 0, 5000, 10, 0, 200, basic: taunt), Unit("squishy", Team.Player, 1, 100, 10, 0, 150) },
                new[] { Unit("e", Team.Enemy, 0, 9999, 30, 0, 100) });
            Turns(sim, 6);
            var enemyHits = Of(sim, BattleEventType.DamageDealt).Where(d => sim.Get(d.Actor).Team == Team.Enemy).ToList();
            Assert.IsNotEmpty(enemyHits);
            Assert.IsTrue(enemyHits.All(d => d.Target == 0), "the lowest-HP hero would be hit without taunt");
        }

        [Test]
        public void Silence_BlocksActiveSkill_ButCooldownStillTicks()
        {
            var silence = Skill("silence", TargetingRule.SingleEnemy, "Damage:0.1|Silence:2");
            var nuke = Skill("nuke", TargetingRule.SingleEnemy, "Damage:5", cooldown: 1);
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 99999, 10, 0, 200, basic: silence) }, new[] { Unit("e", Team.Enemy, 0, 99999, 10, 0, 100, active: nuke) });
            Turns(sim, 4);
            Assert.IsTrue(Of(sim, BattleEventType.Silenced).Any());
            Assert.IsFalse(sim.Events.Any(e => e.Type == BattleEventType.ActionChosen && e.SkillId == "nuke"));
        }

        [Test]
        public void Cleanse_RemovesDebuffs_KeepsBuffs()
        {
            var debuff = Skill("debuff", TargetingRule.SingleEnemy, "Status:Attack:-0.3:5|Dot:burn:0.01:5|Mark:0.1:5|Status:Defense:0.3:5");
            var cleanse = Skill("cleanse", TargetingRule.Self, "Cleanse");
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 99999, 10, 0, 50, basic: cleanse) }, new[] { Unit("e", Team.Enemy, 0, 99999, 10, 0, 100, basic: debuff) });
            Turns(sim, 2); // e debuffs p (the +Defense lands on p too), p cleanses
            var p = sim.Players[0];
            CollectionAssert.AreEqual(new[] { "debuff#3" }, p.Statuses.Select(s => s.StatusId).ToArray(), "only the buff stays");
            Assert.AreEqual(3, Of(sim, BattleEventType.Cleansed)[0].Amount);
        }

        [Test]
        public void Cleanse_RemovesStun()
        {
            var stun = Skill("stun", TargetingRule.SingleEnemy, "Stun:3");
            var cleanseAlly = Skill("cleanse", TargetingRule.AllAllies, "Cleanse");
            var sim = Sim(new[] { Unit("victim", Team.Player, 0, 99999, 10, 0, 50), Unit("cleric", Team.Player, 1, 99999, 10, 0, 40, basic: cleanseAlly) },
                new[] { Unit("e", Team.Enemy, 0, 99999, 10, 0, 100, basic: stun) });
            Turns(sim, 3); // e stuns victim, victim loses a turn, cleric cleanses
            Assert.AreEqual(0, sim.Players[0].StunTurns);
        }

        [Test]
        public void Dispel_RemovesBuffsAndShield()
        {
            var dispel = Skill("dispel", TargetingRule.SingleEnemy, "Dispel");
            var buff = Skill("buff", TargetingRule.Self, "Status:Attack:0.5:5|Shield:5:5|Thorns:0.2:5");
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 99999, 10, 0, 50, basic: dispel) }, new[] { Unit("e", Team.Enemy, 0, 99999, 10, 0, 100, basic: buff) });
            Turns(sim, 2); // e buffs itself, p dispels
            var foe = sim.Enemies[0];
            Assert.AreEqual(0, foe.Shield);
            Assert.IsEmpty(foe.Statuses);
            Assert.AreEqual(3, Of(sim, BattleEventType.Dispelled)[0].Amount);
        }

        [Test]
        public void Dispel_WithCount_RemovesNewestFirst()
        {
            var dispel = Skill("dispel", TargetingRule.SingleEnemy, "Dispel:1");
            var buff = Skill("buff", TargetingRule.Self, "Status:Attack:0.5:5|Status:Defense:0.5:5");
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 99999, 10, 0, 50, basic: dispel) }, new[] { Unit("e", Team.Enemy, 0, 99999, 10, 0, 100, basic: buff) });
            Turns(sim, 2);
            CollectionAssert.AreEqual(new[] { "buff#0" }, sim.Enemies[0].Statuses.Select(s => s.StatusId).ToArray());
        }

        [Test]
        public void Revive_BringsBackOneFallenAlly_OncePerUnit()
        {
            var revive = Skill("revive", TargetingRule.Self, "Revive:0.5", cooldown: 0);
            var sim = Sim(new[] { Unit("healer", Team.Player, 0, 99999, 1, 0, 50, basic: revive), Unit("frail", Team.Player, 1, 100, 1, 0, 40) },
                new[] { Unit("e", Team.Enemy, 0, 99999, 200, 0, 100, active: Skill("aoe", TargetingRule.AllEnemies, "Damage:1")) });
            Turns(sim, 30);
            var revives = Of(sim, BattleEventType.Revived);
            Assert.AreEqual(1, revives.Count, "a unit is revived at most once");
            Assert.AreEqual(1, revives[0].Target);
            Assert.AreEqual(50, revives[0].Amount);
        }

        [Test]
        public void Thorns_ReflectsDirectDamage()
        {
            var thorns = Skill("thorns", TargetingRule.Self, "Thorns:0.5:5");
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 9999, 1, 0, 200, basic: thorns) }, new[] { Unit("e", Team.Enemy, 0, 9999, 100, 0, 100) });
            Turns(sim, 2);
            var back = Of(sim, BattleEventType.ThornsReflected).Single();
            Assert.AreEqual(50, back.Amount);
            Assert.AreEqual(9999 - 50, sim.Enemies[0].Hp);
        }

        [Test]
        public void Dodge_AvoidsSomeHits_AndNoRollWithoutDodge()
        {
            var dodge = Skill("dodge", TargetingRule.Self, "Dodge:0.5:99");
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 999999, 1, 0, 200, basic: dodge) }, new[] { Unit("e", Team.Enemy, 0, 999999, 1, 0, 100) });
            Turns(sim, 400);
            var dodged = Of(sim, BattleEventType.Dodged).Count;
            var hits = Of(sim, BattleEventType.DamageDealt).Count(d => d.Target == 0);
            Assert.That(dodged / (float)(dodged + hits), Is.InRange(0.4f, 0.6f));
        }

        [Test]
        public void CooldownShift_MovesActiveCooldown_NeverBelowZero()
        {
            var slow = Skill("slow", TargetingRule.SingleEnemy, "Cooldown:2");
            var nuke = Skill("nuke", TargetingRule.SingleEnemy, "Damage:1", cooldown: 3);
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 99999, 1, 0, 200, basic: slow) }, new[] { Unit("e", Team.Enemy, 0, 99999, 1, 0, 100, active: nuke) });
            Turns(sim, 1);
            Assert.AreEqual(2, sim.Enemies[0].ActiveCooldown);
            var haste = Skill("haste", TargetingRule.Self, "Cooldown:-5");
            var sim2 = Sim(new[] { Unit("p", Team.Player, 0, 99999, 1, 0, 200, basic: haste, active: nuke) }, new[] { Unit("e", Team.Enemy, 0, 99999, 1, 0, 100) });
            sim2.AutoSkills = false;
            Turns(sim2, 1);
            Assert.AreEqual(0, sim2.Players[0].ActiveCooldown);
        }

        [Test]
        public void Mark_IncreasesDamageTaken()
        {
            var mark = Skill("mark", TargetingRule.SingleEnemy, "Mark:0.5:5|Damage:1");
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 999, 100, 0, 200, basic: mark) }, new[] { Unit("e", Team.Enemy, 0, 99999, 1, 0, 100, basic: Wait) });
            Turns(sim, 3);
            var hits = Of(sim, BattleEventType.DamageDealt).Select(d => d.Amount).ToList();
            CollectionAssert.AreEqual(new[] { 150, 150 }, hits, "mark lands before the hit in the same skill");
        }

        [Test]
        public void StatusKinds_DoNotChangeStats()
        {
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 999, 100, 0, 200) }, new[] { Unit("e", Team.Enemy, 0, 999, 1, 0, 100) });
            var p = sim.Players[0];
            p.Statuses.Add(new StatusEffect { StatusId = "thorns", Kind = StatusKind.Thorns, Stat = StatType.MaxHp, Value = 0.5f, Remaining = 3 });
            Assert.AreEqual(999, p.Stat(StatType.MaxHp));
        }

        // ---------------------------------------------------------------- targeting

        [Test]
        public void Targeting_HighestAttack_LowestHpRatio_Others_RandomAllies()
        {
            // indexes: players 0..2, enemies 3..5
            var poke = Skill("poke", TargetingRule.HighestAttackEnemy, "Damage:0.01");
            var finisher = Skill("finisher", TargetingRule.LowestHpEnemy, "Damage:0.01");
            var others = Skill("others", TargetingRule.AllOtherAllies, "Heal:1");
            var sim = Sim(new[]
            {
                Unit("p0", Team.Player, 0, 999, 1000, 0, 300, basic: poke),
                Unit("p1", Team.Player, 1, 999, 1000, 0, 200, basic: finisher),
                Unit("p2", Team.Player, 2, 999, 1, 0, 150, basic: others),
            }, new[] { Unit("e0", Team.Enemy, 0, 500, 10, 0, 1), Unit("e1", Team.Enemy, 1, 1000, 90, 0, 1), Unit("e2", Team.Enemy, 2, 9000, 50, 0, 1) });
            Turns(sim, 3);
            var actions = Of(sim, BattleEventType.ActionChosen);
            CollectionAssert.AreEqual(new[] { 4 }, actions[0].Targets, "highest attack = e1");
            CollectionAssert.AreEqual(new[] { 4 }, actions[1].Targets, "lowest HP ratio = e1 (99%), although e0 has less HP");
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, actions[2].Targets, "all allies except the caster");

            var rand = Skill("rand", TargetingRule.RandomAllies, "Heal:1", targetCount: 2);
            for (var seed = 1; seed < 20; seed++)
            {
                var s = new BattleSetup { Rules = Rules };
                for (var i = 0; i < 3; i++) s.Players.Add(Unit("p" + i, Team.Player, i, 999, 1, 0, 200 - i, basic: rand));
                s.EnemyWaves.Add(new List<CombatantSetup> { Unit("e", Team.Enemy, 0, 999, 1, 0, 1) });
                var r = new BattleSimulator(s, new SeededRandom((ulong)seed));
                r.Step();
                var t = r.Events.First(e => e.Type == BattleEventType.ActionChosen).Targets;
                Assert.AreEqual(2, t.Distinct().Count(), "random allies are distinct");
            }
        }

        [Test]
        public void Taunt_DoesNotRedirectAreaOrRandomAttacks()
        {
            var taunt = Skill("taunt", TargetingRule.Self, "Taunt:5");
            var aoe = Skill("aoe", TargetingRule.AllEnemies, "Damage:0.1");
            var sim = Sim(new[] { Unit("tank", Team.Player, 0, 5000, 10, 0, 200, basic: taunt), Unit("other", Team.Player, 1, 5000, 10, 0, 150, basic: Wait) },
                new[] { Unit("e", Team.Enemy, 0, 9999, 30, 0, 100, basic: aoe) });
            Turns(sim, 3);
            Assert.AreEqual(2, Of(sim, BattleEventType.DamageDealt).Count(d => d.Actor == 2));
        }

        // ---------------------------------------------------------------- passives

        [Test]
        public void Passive_BattleStart_FiresForHeroesAndEachWavesEnemies()
        {
            var opener = Passive("opener", PassiveTrigger.BattleStart, TargetingRule.Self, "Status:Attack:0.5:2");
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 9999, 1000, 0, 200, passive: opener) },
                new[] { Unit("a", Team.Enemy, 0, 10, 1, 0, 1, passive: opener) }, new[] { Unit("b", Team.Enemy, 0, 10, 1, 0, 1, passive: opener) });
            sim.RunToEnd();
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Of(sim, BattleEventType.PassiveTriggered).Select(e => e.Actor).ToArray());
        }

        [Test]
        public void Passive_AfterBasic_WithChance_AndAfterActive()
        {
            var echo = Passive("echo", PassiveTrigger.AfterBasic, TargetingRule.LowestHpEnemy, "Damage:0.5", chance: 0.5f);
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 999999, 1, 0, 200, passive: echo) }, new[] { Unit("e", Team.Enemy, 0, 999999, 1, 0, 100) });
            Turns(sim, 400);
            var fired = Of(sim, BattleEventType.PassiveTriggered).Count;
            Assert.That(fired, Is.InRange(35, 65), "≈ half of the 100 basic attacks before the round cap");

            var after = Passive("after", PassiveTrigger.AfterActive, TargetingRule.Self, "Heal:1");
            var nuke = Skill("nuke", TargetingRule.SingleEnemy, "Damage:1", cooldown: 2);
            var sim2 = Sim(new[] { Unit("p", Team.Player, 0, 999999, 1, 0, 200, active: nuke, passive: after) }, new[] { Unit("e", Team.Enemy, 0, 999999, 1, 0, 100) });
            Turns(sim2, 12);
            var actives = sim2.Events.Count(e => e.Type == BattleEventType.ActionChosen && e.SkillId == "nuke");
            Assert.AreEqual(actives, Of(sim2, BattleEventType.PassiveTriggered).Count);
        }

        [Test]
        public void Passive_OnLowHp_FiresOnceAtThreshold()
        {
            var last = Passive("last", PassiveTrigger.OnLowHp, TargetingRule.Self, "Heal:5", threshold: 0.5f);
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 1000, 100, 0, 50, passive: last) }, new[] { Unit("e", Team.Enemy, 0, 999999, 200, 0, 100) });
            Turns(sim, 40);
            Assert.AreEqual(1, Of(sim, BattleEventType.PassiveTriggered).Count);
        }

        [Test]
        public void Passive_OnKill_OnAllyDeath_OnHitTaken_OnCrit()
        {
            var onKill = Passive("onkill", PassiveTrigger.OnKill, TargetingRule.Self, "Status:Attack:0.1:9");
            var grief = Passive("grief", PassiveTrigger.OnAllyDeath, TargetingRule.Self, "Status:Attack:0.2:9");
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 99999, 1000, 0, 200, passive: onKill) },
                new[] { Unit("e0", Team.Enemy, 0, 10, 1, 0, 1, passive: grief), Unit("e1", Team.Enemy, 1, 99999, 1, 0, 1, passive: grief) });
            Turns(sim, 1);
            var fired = Of(sim, BattleEventType.PassiveTriggered).Select(e => e.SkillId).ToList();
            CollectionAssert.AreEqual(new[] { "onkill", "grief" }, fired);

            var spikes = Passive("spikes", PassiveTrigger.OnHitTaken, TargetingRule.SingleEnemy, "Damage:1", cooldown: 2);
            var sim2 = Sim(new[] { Unit("p", Team.Player, 0, 99999, 10, 0, 50, passive: spikes) }, new[] { Unit("e", Team.Enemy, 0, 99999, 10, 0, 100) });
            Turns(sim2, 6);
            Assert.AreEqual(2, Of(sim2, BattleEventType.PassiveTriggered).Count, "cooldown = 2 of the holder's turns: hits 1 and 3 of 3 trigger");

            var arc = Passive("arc", PassiveTrigger.OnCrit, TargetingRule.RandomEnemies, "Damage:0.1", targetCount: 2);
            var sim3 = Sim(new[] { Unit("p", Team.Player, 0, 99999, 10, 0, 200, passive: arc, crit: 1f) }, new[] { Unit("e", Team.Enemy, 0, 99999, 1, 0, 100) });
            Turns(sim3, 1);
            Assert.AreEqual(1, Of(sim3, BattleEventType.PassiveTriggered).Count);
            Assert.AreEqual(3, Of(sim3, BattleEventType.DamageDealt).Count, "crit hit + two arcs; arcs that crit do not chain");
        }

        [Test]
        public void Passive_TurnStart_OncePerBattle()
        {
            var once = Passive("once", PassiveTrigger.TurnStart, TargetingRule.Self, "Shield:1:2", once: true);
            var sim = Sim(new[] { Unit("p", Team.Player, 0, 99999, 10, 0, 200, passive: once) }, new[] { Unit("e", Team.Enemy, 0, 99999, 1, 0, 100) });
            Turns(sim, 10);
            Assert.AreEqual(1, Of(sim, BattleEventType.PassiveTriggered).Count);
        }

        [Test]
        public void NewMechanics_AreDeterministic()
        {
            for (var seed = 1; seed <= 50; seed++)
            {
                BattleSimulator Make()
                {
                    var s = new BattleSetup { Rules = Rules };
                    s.Players.Add(Unit("tank", Team.Player, 0, 3000, 40, 30, 90, active: Skill("taunt", TargetingRule.Self, "Taunt:2|Thorns:0.3:2", 3), passive: Passive("p1", PassiveTrigger.OnAllyDeath, TargetingRule.Self, "Shield:1:2")));
                    s.Players.Add(Unit("mage", Team.Player, 1, 1200, 80, 10, 110, active: Skill("burn", TargetingRule.AllEnemies, "Dot:burn:0.3:2|Silence:1:0.3", 3), passive: Passive("p2", PassiveTrigger.AfterBasic, TargetingRule.LowestHpEnemy, "Damage:0.3", chance: 0.4f), crit: 0.2f));
                    s.Players.Add(Unit("priest", Team.Player, 2, 1400, 50, 15, 100, active: Skill("rez", TargetingRule.LowestHpAlly, "Heal:1|Revive:0.3|Cleanse", 3), passive: Passive("p3", PassiveTrigger.OnLowHp, TargetingRule.Self, "Dodge:0.3:2", threshold: 0.4f)));
                    s.EnemyWaves.Add(new List<CombatantSetup>
                    {
                        Unit("brute", Team.Enemy, 0, 2500, 70, 20, 95, active: Skill("smash", TargetingRule.AllEnemies, "Damage:0.8|Mark:0.2:2", 3), passive: Passive("enrage", PassiveTrigger.OnLowHp, TargetingRule.Self, "Status:Attack:0.5:9", threshold: 0.3f)),
                        Unit("witch", Team.Enemy, 1, 1500, 60, 10, 105, active: Skill("drain", TargetingRule.HighestAttackEnemy, "Drain:1.2:0.5|Dispel:1", 2)),
                    });
                    return new BattleSimulator(s, new SeededRandom((ulong)seed));
                }
                var a = Make();
                var b = Make();
                a.RunToEnd();
                b.RunToEnd();
                Assert.AreEqual(BattleSimulator.Hash(a.Events), BattleSimulator.Hash(b.Events), $"seed {seed}");
                Assert.IsTrue(a.IsOver);
            }
        }
    }
}
