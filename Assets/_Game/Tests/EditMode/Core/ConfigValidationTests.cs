using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class ConfigValidationTests
    {
        private GameDatabase _db;

        [SetUp]
        public void SetUp() => _db = TestContent.Create();

        private ValidationReport Validate() => ConfigValidation.Validate(_db);

        private static void AssertError(ValidationReport r, string fragment) =>
            Assert.IsTrue(r.Errors.Any(e => (e.Context + " " + e.Message).Contains(fragment)), $"Expected an error containing '{fragment}'.\n{r}");

        [Test]
        public void ValidFixture_HasNoErrors()
        {
            var r = Validate();
            Assert.IsFalse(r.HasErrors, r.ToString());
        }

        [Test]
        public void DuplicateHeroId_IsError()
        {
            _db.Heroes.Add(TestContent.Hero("hero_a", Rarity.Common, TestContent.Stats(1, 1, 1, 1), null));
            AssertError(Validate(), "hero:hero_a");
        }

        [Test]
        public void DuplicateFightAcrossChapters_IsError()
        {
            _db.Chapters[1].FightIds.Add("fight_1");
            AssertError(Validate(), "unique globally");
        }

        [TestCase("")]
        [TestCase("Hero_A")]
        [TestCase("1hero")]
        [TestCase("hero a")]
        [TestCase("hero__a")]
        public void BadIds_AreErrors(string id)
        {
            _db.Heroes[0].Id = id;
            Assert.IsTrue(Validate().HasErrors);
        }

        [Test]
        public void HeroWithUnknownSkill_IsError()
        {
            _db.Heroes[0].BasicSkillId = "skill_missing";
            AssertError(Validate(), "skill_missing");
        }

        [Test]
        public void FightWithoutDropTable_IsError()
        {
            _db.Fights[0].DropTableId = null;
            AssertError(Validate(), "missing drop table");
        }

        [Test]
        public void FightWithUnknownEnemy_IsError()
        {
            _db.Fights[0].Waves[0].Spawns[0].EnemyId = "enemy_missing";
            AssertError(Validate(), "enemy_missing");
        }

        [Test]
        public void FightWithoutWaves_IsError()
        {
            _db.Fights[1].Waves.Clear();
            AssertError(Validate(), "no waves");
        }

        [Test]
        public void EmptyChapter_IsError()
        {
            _db.Chapters[1].FightIds.Clear();
            AssertError(Validate(), "no fights");
        }

        [Test]
        public void NegativeWeight_IsError()
        {
            _db.DropTables[0].Groups[0].Entries[0].Weight = -1;
            AssertError(Validate(), "weight must be >= 0");
        }

        [Test]
        public void ZeroTotalWeight_IsError()
        {
            foreach (var e in _db.DropTables[0].Groups[0].Entries) e.Weight = 0;
            AssertError(Validate(), "total weight must be > 0");
        }

        [Test]
        public void ZeroWeightEntry_IsWarning()
        {
            _db.DropTables[0].Groups[0].Entries[0].Weight = 0;
            var r = Validate();
            Assert.IsTrue(r.Warnings.Any(w => w.Message.Contains("zero weight")));
        }

        [Test]
        public void DropOfUnknownHero_IsError()
        {
            _db.DropTables[0].Groups[0].Entries[0].RefId = "hero_missing";
            AssertError(Validate(), "hero_missing");
        }

        [Test]
        public void RandomEquipmentWithoutRarityWeights_IsError()
        {
            _db.DropTables[0].Groups[0].Entries[2].RarityWeights.Clear();
            AssertError(Validate(), "rarity weights");
        }

        [Test]
        public void BadAmounts_AreErrors()
        {
            _db.DropTables[0].Guaranteed[0].AmountMin = 5;
            _db.DropTables[0].Guaranteed[0].AmountMax = 2;
            AssertError(Validate(), "amounts");
        }

        [Test]
        public void MissingRarity_IsError()
        {
            _db.Rarities.RemoveAt(4);
            AssertError(Validate(), "rarity:Legendary");
        }

        [Test]
        public void StarTableGap_IsError()
        {
            _db.StarLevels[2].Star = 4;
            AssertError(Validate(), "gap");
        }

        [Test]
        public void StarMultiplierDecrease_IsError()
        {
            _db.StarLevels[2].StatMultiplier = 1.1f;
            AssertError(Validate(), "must not decrease");
        }

        [Test]
        public void UnknownStartingHero_IsError()
        {
            _db.Rules.StartingHeroIds.Add("hero_missing");
            AssertError(Validate(), "hero_missing");
        }

        [Test]
        public void UnknownStoryBeat_IsError()
        {
            _db.Fights[0].PreBeatIds.Add("beat_missing");
            AssertError(Validate(), "beat_missing");
        }

        [Test]
        public void SlotNotInLayout_IsError()
        {
            _db.Rules.SlotLayout.Remove(EquipmentSlot.Boots);
            AssertError(Validate(), "not in the slot layout");
        }

        [Test]
        public void UnobtainableHero_IsWarningOnly()
        {
            _db.Heroes.Add(TestContent.Hero("hero_hidden", Rarity.Epic, TestContent.Stats(1, 1, 1, 1), null));
            var r = Validate();
            Assert.IsFalse(r.HasErrors, r.ToString());
            Assert.IsTrue(r.Warnings.Any(w => w.Context == "hero:hero_hidden" && w.Message.Contains("never obtainable")));
        }

        [Test]
        public void InvalidStats_AreErrors()
        {
            _db.Heroes[0].BaseStats[StatType.MaxHp] = 0;
            _db.Heroes[1].BaseStats[StatType.CritChance] = 2;
            var r = Validate();
            AssertError(r, "MaxHp");
            AssertError(r, "CritChance");
        }
    }

    public class StatBlockTests
    {
        [Test]
        public void AddScaleMultiply_Work()
        {
            var a = TestContent.Stats(100, 10, 5, 50);
            var b = TestContent.Stats(10, 1, 1, 1, 0.1f, 0.5f);
            var sum = a + b;
            Assert.AreEqual(110, sum[StatType.MaxHp]);
            Assert.AreEqual(2.0f, sum[StatType.CritDamage], 1e-5f);
            Assert.AreEqual(100, a[StatType.MaxHp], "operator+ must not mutate operands");
            var scaled = a * 2;
            Assert.AreEqual(20, scaled[StatType.Attack]);
            var m = a.Clone().Multiply(TestContent.Stats(2, 2, 2, 2, 2, 2));
            Assert.AreEqual(100, m[StatType.Speed]);
        }

        [Test]
        public void EqualityAndClone()
        {
            var a = TestContent.Stats(1, 2, 3, 4);
            var c = a.Clone();
            Assert.AreEqual(a, c);
            c[StatType.Attack] = 9;
            Assert.AreNotEqual(a, c);
            Assert.AreEqual(2, a[StatType.Attack]);
        }

        [Test]
        public void SizedFromEnum() => Assert.AreEqual(System.Enum.GetValues(typeof(StatType)).Length, StatBlock.Count);
    }

    public class IdsTests
    {
        [TestCase("hero_knight_01", true)]
        [TestCase("a", true)]
        [TestCase("hero", true)]
        [TestCase("Hero", false)]
        [TestCase("_hero", false)]
        [TestCase("hero_", false)]
        [TestCase("hero-knight", false)]
        [TestCase(null, false)]
        public void Validates(string id, bool expected) => Assert.AreEqual(expected, Ids.IsValid(id));
    }
}
