using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Gear shop. Fixture: weapons + armour on sale, tier prices 100 / 200, Common ×1, Uncommon ×2, Rare ×4 (Epic and
    /// Legendary not sold). Tier 1: eq_sword, eq_helm, eq_boots (Common templates) and eq_fine_sword (an Uncommon template, so ×2);
    /// eq_ring is a ring (not sold). Tier 2: eq_t2_axe, reached when fight_4 (chapter 2, first clear rolls tier-2 gear) unlocks.
    /// Start: 100 gold.</summary>
    public class ShopTests
    {
        private GameSession _s;
        private RecordingSaver _saver;

        public static GameDatabase Db()
        {
            var db = TestContent.Create();
            db.Rules.ShopEnabled = true;
            db.Rules.ShopSlots = new List<EquipmentSlot> { EquipmentSlot.Weapon, EquipmentSlot.Helmet, EquipmentSlot.Chest, EquipmentSlot.Gloves, EquipmentSlot.Boots };
            db.Rules.ShopTierPrices = new List<int> { 100, 200 };
            db.Rules.ShopRarityPrices = new List<RarityPrice> { new RarityPrice(Rarity.Common, 1), new RarityPrice(Rarity.Uncommon, 2), new RarityPrice(Rarity.Rare, 4) };
            db.Equipment.Add(new EquipmentTemplateDef { Id = "eq_fine_sword", NameKey = "k", Slot = EquipmentSlot.Weapon, Rarity = Rarity.Uncommon, Stats = { new StatModifier(StatType.Attack, 20) } });
            db.Equipment.Add(new EquipmentTemplateDef { Id = "eq_t2_axe", NameKey = "k", Slot = EquipmentSlot.Weapon, Rarity = Rarity.Common, Tier = 2, Stats = { new StatModifier(StatType.Attack, 40) } });
            db.DropTables.Add(new DropTableDef { Id = "drops_t2", Guaranteed = { new DropEntryDef { Type = DropType.RandomEquipment, RefId = "2", RarityWeights = { new RarityWeight(Rarity.Common, 1) } } } });
            db.Fights.First(f => f.Id == "fight_4").FirstClearTableId = "drops_t2";
            return db.Index();
        }

        [SetUp]
        public void SetUp() => _s = TestSession.Create(out _saver, Db());

        private long Gold => _s.Currency.Get(TestContent.Gold);
        private static BattleResult Win() => new BattleResult { Outcome = BattleOutcome.Victory, Rounds = 3 };

        [Test]
        public void Catalog_CurrentTierWeaponsAndArmour_PricedByTierAndRarity()
        {
            Assert.AreEqual(1, _s.Shop.CurrentTier);
            CollectionAssert.AreEqual(new[] { Rarity.Common, Rarity.Uncommon, Rarity.Rare }, _s.Shop.Rarities);
            var common = _s.Shop.GetOffers(Rarity.Common);
            CollectionAssert.AreEqual(new[] { "eq_sword", "eq_helm", "eq_boots", "eq_fine_sword" }, common.Select(o => o.Template.Id).ToArray(), "no ring, no tier 2");
            CollectionAssert.AreEqual(new long[] { 100, 100, 100, 200 }, common.Select(o => o.Price).ToArray());
            Assert.IsTrue(common[0].CanAfford);
            Assert.IsFalse(common[3].CanAfford);
            CollectionAssert.AreEqual(new long[] { 400, 400, 400, 800 }, _s.Shop.GetOffers(Rarity.Rare).Select(o => o.Price).ToArray());
            CollectionAssert.IsEmpty(_s.Shop.GetOffers(Rarity.Epic), "Epic is not on sale");
            Assert.IsNull(_s.Shop.GetPrice("eq_ring", Rarity.Common));
            Assert.IsNull(_s.Shop.GetPrice("eq_t2_axe", Rarity.Common), "tier 2 not reached");
        }

        [Test]
        public void Buy_SpendsGold_AddsItemAtChosenRarity_SavesAtOnce()
        {
            var added = new List<string>();
            var bought = new List<ItemBought>();
            _s.Bus.Subscribe<ItemAdded>(e => added.Add(e.InstanceId));
            _s.Bus.Subscribe<ItemBought>(e => bought.Add(e));
            _s.Currency.Add(TestContent.Gold, 300);
            var saves = _saver.Saves;

            var r = _s.Shop.Buy("eq_helm", Rarity.Rare);
            Assert.IsTrue(r.Ok, r.ToString());
            Assert.AreEqual(0, Gold);
            var item = _s.Inventory.Get(r.Value).Value;
            Assert.AreEqual("eq_helm", item.Template.Id);
            Assert.AreEqual(Rarity.Rare, item.Rarity);
            Assert.IsFalse(item.IsEquipped);
            Assert.AreEqual(saves + 1, _saver.Saves, "purchase is saved immediately");
            CollectionAssert.AreEqual(new[] { r.Value }, added);
            Assert.AreEqual(1, bought.Count);
            Assert.AreEqual(400, bought[0].Price);
            CollectionAssert.AreEqual(_s.Inventory.GetItemStats(r.Value).Select(m => m.Value), new[] { 10f, 100f }, "Rare = template × 2");
        }

        [Test]
        public void Buy_NotEnoughGold_ChangesNothing()
        {
            var items = _s.Inventory.Count;
            Assert.AreEqual(ErrorCode.InsufficientFunds, _s.Shop.Buy("eq_fine_sword", Rarity.Common).Code);
            Assert.AreEqual(100, Gold);
            Assert.AreEqual(items, _s.Inventory.Count);
        }

        [Test]
        public void Buy_FullBag_KeepsGold()
        {
            _s.Currency.Add(TestContent.Gold, 1000);
            Assert.IsTrue(_s.Shop.Buy("eq_sword", Rarity.Common).Ok);
            _s.Db.Rules.InventoryLimit = _s.Inventory.Count;
            var gold = Gold;
            Assert.AreEqual(ErrorCode.InventoryFull, _s.Shop.Buy("eq_sword", Rarity.Common).Code);
            Assert.AreEqual(gold, Gold);
        }

        [Test]
        public void Buy_ItemNotOnSale_Rejected()
        {
            Assert.AreEqual(ErrorCode.InvalidArgument, _s.Shop.Buy("eq_ring", Rarity.Common).Code);
            Assert.AreEqual(ErrorCode.InvalidArgument, _s.Shop.Buy("eq_sword", Rarity.Legendary).Code);
            Assert.AreEqual(ErrorCode.InvalidArgument, _s.Shop.Buy("eq_t2_axe", Rarity.Common).Code);
            Assert.AreEqual(ErrorCode.NotFound, _s.Shop.Buy("eq_nothing", Rarity.Common).Code);
            Assert.AreEqual(100, Gold);
        }

        [Test]
        public void Tier_RisesWhenTheCampaignReachesNewGear()
        {
            _s.Progress.CompleteFight("fight_1", Win());
            _s.Progress.CompleteFight("fight_2", Win());
            Assert.AreEqual(1, _s.Shop.CurrentTier);
            _s.Progress.CompleteFight("fight_3", Win());
            Assert.IsTrue(_s.Progress.IsFightUnlocked("fight_4"));
            Assert.AreEqual(2, _s.Shop.CurrentTier);
            var offers = _s.Shop.GetOffers(Rarity.Common);
            CollectionAssert.AreEqual(new[] { "eq_t2_axe" }, offers.Select(o => o.Template.Id).ToArray(), "only the current tier is sold");
            Assert.AreEqual(200, offers[0].Price);
        }

        [Test]
        public void Disabled_SellsNothing()
        {
            var s = TestSession.Create();
            Assert.IsFalse(s.Shop.Enabled);
            CollectionAssert.IsEmpty(s.Shop.GetOffers(Rarity.Common));
            Assert.AreEqual(ErrorCode.FeatureDisabled, s.Shop.Buy("eq_sword", Rarity.Common).Code);
        }

        [Test]
        public void BestUpgrade_PointsAtThePartyHeroThatGainsMost()
        {
            var sword = _s.Shop.GetOffers(Rarity.Common).First(o => o.Template.Id == "eq_sword");
            var best = _s.Shop.BestUpgrade(sword, _s.Party.Members);
            Assert.IsNotNull(best);
            Assert.AreEqual(10, best.Value.PowerGain, "+10 attack × weight 1 for an empty weapon slot");
            _s.Currency.Add(TestContent.Gold, 10000); // 800 per Rare fine sword
            foreach (var h in _s.Party.Members)
            {
                var id = _s.Shop.Buy("eq_fine_sword", Rarity.Rare).Value;
                Assert.IsTrue(_s.Inventory.Equip(h, id, 0).Ok);
            }
            Assert.IsNull(_s.Shop.BestUpgrade(sword, _s.Party.Members), "everyone holds a better weapon");
        }

        [Test]
        public void Validation_ShopPricesMustBeatSellValue()
        {
            var db = Db();
            Assert.IsFalse(ConfigValidation.Validate(db).HasErrors, ConfigValidation.Validate(db).ToString());
            db.Rules.ShopTierPrices[0] = 5;
            db.Rules.ShopSlots.Add(EquipmentSlot.Ring);
            db.Rules.SlotLayout.Remove(EquipmentSlot.Ring);
            db.Rules.SlotLayout.Remove(EquipmentSlot.Ring);
            db.Rules.ShopRarityPrices.Add(new RarityPrice(Rarity.Common, 3));
            var r = ConfigValidation.Validate(db);
            Assert.IsTrue(r.Contains("is not above its sell value"), r.ToString());
            Assert.IsTrue(r.Contains("shop slot Ring is not in the slot layout"), r.ToString());
            Assert.IsTrue(r.Contains("priced twice"), r.ToString());
        }
    }
}
