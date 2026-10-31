using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Save;
using Game.Services;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace Game.Tests.EditMode
{
    /// <summary>Gold-bought skill levels. Fixture: level 2 = 50 gold (×1.5), level 3 = 100 gold (×2, max). hero_a: active skill_aoe
    /// (Damage 0.5) + passive_guard (Shield 1 for 2 turns at battle start); hero_b has neither. Start: 100 gold.</summary>
    public class SkillLevelTests
    {
        private GameSession _s;
        private RecordingSaver _saver;

        public static GameDatabase Db()
        {
            var db = TestContent.Create();
            db.SkillLevels = new List<SkillLevelDef>
            {
                new SkillLevelDef { Level = 1, GoldCost = 0, Potency = 1f },
                new SkillLevelDef { Level = 2, GoldCost = 50, Potency = 1.5f },
                new SkillLevelDef { Level = 3, GoldCost = 100, Potency = 2f },
            };
            db.Skills.Add(new SkillDef { Id = "passive_guard", NameKey = "k", Trigger = PassiveTrigger.BattleStart, Targeting = TargetingRule.Self, Effects = EffectSpec.Parse("Shield:1:2") });
            db.Heroes.First(h => h.Id == "hero_a").PassiveSkillId = "passive_guard";
            return db.Index();
        }

        [SetUp]
        public void SetUp() => _s = TestSession.Create(out _saver, Db());

        private long Gold => _s.Currency.Get(TestContent.Gold);

        [Test]
        public void NewHero_StartsAtLevel1_InfoShowsNextCostAndPotency()
        {
            Assert.AreEqual(1, _s.Roster.GetSkillLevel("hero_a", HeroSkillSlot.Active));
            var info = _s.Roster.GetSkillUpgradeInfo("hero_a", HeroSkillSlot.Passive);
            Assert.AreEqual(1, info.Level);
            Assert.AreEqual(3, info.MaxLevel);
            Assert.AreEqual(50, info.Cost);
            Assert.AreEqual(1f, info.Potency);
            Assert.AreEqual(1.5f, info.NextPotency);
            Assert.IsTrue(info.CanUpgrade);
        }

        [Test]
        public void Upgrade_SpendsGold_RaisesLevel_EmitsEvent()
        {
            var events = new List<HeroSkillUpgraded>();
            _s.Bus.Subscribe<HeroSkillUpgraded>(e => events.Add(e));
            var marks = _saver.DirtyMarks;
            Assert.IsTrue(_s.Roster.UpgradeSkill("hero_a", HeroSkillSlot.Active).Ok);
            Assert.AreEqual(50, Gold);
            Assert.AreEqual(2, _s.Roster.GetSkillLevel("hero_a", HeroSkillSlot.Active));
            Assert.AreEqual(1, _s.Roster.GetSkillLevel("hero_a", HeroSkillSlot.Passive), "slots level separately");
            Assert.AreEqual(2, _s.State.GetHero("hero_a").ActiveSkillLevel);
            Assert.Greater(_saver.DirtyMarks, marks);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(HeroSkillSlot.Active, events[0].Slot);
            Assert.AreEqual(2, events[0].NewLevel);
        }

        [Test]
        public void Upgrade_NotEnoughGold_ChangesNothing()
        {
            _s.Currency.Spend(TestContent.Gold, 60);
            Assert.IsFalse(_s.Roster.GetSkillUpgradeInfo("hero_a", HeroSkillSlot.Active).CanUpgrade);
            Assert.AreEqual(ErrorCode.InsufficientFunds, _s.Roster.UpgradeSkill("hero_a", HeroSkillSlot.Active).Code);
            Assert.AreEqual(40, Gold);
            Assert.AreEqual(1, _s.Roster.GetSkillLevel("hero_a", HeroSkillSlot.Active));
        }

        [Test]
        public void Upgrade_AtMaxLevel_Rejected()
        {
            _s.Currency.Add(TestContent.Gold, 1000);
            Assert.IsTrue(_s.Roster.UpgradeSkill("hero_a", HeroSkillSlot.Passive).Ok);
            Assert.IsTrue(_s.Roster.UpgradeSkill("hero_a", HeroSkillSlot.Passive).Ok);
            var info = _s.Roster.GetSkillUpgradeInfo("hero_a", HeroSkillSlot.Passive);
            Assert.IsTrue(info.IsMaxLevel);
            Assert.IsFalse(info.CanUpgrade);
            Assert.AreEqual(2f, info.Potency);
            var gold = Gold;
            Assert.AreEqual(ErrorCode.MaxSkillLevelReached, _s.Roster.UpgradeSkill("hero_a", HeroSkillSlot.Passive).Code);
            Assert.AreEqual(gold, Gold);
            Assert.AreEqual(950, gold, "50 + 100 spent");
        }

        [Test]
        public void Upgrade_LockedHeroOrMissingSkill_Rejected()
        {
            Assert.AreEqual(ErrorCode.NotUnlocked, _s.Roster.UpgradeSkill("hero_c", HeroSkillSlot.Active).Code);
            Assert.AreEqual(ErrorCode.NotFound, _s.Roster.UpgradeSkill("hero_b", HeroSkillSlot.Active).Code, "hero_b has no active");
            Assert.AreEqual(ErrorCode.NotFound, _s.Roster.UpgradeSkill("nobody", HeroSkillSlot.Active).Code);
            Assert.IsFalse(_s.Roster.GetSkillUpgradeInfo("hero_c", HeroSkillSlot.Active).CanUpgrade);
            Assert.AreEqual(100, Gold);
        }

        [Test]
        public void NoSkillLevelTable_SkillsCannotLevel()
        {
            var s = TestSession.Create();
            Assert.AreEqual(1, s.Db.MaxSkillLevel);
            Assert.IsTrue(s.Roster.GetSkillUpgradeInfo("hero_a", HeroSkillSlot.Active).IsMaxLevel);
            Assert.AreEqual(ErrorCode.MaxSkillLevelReached, s.Roster.UpgradeSkill("hero_a", HeroSkillSlot.Active).Code);
        }

        [Test]
        public void Scale_MultipliesMagnitudes_KeepsDurationsCooldownsAndChances()
        {
            var skill = new SkillDef
            {
                Id = "s", Targeting = TargetingRule.SingleEnemy, Cooldown = 3, Trigger = PassiveTrigger.OnHitTaken, Chance = 0.4f,
                Effects = EffectSpec.Parse("Damage:1|Shield:0.5:2|Stun:1:0.5|Status:Defense:-0.2:2|Dot:burn:0.3:2|Revive:0.8|Dodge:0.3:2|Thorns:0.2:2|PctHp:0.1:4|Cooldown:1"),
            };
            var c = SkillLevelRules.Scale(skill, 1.5f);
            Assert.AreNotSame(skill, c);
            Assert.AreEqual(3, c.Cooldown);
            Assert.AreEqual(0.4f, c.Chance, "proc chance unchanged");
            Assert.AreEqual(1.5f, c.Effects[0].Power, 1e-5);
            Assert.AreEqual(0.75f, c.Effects[1].Power, 1e-5);
            Assert.AreEqual(2, c.Effects[1].Duration, "durations unchanged");
            Assert.AreEqual(0.5f, c.Effects[2].Chance, "stun chance unchanged");
            Assert.AreEqual(1, c.Effects[2].Duration);
            Assert.AreEqual(-0.3f, c.Effects[3].Value, 1e-5, "debuffs grow too");
            Assert.AreEqual(0.45f, c.Effects[4].Power, 1e-5);
            Assert.AreEqual(1f, c.Effects[5].Value, "revive HP capped at full");
            Assert.AreEqual(0.3f, c.Effects[6].Value, "dodge is a chance: unchanged");
            Assert.AreEqual(0.3f, c.Effects[7].Value, 1e-5);
            Assert.AreEqual(0.15f, c.Effects[8].Value, 1e-5);
            Assert.AreEqual(6f, c.Effects[8].Value2, 1e-5);
            Assert.AreEqual(1f, c.Effects[9].Value, "cooldown shifts unchanged");
            Assert.AreEqual(1f, skill.Effects[0].Power, "original untouched");
            Assert.AreEqual(EffectSpec.Signature(skill).Split('|')[0], EffectSpec.Signature(c).Split('|')[0], "same trigger/targeting/cooldown");
            Assert.AreSame(skill, SkillLevelRules.Scale(skill, 1f));
        }

        [Test]
        public void Battle_UsesLevelledActiveAndPassive_ContentUntouched()
        {
            _s.Currency.Add(TestContent.Gold, 1000);
            _s.Roster.UpgradeSkill("hero_a", HeroSkillSlot.Active);
            _s.Roster.UpgradeSkill("hero_a", HeroSkillSlot.Passive);
            _s.Roster.UpgradeSkill("hero_a", HeroSkillSlot.Passive);
            var setup = BattleSetupFactory.Create(_s, _s.Db.GetFight("fight_1"));
            var a = setup.Players.Single(p => p.DefId == "hero_a");
            Assert.AreEqual(0.75f, a.Active.Effects[0].Power, 1e-5, "0.5 × 1.5");
            Assert.AreEqual(2f, a.Passive.Effects[0].Power, 1e-5, "1 × 2");
            Assert.AreEqual(0.5f, _s.Db.GetSkill("skill_aoe").Effects[0].Power, "content skill unchanged");
            Assert.AreSame(_s.Db.GetSkill("skill_hit"), a.Basic, "basic attack never levels");

            // The stronger shield shows up in a real battle.
            var sim = new BattleSimulator(setup, new SeededRandom(3));
            sim.Step();
            var shield = sim.Players.First(c => c.DefId == "hero_a").Shield;
            var plain = new BattleSimulator(BattleSetupFactory.Create(TestSession.Create(Db()), _s.Db.GetFight("fight_1")), new SeededRandom(3));
            plain.Step();
            Assert.Greater(shield, plain.Players.First(c => c.DefId == "hero_a").Shield);
        }

        [Test]
        public void Validation_RejectsBrokenSkillLevelTables()
        {
            var db = Db();
            Assert.IsFalse(ConfigValidation.Validate(db).HasErrors, ConfigValidation.Validate(db).ToString());
            db.SkillLevels[0].Potency = 1.2f;
            db.SkillLevels[2].Potency = 1.1f;
            db.SkillLevels[1].GoldCost = 0;
            db.SkillLevels.Add(new SkillLevelDef { Level = 5, GoldCost = 10, Potency = 3f });
            var r = ConfigValidation.Validate(db);
            Assert.IsTrue(r.Contains("level 1 potency must be 1"), r.ToString());
            Assert.IsTrue(r.Contains("potency must not decrease"), r.ToString());
            Assert.IsTrue(r.Contains("goldCost must be > 0"), r.ToString());
            Assert.IsTrue(r.Contains("gap or duplicate"), r.ToString());
        }

        [Test]
        public void Reconciler_ClampsSkillLevels()
        {
            var data = _s.State.Data;
            data.Heroes.First(h => h.HeroId == "hero_a").ActiveSkillLevel = 9;
            data.Heroes.First(h => h.HeroId == "hero_b").PassiveSkillLevel = 0;
            var fixes = SaveReconciler.Reconcile(data, _s.Db);
            Assert.AreEqual(3, data.Heroes.First(h => h.HeroId == "hero_a").ActiveSkillLevel);
            Assert.AreEqual(1, data.Heroes.First(h => h.HeroId == "hero_b").PassiveSkillLevel);
            Assert.AreEqual(2, fixes.Count(f => f.Contains("skill level clamped")));
        }

        [Test]
        public void Migration_5_To_6_StartsSkillsAtLevel1_AndLevelsSurviveARoundTrip()
        {
            var payload = JObject.Parse("{\"version\":5,\"heroes\":[{\"heroId\":\"hero_a\",\"unlocked\":true,\"stars\":2,\"shards\":3}]}");
            new Migration_5_To_6().Apply(payload);
            Assert.AreEqual(1, (int)payload["heroes"][0]["activeSkillLevel"]);
            Assert.AreEqual(1, (int)payload["heroes"][0]["passiveSkillLevel"]);
            Assert.AreEqual(2, (int)payload["heroes"][0]["stars"]);

            _s.Currency.Add(TestContent.Gold, 1000);
            _s.Roster.UpgradeSkill("hero_a", HeroSkillSlot.Passive);
            var ser = new SaveSerializer();
            var back = ser.Decode(ser.Encode(_s.ExportForSave(), 1, System.DateTime.UtcNow));
            Assert.AreEqual(DecodeStatus.Ok, back.Status, back.Error);
            Assert.AreEqual(2, back.Data.Heroes.First(h => h.HeroId == "hero_a").PassiveSkillLevel);
            Assert.AreEqual(1, back.Data.Heroes.First(h => h.HeroId == "hero_a").ActiveSkillLevel);
        }
    }
}
