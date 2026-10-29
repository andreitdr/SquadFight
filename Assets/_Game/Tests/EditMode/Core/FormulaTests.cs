using Game.Core;
using Game.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class FormulaTests
    {
        private CombatRules _c;

        [SetUp]
        public void SetUp() => _c = TestContent.Create().Combat; // minDamage 1, defK 1, cap 1

        /// <summary>Always returns the same double.</summary>
        private sealed class FixedRandom : IRandom
        {
            private readonly double _v;
            public FixedRandom(double v) => _v = v;
            public int NextInt(int a, int b) => a;
            public double NextDouble() => _v;
            public ulong[] GetState() => new ulong[2];
            public void SetState(ulong[] s) { }
        }

        [Test]
        public void StatPipeline_HandComputed()
        {
            // base 500 HP, 50 ATK; star 2 = ×1.2; +10 ATK flat, +10% ATK, +50 HP flat
            var eq = new EquipmentStats();
            eq.Add(new[] { new StatModifier(StatType.Attack, 10), new StatModifier(StatType.Attack, 0.1f, ModifierKind.Percent), new StatModifier(StatType.MaxHp, 50) });
            var db = TestContent.Create();
            var s = StatCalculator.Compute(db.GetHero("hero_a"), 2, eq, db);
            Assert.AreEqual((50 * 1.2f + 10) * 1.1f, s[StatType.Attack], 1e-3f); // 77
            Assert.AreEqual(500 * 1.2f + 50, s[StatType.MaxHp], 1e-3f); // 650
            Assert.AreEqual(20 * 1.2f, s[StatType.Defense], 1e-3f);
        }

        [Test]
        public void StatPipeline_ChangingStarMultiplierChangesResult()
        {
            var db = TestContent.Create();
            db.GetStarLevel(2).StatMultiplier = 2f;
            Assert.AreEqual(100, StatCalculator.Compute(db.GetHero("hero_a"), 2, null, db)[StatType.Attack], 1e-4f);
        }

        [Test]
        public void Stats_ClampedNeverNegative_CritCapped()
        {
            var eq = new EquipmentStats();
            eq.Add(new[] { new StatModifier(StatType.Defense, -2f, ModifierKind.Percent), new StatModifier(StatType.CritChance, 5), new StatModifier(StatType.CritDamage, -9) });
            var s = StatCalculator.Compute(TestContent.Stats(100, 10, 10, 10, 0.1f, 1.5f), 1f, eq, _c);
            Assert.AreEqual(0, s[StatType.Defense]);
            Assert.AreEqual(1, s[StatType.CritChance]);
            Assert.AreEqual(1, s[StatType.CritDamage]);
        }

        [Test]
        public void Damage_HandComputed()
        {
            // 100 atk × 1.0 × 100/(100+50) = 66.67 → 67
            Assert.AreEqual(67, DamageCalculator.Damage(100, 1f, 50, 0, 1.5f, _c, new FixedRandom(0.5), out var crit));
            Assert.IsFalse(crit);
            // zero defense: full damage
            Assert.AreEqual(150, DamageCalculator.Damage(100, 1.5f, 0, 0, 1.5f, _c, new FixedRandom(0.5), out _));
        }

        [Test]
        public void Damage_DefKChangesMitigation()
        {
            _c.DefenseK = 2f; // 100/(100+100) = 0.5
            Assert.AreEqual(50, DamageCalculator.Damage(100, 1f, 50, 0, 1.5f, _c, new FixedRandom(0.5), out _));
        }

        [Test]
        public void Damage_MinDamageFloor()
        {
            _c.MinDamage = 5;
            Assert.AreEqual(5, DamageCalculator.Damage(1, 0.1f, 10000, 0, 1.5f, _c, new FixedRandom(0.5), out _));
            Assert.AreEqual(5, DamageCalculator.Damage(-50, 1f, 0, 0, 1.5f, _c, new FixedRandom(0.5), out _), "negative attack treated as 0");
        }

        [Test]
        public void Crit_RollAndMultiplier()
        {
            Assert.AreEqual(150, DamageCalculator.Damage(100, 1f, 0, 0.3f, 1.5f, _c, new FixedRandom(0.29), out var crit));
            Assert.IsTrue(crit);
            Assert.AreEqual(100, DamageCalculator.Damage(100, 1f, 0, 0.3f, 1.5f, _c, new FixedRandom(0.31), out crit));
            Assert.IsFalse(crit);
            Assert.IsTrue(DamageCalculator.RollCrit(5f, _c, new FixedRandom(0.999)), "max crit: chance clamped to 1 always crits");
            Assert.IsFalse(DamageCalculator.RollCrit(-1f, _c, new FixedRandom(0)), "negative chance never crits");
            _c.CritChanceCap = 0.5f;
            Assert.IsFalse(DamageCalculator.RollCrit(0.9f, _c, new FixedRandom(0.6)), "cap applies");
            Assert.AreEqual(100, DamageCalculator.Damage(100, 1f, 0, 1f, 0.5f, new CombatRules { CritChanceCap = 1, DefenseK = 1 }, new FixedRandom(0), out _), "crit damage below ×1 treated as ×1");
        }

        [Test]
        public void Heal_HandComputed() => Assert.AreEqual(75, DamageCalculator.Heal(50, 1.5f));

        [Test]
        public void Power_WeightedSum()
        {
            // 0.1×500 + 1×50 + 0.8×20 + 0.5×100 + 100×0.05 + 20×1.5 = 50+50+16+50+5+30 = 201
            Assert.AreEqual(201, PowerRating.Compute(TestContent.Stats(500, 50, 20, 100, 0.05f, 1.5f), _c));
        }

        [Test]
        public void Property_StatsNeverNegative_HigherStarNeverLowers()
        {
            var db = TestContent.Create();
            var rng = new SeededRandom(17);
            foreach (var hero in db.Heroes)
                for (var trial = 0; trial < 50; trial++)
                {
                    var eq = new EquipmentStats();
                    foreach (var st in StatBlock.All)
                    {
                        eq.Flat[st] = (float)(rng.NextDouble() * 40 - 10);
                        eq.Percent[st] = (float)(rng.NextDouble() * 0.6 - 0.2);
                    }
                    StatBlock previous = null;
                    for (var star = 1; star <= db.MaxStar; star++)
                    {
                        var s = StatCalculator.Compute(hero, star, eq, db);
                        foreach (var st in StatBlock.All)
                        {
                            Assert.GreaterOrEqual(s[st], 0f);
                            if (previous != null) Assert.GreaterOrEqual(s[st], previous[st] - 1e-4f, $"{hero.Id} {st} star {star}");
                        }
                        previous = s;
                    }
                }
        }

        [Test]
        public void Roster_FinalStatsIncludeStarAndEquipment()
        {
            var s = TestSession.Create();
            var item = EquipmentFactory.Create(s.Db.GetEquipment("eq_sword"), Rarity.Common, s.Random, s.Db);
            s.Inventory.AddItem(item);
            s.Inventory.Equip("hero_a", item.InstanceId);
            Assert.AreEqual(60, s.Roster.GetFinalStats("hero_a")[StatType.Attack], 1e-4f);
            var bd = s.Roster.GetStatBreakdown("hero_a");
            Assert.AreEqual(10, bd.EquipmentFlat[StatType.Attack]);
            Assert.AreEqual(1f, bd.StarMultiplier);
            Assert.Greater(s.Roster.GetPower("hero_a"), PowerRating.Compute(s.Db.GetHero("hero_a").BaseStats, s.Db.Combat));
            Assert.AreEqual(s.Roster.GetPower("hero_a") + s.Roster.GetPower("hero_b"), s.PartyPower);
        }
    }
}
