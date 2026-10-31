using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Validator rules and drop pools for the 100-hero roster (passives, unique abilities, origin pools).</summary>
    public class RosterRulesTests
    {
        private GameDatabase _db;

        [SetUp]
        public void SetUp() => _db = TestContent.Create();

        private static void AssertError(ValidationReport r, string fragment) =>
            Assert.IsTrue(r.Errors.Any(e => (e.Context + " " + e.Message).Contains(fragment)), $"Expected an error containing '{fragment}'.\n{r}");

        private static SkillDef Passive(string id, string effects, PassiveTrigger trigger = PassiveTrigger.BattleStart) =>
            new SkillDef { Id = id, NameKey = "k", Trigger = trigger, Targeting = TargetingRule.Self, Effects = EffectSpec.Parse(effects) };

        private void GiveEveryHeroUniqueAbilities()
        {
            for (var i = 0; i < _db.Heroes.Count; i++)
            {
                var h = _db.Heroes[i];
                var active = new SkillDef { Id = "skill_u" + i, NameKey = "k", Targeting = TargetingRule.SingleEnemy, Cooldown = 2, Effects = EffectSpec.Parse("Damage:" + (1 + i)) };
                var passive = Passive("passive_u" + i, "Status:Attack:0." + (i + 1) + ":2");
                _db.Skills.Add(active);
                _db.Skills.Add(passive);
                h.ActiveSkillId = active.Id;
                h.PassiveSkillId = passive.Id;
            }
            _db.Index();
        }

        [Test]
        public void UniqueAbilities_Off_AllowsSharedSkills()
        {
            var r = ConfigValidation.Validate(_db);
            Assert.IsFalse(r.HasErrors, r.ToString());
        }

        [Test]
        public void UniqueAbilities_On_ValidWhenEveryHeroDiffers()
        {
            _db.Rules.UniqueHeroAbilities = true;
            GiveEveryHeroUniqueAbilities();
            var r = ConfigValidation.Validate(_db);
            Assert.IsFalse(r.HasErrors, r.ToString());
        }

        [Test]
        public void UniqueAbilities_On_RejectsSameBehaviourUnderAnotherName()
        {
            _db.Rules.UniqueHeroAbilities = true;
            GiveEveryHeroUniqueAbilities();
            var copy = new SkillDef { Id = "skill_copy", NameKey = "k", Targeting = TargetingRule.SingleEnemy, Cooldown = 2, Effects = EffectSpec.Parse("Damage:1") };
            _db.Skills.Add(copy);
            _db.Heroes[1].ActiveSkillId = copy.Id; // same as hero 0's active
            AssertError(ConfigValidation.Validate(_db), "behaves exactly like");
        }

        [Test]
        public void UniqueAbilities_On_EveryHeroNeedsAPassive()
        {
            _db.Rules.UniqueHeroAbilities = true;
            GiveEveryHeroUniqueAbilities();
            _db.Heroes[2].PassiveSkillId = null;
            AssertError(ConfigValidation.Validate(_db), "hero has no passive");
        }

        [Test]
        public void PassiveSlots_MustHoldPassives_AndViceVersa()
        {
            var p = Passive("passive_x", "Shield:1:2");
            _db.Skills.Add(p);
            _db.Heroes[0].ActiveSkillId = "passive_x";
            _db.Heroes[1].PassiveSkillId = "skill_heal";
            var r = ConfigValidation.Validate(_db);
            AssertError(r, "is a passive");
            AssertError(r, "has no trigger");
        }

        [Test]
        public void EffectRanges_AreValidated()
        {
            _db.Skills.Add(new SkillDef { Id = "skill_bad", NameKey = "k", Targeting = TargetingRule.SingleEnemy, Effects =
            {
                new EffectDef { Type = EffectType.Execute, Power = 1, Value = 1.5f, Value2 = 0.5f },
                new EffectDef { Type = EffectType.Dodge, Value = 1f, Duration = 0 },
                new EffectDef { Type = EffectType.Revive, Value = 0 },
                new EffectDef { Type = EffectType.CooldownShift, Value = 0 },
            } });
            _db.Skills.Add(new SkillDef { Id = "passive_low", NameKey = "k", Trigger = PassiveTrigger.OnLowHp, Threshold = 0, Effects = EffectSpec.Parse("Heal:1") });
            _db.Heroes[0].ActiveSkillId = "skill_bad";
            _db.Heroes[0].PassiveSkillId = "passive_low";
            var r = ConfigValidation.Validate(_db);
            AssertError(r, "execute threshold");
            AssertError(r, "execute multiplier");
            AssertError(r, "dodge chance");
            AssertError(r, "duration must be >= 1");
            AssertError(r, "revive HP ratio");
            AssertError(r, "cooldown shift");
            AssertError(r, "OnLowHp passive needs a threshold");
        }

        [Test]
        public void OriginPool_LimitsRandomShards()
        {
            _db.Heroes.Add(TestContent.Hero("hero_r2", Rarity.Rare, TestContent.Stats(1, 1, 1, 1), null));
            _db.Heroes.First(h => h.Id == "hero_d").Origin = "act_1";
            _db.Heroes.Last().Origin = "act_2";
            _db.Index();
            var any = new DropEntryDef { Type = DropType.RandomHeroShardByRarity, Rarity = Rarity.Rare };
            var act2 = new DropEntryDef { Type = DropType.RandomHeroShardByRarity, Rarity = Rarity.Rare, RefId = "act_2" };
            var both = new DropEntryDef { Type = DropType.RandomHeroShardByRarity, Rarity = Rarity.Rare, RefId = "act_1+act_2" };
            Assert.AreEqual(2, DropRoller.ShardCandidates(any, _db).Count);
            CollectionAssert.AreEqual(new[] { "hero_r2" }, DropRoller.ShardCandidates(act2, _db).Select(h => h.Id).ToArray());
            Assert.AreEqual(2, DropRoller.ShardCandidates(both, _db).Count);

            var table = new DropTableDef { Id = "pool_table", Guaranteed = { act2 } };
            for (ulong seed = 1; seed < 30; seed++)
            {
                var bundle = DropRoller.Roll(table, new SeededRandom(seed), _db);
                CollectionAssert.AreEqual(new[] { "hero_r2" }, bundle.Shards.Keys.ToArray());
            }
        }

        [Test]
        public void OriginPool_WithNoMatch_IsError()
        {
            _db.DropTables[0].Guaranteed.Add(new DropEntryDef { Type = DropType.RandomHeroShardByRarity, Rarity = Rarity.Rare, RefId = "act_9" });
            AssertError(ConfigValidation.Validate(_db), "no Rare hero with origin 'act_9'");
        }
    }
}
