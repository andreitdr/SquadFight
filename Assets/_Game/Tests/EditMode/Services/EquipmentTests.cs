using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Save;
using Game.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Fixture: eq_sword Weapon Attack+10, eq_helm Helmet Def+5/HP+50, eq_ring Ring Attack+10%, eq_boots Speed+5.
    /// Rarity multipliers 1 / 1.5 / 2 / 3 / 4. Layout: Weapon, Helmet, Chest, Gloves, Boots, Ring(5), Ring(6).</summary>
    public class EquipmentTests
    {
        private GameSession _s;

        [SetUp]
        public void SetUp() => _s = TestSession.Create();

        private string Give(string template, Rarity rarity = Rarity.Common)
        {
            var item = EquipmentFactory.Create(_s.Db.GetEquipment(template), rarity, _s.Random, _s.Db);
            Assert.IsTrue(_s.Inventory.AddItem(item).Ok);
            return item.InstanceId;
        }

        [Test]
        public void Equip_AutoPicksSlot_AndUnequip()
        {
            var sword = Give("eq_sword");
            var events = new List<ItemEquipped>();
            _s.Bus.Subscribe<ItemEquipped>(events.Add);
            Assert.IsTrue(_s.Inventory.Equip("hero_a", sword).Ok);
            Assert.AreEqual("hero_a", _s.Inventory.Get(sword).Value.EquippedBy);
            Assert.AreEqual(0, _s.Inventory.Get(sword).Value.SlotIndex);
            Assert.AreEqual(1, events.Count);
            Assert.IsTrue(_s.Inventory.Unequip("hero_a", 0).Ok);
            Assert.IsFalse(_s.Inventory.Get(sword).Value.IsEquipped);
            Assert.AreEqual(ErrorCode.NotFound, _s.Inventory.Unequip("hero_a", 0).Code);
        }

        [Test]
        public void Equip_WrongSlotOrLockedHero_Rejected()
        {
            var sword = Give("eq_sword");
            Assert.AreEqual(ErrorCode.SlotIncompatible, _s.Inventory.Equip("hero_a", sword, 1).Code);
            Assert.AreEqual(ErrorCode.InvalidSlot, _s.Inventory.Equip("hero_a", sword, 99).Code);
            Assert.AreEqual(ErrorCode.HeroLocked, _s.Inventory.Equip("hero_c", sword).Code);
            Assert.AreEqual(ErrorCode.NotFound, _s.Inventory.Equip("hero_a", "nope").Code);
            Assert.IsFalse(_s.Inventory.Get(sword).Value.IsEquipped);
        }

        [Test]
        public void Equip_ItemOfAnotherHero_IsMoved_NotDuplicated()
        {
            var sword = Give("eq_sword");
            _s.Inventory.Equip("hero_a", sword);
            Assert.IsTrue(_s.Inventory.Equip("hero_b", sword).Ok);
            Assert.AreEqual("hero_b", _s.Inventory.Get(sword).Value.EquippedBy);
            Assert.IsNull(_s.Inventory.GetEquipped("hero_a", 0));
            Assert.AreEqual(1, _s.State.Data.Equipment.Count);
        }

        [Test]
        public void Equip_OccupiedSlot_SwapsOldItemBackToBag()
        {
            var a = Give("eq_sword");
            var b = Give("eq_sword", Rarity.Rare);
            _s.Inventory.Equip("hero_a", a);
            Assert.IsTrue(_s.Inventory.Equip("hero_a", b).Ok);
            Assert.AreEqual(b, _s.Inventory.GetEquipped("hero_a", 0).Value.InstanceId);
            Assert.IsFalse(_s.Inventory.Get(a).Value.IsEquipped);
        }

        [Test]
        public void Rings_TwoSlots_TwoInstancesOfSameTemplate()
        {
            var r1 = Give("eq_ring");
            var r2 = Give("eq_ring");
            var r3 = Give("eq_ring");
            Assert.IsTrue(_s.Inventory.Equip("hero_a", r1).Ok);
            Assert.IsTrue(_s.Inventory.Equip("hero_a", r2).Ok);
            Assert.AreEqual(5, _s.Inventory.Get(r1).Value.SlotIndex);
            Assert.AreEqual(6, _s.Inventory.Get(r2).Value.SlotIndex);
            Assert.IsTrue(_s.Inventory.Equip("hero_a", r3).Ok, "third ring replaces the first ring slot");
            Assert.AreEqual(5, _s.Inventory.Get(r3).Value.SlotIndex);
            Assert.IsFalse(_s.Inventory.Get(r1).Value.IsEquipped);
            Assert.IsTrue(_s.Inventory.Equip("hero_a", r3, 6).Ok, "moving within the same hero");
            Assert.AreEqual(6, _s.Inventory.Get(r3).Value.SlotIndex);
            Assert.IsFalse(_s.Inventory.Get(r2).Value.IsEquipped);
            Assert.IsNull(_s.Inventory.GetEquipped("hero_a", 5));
        }

        [TestCase(Rarity.Common, 1f)]
        [TestCase(Rarity.Uncommon, 1.5f)]
        [TestCase(Rarity.Rare, 2f)]
        [TestCase(Rarity.Epic, 3f)]
        [TestCase(Rarity.Legendary, 4f)]
        public void EquipmentStats_EqualManualCalculation(Rarity rarity, float mult)
        {
            _s.Inventory.Equip("hero_a", Give("eq_sword", rarity));
            _s.Inventory.Equip("hero_a", Give("eq_helm", rarity));
            _s.Inventory.Equip("hero_a", Give("eq_ring", rarity));
            _s.Inventory.Equip("hero_a", Give("eq_ring", rarity));
            _s.Inventory.Equip("hero_b", Give("eq_boots", rarity));
            var stats = _s.Inventory.GetEquipmentStats("hero_a");
            Assert.AreEqual(10 * mult, stats.Flat[StatType.Attack], 1e-4f);
            Assert.AreEqual(5 * mult, stats.Flat[StatType.Defense], 1e-4f);
            Assert.AreEqual(50 * mult, stats.Flat[StatType.MaxHp], 1e-4f);
            Assert.AreEqual(2 * 0.1f * mult, stats.Percent[StatType.Attack], 1e-4f);
            Assert.AreEqual(0, stats.Flat[StatType.Speed], "boots are on another hero");
        }

        [Test]
        public void RollVariation_DeterministicWithSeed_AndWithinBounds()
        {
            var db = TestContent.Create();
            db.Rules.EquipmentRollVariation = true;
            db.Rules.RollVariationPercent = 0.1f;
            var t = db.GetEquipment("eq_helm");
            var a = EquipmentFactory.Create(t, Rarity.Rare, new SeededRandom(5), db);
            var b = EquipmentFactory.Create(t, Rarity.Rare, new SeededRandom(5), db);
            Assert.AreEqual(a.InstanceId, b.InstanceId);
            CollectionAssert.AreEqual(a.RolledStats, b.RolledStats);
            foreach (var m in a.RolledStats)
            {
                var baseValue = t.Stats.Single(s => s.Stat == m.Stat).Value * 2f;
                Assert.That(m.Value, Is.InRange(baseValue * 0.9f - 0.05f, baseValue * 1.1f + 0.05f));
            }
            var noRoll = EquipmentFactory.Create(t, Rarity.Rare, new SeededRandom(5), TestContent.Create());
            Assert.IsNull(noRoll.RolledStats, "flag off: stats come from template × rarity");
        }

        [Test]
        public void RolledStats_UsedForEquipmentStats_AndSurviveSave()
        {
            var db = TestContent.Create();
            db.Rules.EquipmentRollVariation = true;
            db.Rules.RollVariationPercent = 0.2f;
            var s = TestSession.Create(db, seed: 9);
            var item = EquipmentFactory.Create(db.GetEquipment("eq_sword"), Rarity.Common, s.Random, db);
            s.Inventory.AddItem(item);
            s.Inventory.Equip("hero_a", item.InstanceId);
            Assert.AreEqual(item.RolledStats[0].Value, s.Inventory.GetEquipmentStats("hero_a").Flat[StatType.Attack], 1e-5f);
            var ser = new SaveSerializer();
            var reloaded = new GameSession(db, ser.Decode(ser.Encode(s.ExportForSave(), 1, System.DateTime.UtcNow)).Data, new RecordingSaver(), new SeededRandom(1), new ManualClock());
            Assert.AreEqual(item.RolledStats[0].Value, reloaded.Inventory.GetEquipmentStats("hero_a").Flat[StatType.Attack], 1e-5f);
        }

        [Test]
        public void EquipmentState_SurvivesSaveLoadExactly()
        {
            var r1 = Give("eq_ring", Rarity.Epic);
            var sword = Give("eq_sword", Rarity.Legendary);
            _s.Inventory.Equip("hero_b", r1, 6);
            _s.Inventory.Equip("hero_a", sword);
            Give("eq_boots");
            var ser = new SaveSerializer();
            var data = ser.Decode(ser.Encode(_s.ExportForSave(), 1, System.DateTime.UtcNow)).Data;
            var reloaded = new GameSession(_s.Db, data, new RecordingSaver(), new SeededRandom(1), new ManualClock());
            Assert.AreEqual(SaveSerializer.ToCanonicalJson(_s.State.Data), SaveSerializer.ToCanonicalJson(reloaded.ExportForSave()));
            Assert.AreEqual(6, reloaded.Inventory.Get(r1).Value.SlotIndex);
            Assert.AreEqual("hero_b", reloaded.Inventory.Get(r1).Value.EquippedBy);
            Assert.AreEqual(Rarity.Legendary, reloaded.Inventory.Get(sword).Value.Rarity);
            Assert.AreEqual(_s.Inventory.GetEquipmentStats("hero_a").Flat, reloaded.Inventory.GetEquipmentStats("hero_a").Flat);
        }

        [Test]
        public void Query_FiltersAndSorts()
        {
            Give("eq_sword", Rarity.Common);
            Give("eq_sword", Rarity.Epic);
            Give("eq_helm", Rarity.Rare);
            var newest = Give("eq_boots", Rarity.Uncommon);
            Assert.AreEqual(2, _s.Inventory.Query(EquipmentSlot.Weapon).Count);
            Assert.AreEqual(2, _s.Inventory.Query(minRarity: Rarity.Rare).Count);
            Assert.AreEqual(Rarity.Epic, _s.Inventory.Query()[0].Rarity);
            Assert.AreEqual(newest, _s.Inventory.Query(sort: ItemSort.Newest)[0].InstanceId);
        }

        [Test]
        public void Sell_GivesGold_BlockedWhenEquippedOrDisabled()
        {
            var sword = Give("eq_sword", Rarity.Rare);
            _s.Inventory.Equip("hero_a", sword);
            Assert.AreEqual(ErrorCode.ItemEquipped, _s.Inventory.SellItem(sword).Code);
            _s.Inventory.Unequip("hero_a", 0);
            Assert.IsTrue(_s.Inventory.SellItem(sword).Ok);
            Assert.AreEqual(160, _s.Currency.Get(TestContent.Gold));
            Assert.IsFalse(_s.Inventory.Has(sword));
            var db = TestContent.Create();
            db.Rules.SellEnabled = false;
            var s2 = TestSession.Create(db);
            var it = EquipmentFactory.Create(db.GetEquipment("eq_sword"), Rarity.Common, s2.Random, db);
            s2.Inventory.AddItem(it);
            Assert.AreEqual(ErrorCode.FeatureDisabled, s2.Inventory.SellItem(it.InstanceId).Code);
        }

        [Test]
        public void InventoryLimit_BlocksAdd()
        {
            var db = TestContent.Create();
            db.Rules.InventoryLimit = 1;
            var s = TestSession.Create(db);
            Assert.IsTrue(s.Inventory.AddItem(EquipmentFactory.Create(db.GetEquipment("eq_sword"), Rarity.Common, s.Random, db)).Ok);
            Assert.AreEqual(ErrorCode.InventoryFull, s.Inventory.AddItem(EquipmentFactory.Create(db.GetEquipment("eq_sword"), Rarity.Common, s.Random, db)).Code);
        }

        [Test]
        public void SlotLayoutIsConfig_AddingASlotNeedsNoCode()
        {
            var db = TestContent.Create();
            db.Rules.SlotLayout.Add(EquipmentSlot.Weapon); // e.g. off-hand weapon slot via config only
            var s = TestSession.Create(db);
            var a = EquipmentFactory.Create(db.GetEquipment("eq_sword"), Rarity.Common, s.Random, db);
            var b = EquipmentFactory.Create(db.GetEquipment("eq_sword"), Rarity.Common, s.Random, db);
            s.Inventory.AddItem(a);
            s.Inventory.AddItem(b);
            s.Inventory.Equip("hero_a", a.InstanceId);
            s.Inventory.Equip("hero_a", b.InstanceId);
            Assert.AreEqual(7, s.Inventory.Get(b.InstanceId).Value.SlotIndex);
            Assert.AreEqual(20, s.Inventory.GetEquipmentStats("hero_a").Flat[StatType.Attack], 1e-5f);
        }

        [Test]
        public void DebugGiveItem()
        {
            StringAssert.Contains("eq_ring", new DebugCommands(_s).Execute("give item eq_ring epic"));
            Assert.AreEqual(Rarity.Epic, _s.Inventory.All().Single().Rarity);
        }
    }
}
