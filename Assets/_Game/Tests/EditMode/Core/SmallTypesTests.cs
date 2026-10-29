using System;
using System.IO;
using Game.Core;
using Game.Save;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class SmallTypesTests
    {
        [Test]
        public void StatBlock_ToStringAndHash()
        {
            var a = TestContent.Stats(1, 2, 3, 4);
            StringAssert.Contains("Attack=2", a.ToString());
            Assert.AreEqual(a.GetHashCode(), a.Clone().GetHashCode());
            Assert.IsFalse(a.Equals((object)null));
            Assert.IsTrue(a.ApproximatelyEquals(a.Clone().Add(new StatBlock((StatType.MaxHp, 0.00001f)))));
            var c = new StatBlock();
            c.CopyFrom(a);
            Assert.AreEqual(a, c);
            c.Clear();
            Assert.AreEqual(0, c[StatType.Speed]);
            StringAssert.Contains("%", new StatModifier(StatType.Attack, 0.1f, ModifierKind.Percent).ToString());
        }

        [Test]
        public void Ids_ShardCurrencyAndLimits()
        {
            Assert.AreEqual("shard_hero_a", Ids.ShardCurrency("hero_a"));
            Assert.IsFalse(Ids.IsValid(new string('a', 65)));
        }

        [Test]
        public void Clocks_And_Loggers()
        {
            var sys = new SystemClock();
            Assert.That(sys.RealtimeSeconds, Is.GreaterThanOrEqualTo(0));
            Assert.Less(Math.Abs((sys.UtcNow - DateTime.UtcNow).TotalSeconds), 5);
            NullLogger.Instance.Info("x"); NullLogger.Instance.Warning("x"); NullLogger.Instance.Error("x");
            NullHaptics.Instance.Play(HapticKind.Success);
            var a = NullAudio.Instance;
            a.PlayMusic("m"); a.StopMusic(); a.PlaySfx("s"); a.SetMusicVolume(1); a.SetSfxVolume(1); a.Muted = true;
            Assert.IsTrue(a.Muted);
        }

        [Test]
        public void BattleLog_DescribesEveryEventKind()
        {
            var db = TestContent.Create();
            var setup = new BattleSetup { Rules = db.Combat };
            var stun = new SkillDef { Id = "s", NameKey = "s", Targeting = TargetingRule.AllEnemies, Effects =
            {
                new EffectDef { Type = EffectType.Damage, Power = 1 },
                new EffectDef { Type = EffectType.Stun, Duration = 1 },
                new EffectDef { Type = EffectType.ApplyStatus, Stat = StatType.Defense, Value = -0.2f, Duration = 1 },
                new EffectDef { Type = EffectType.Shield, Target = EffectTarget.Self, Power = 0.5f, Duration = 1 },
                new EffectDef { Type = EffectType.Heal, Target = EffectTarget.Self, Power = 0.1f },
            } };
            setup.Players.Add(new CombatantSetup { DefId = "hero", Team = Team.Player, PreStats = TestContent.Stats(500, 80, 10, 200, 1f, 2f), Basic = stun, Active = null });
            setup.EnemyWaves.Add(new System.Collections.Generic.List<CombatantSetup> { new CombatantSetup { DefId = "foe", Team = Team.Enemy, PreStats = TestContent.Stats(300, 10, 0, 50), Basic = db.GetSkill("skill_hit") } });
            setup.EnemyWaves.Add(new System.Collections.Generic.List<CombatantSetup> { new CombatantSetup { DefId = "foe2", Team = Team.Enemy, PreStats = TestContent.Stats(100, 10, 0, 50), Basic = db.GetSkill("skill_hit") } });
            var sim = new BattleSimulator(setup, new SeededRandom(1));
            sim.RunToEnd();
            var log = BattleLog.Format(sim, id => id.ToUpperInvariant());
            foreach (var fragment in new[] { "Battle started", "Wave 2", "CRIT", "stunned", "shield", "heals", "defeated", "cleared", "Victory" })
                StringAssert.Contains(fragment, log);
        }

        [Test]
        public void FileStorage_MoveDeleteAndMissing()
        {
            var dir = Path.Combine(Path.GetTempPath(), "sf-fs-" + Guid.NewGuid().ToString("N"));
            try
            {
                var st = new FileSaveStorage(dir);
                Assert.IsNull(st.Read("nope.json"));
                st.Write("a.json", "1");
                st.Write("b.json", "2");
                st.Move("a.json", "b.json");
                Assert.AreEqual("1", st.Read("b.json"));
                Assert.IsFalse(st.Exists("a.json"));
                st.Replace("b.json", "c.json", null);
                Assert.AreEqual("1", st.Read("c.json"));
                st.Delete("c.json");
                st.Delete("c.json");
                Assert.IsFalse(st.Exists("c.json"));
                CollectionAssert.IsEmpty(st.List("c"));
                StringAssert.Contains("sf-fs-", st.Location);
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
    }
}
