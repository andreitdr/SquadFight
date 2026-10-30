using System.Linq;
using Game.Core;
using Game.Services;
using Game.ViewModels;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class ScreenViewModelTests
    {
        private GameSession _s;
        private NotificationsModel _dots;

        [SetUp]
        public void SetUp()
        {
            _s = TestSession.Create();
            _dots = new NotificationsModel(_s);
        }

        private string Give(string template, Rarity r)
        {
            var item = EquipmentFactory.Create(_s.Db.GetEquipment(template), r, _s.Random, _s.Db);
            _s.Inventory.AddItem(item);
            return item.InstanceId;
        }

        [Test]
        public void HeroList_UnlockInfoAndBadges()
        {
            var vm = new HeroListViewModel(_s, _dots);
            var c = vm.Items.Single(e => e.Def.Id == "hero_c");
            Assert.IsFalse(c.Unlocked);
            Assert.AreEqual(20, c.ShardsNeeded);
            Assert.IsFalse(c.Badge);
            var raised = 0;
            vm.Changed += _ => raised++;
            _s.Roster.AddShards("hero_c", 20);
            Assert.Greater(raised, 0);
            c = vm.Items.Single(e => e.Def.Id == "hero_c");
            Assert.IsTrue(c.Badge, "unlockable → red dot");
            Assert.IsTrue(_dots.AnyHeroBadge);
            Assert.AreEqual("hero_c", vm.Items.First(e => !e.Unlocked).Def.Id, "unlockable heroes float up");
            Assert.IsTrue(vm.Items.Take(2).All(e => e.Unlocked), "owned first");
            vm.SetRarityFilter(Rarity.Rare);
            Assert.IsTrue(vm.Items.All(e => e.Def.Rarity == Rarity.Rare));
            vm.SetRarityFilter(null);
            vm.SetOwnedOnly(true);
            Assert.AreEqual(2, vm.Items.Count);
        }

        [Test]
        public void HeroDetail_UnlockAndStarUpCommands()
        {
            var vm = new HeroDetailViewModel(_s, "hero_c");
            Assert.AreEqual("error_insufficientshards", vm.Unlock().MessageKey);
            _s.Roster.AddShards("hero_c", 20);
            Assert.IsTrue(vm.Unlock().Ok);
            Assert.IsTrue(vm.Unlocked);
            Assert.AreEqual(7, vm.Slots.Count);
            Assert.AreEqual(EquipmentSlot.Ring, vm.Slots[6].Slot);
            Assert.AreEqual(15, vm.StarUpInfo.NeedShards);
            Assert.AreEqual("error_insufficientshards", vm.StarUp().MessageKey);
        }

        [Test]
        public void Picker_ShowsDeltasAndSortsByPower()
        {
            var weak = Give("eq_sword", Rarity.Common);
            var strong = Give("eq_sword", Rarity.Epic);
            Give("eq_helm", Rarity.Epic);
            var vm = new EquipmentPickerViewModel(_s, "hero_a", 0);
            var list = vm.Candidates;
            Assert.AreEqual(2, list.Count, "only weapons");
            Assert.AreEqual(strong, list[0].Item.InstanceId);
            Assert.AreEqual(30, list[0].Delta[StatType.Attack], 1e-3f);
            Assert.Greater(list[0].PowerDelta, list[1].PowerDelta);
            Assert.IsTrue(vm.Equip(strong).Ok);
            var after = vm.Candidates.Single();
            Assert.AreEqual(weak, after.Item.InstanceId);
            Assert.AreEqual(-20, after.Delta[StatType.Attack], 1e-3f, "downgrade shows a negative delta");
            Assert.Less(after.PowerDelta, 0);
        }

        [Test]
        public void ItemBadge_ForUpgradesOnly()
        {
            var sword = Give("eq_sword", Rarity.Common);
            Assert.IsTrue(_dots.ItemBadge(sword));
            _s.Inventory.Equip("hero_a", sword);
            _s.Inventory.Equip("hero_b", Give("eq_sword", Rarity.Rare));
            var weaker = Give("eq_sword", Rarity.Common);
            Assert.IsFalse(_dots.ItemBadge(weaker), "not better than what both party heroes wear");
        }

        [Test]
        public void Party_PowerAndToggle()
        {
            var vm = new PartyViewModel(_s);
            var p0 = vm.Power;
            Assert.AreEqual(_s.Roster.GetPower("hero_a") + _s.Roster.GetPower("hero_b"), p0);
            Assert.IsTrue(vm.Toggle("hero_a").Ok);
            Assert.Less(vm.Power, p0);
            Assert.IsTrue(vm.Toggle("hero_a", 4).Ok);
            Assert.AreEqual("hero_a", vm.Slots[4]);
            Assert.AreEqual("error_herolocked", vm.Toggle("hero_c").MessageKey);
            vm.AutoFill();
            Assert.AreEqual(2, vm.Slots.Count(x => x != null));
            Assert.AreEqual(p0, vm.Power);
        }

        [Test]
        public void Campaign_StatesStarsAndNext()
        {
            var vm = new CampaignViewModel(_s);
            var f = vm.Fights;
            Assert.AreEqual(FightState.Unlocked, f[0].State);
            Assert.IsTrue(f[0].IsNext);
            Assert.AreEqual(FightState.Locked, f[1].State);
            Assert.IsTrue(f[2].IsBoss);
            _s.Progress.CompleteFight("fight_1", new BattleResult { Outcome = BattleOutcome.Victory, Rounds = 1 });
            f = vm.Fights;
            Assert.AreEqual(FightState.Cleared, f[0].State);
            Assert.AreEqual(3, f[0].Stars);
            Assert.IsTrue(f[1].IsNext);
            Assert.IsFalse(vm.Chapters[1].Unlocked);
            Assert.AreEqual(3, vm.Chapters[0].Stars);
        }

        [Test]
        public void FightPreview_DropCategoriesAndPower()
        {
            var vm = new FightPreviewViewModel(_s, "fight_1");
            var d = vm.Drops;
            Assert.IsTrue(d.Gold);
            CollectionAssert.Contains(d.HeroShards, "hero_c");
            CollectionAssert.Contains(d.ShardRarities, Rarity.Rare);
            CollectionAssert.Contains(d.ItemRarities, Rarity.Uncommon);
            CollectionAssert.IsEmpty(d.Odds, "odds hidden unless configured");
            Assert.IsTrue(vm.FirstClearAvailable);
            Assert.AreEqual(1, vm.Enemies.Count);
            Assert.IsTrue(vm.CanStart().Ok);
            Assert.AreEqual("error_fightlocked", new FightPreviewViewModel(_s, "fight_2").CanStart().MessageKey);
        }

        [Test]
        public void Preview_WarnsOnlyWellBelowRecommendedPower()
        {
            var vm = new FightPreviewViewModel(_s, "fight_1");
            var power = vm.PartyPower;
            vm.Fight.RecommendedPower = power;
            Assert.IsFalse(vm.IsUnderpowered || vm.IsClose, "at the recommendation");
            vm.Fight.RecommendedPower = (int)(power / 0.95f);
            Assert.IsTrue(vm.IsClose && !vm.IsUnderpowered, "5% under is close, no warning");
            vm.Fight.RecommendedPower = (int)(power / 0.8f);
            Assert.IsTrue(vm.IsUnderpowered && !vm.IsClose, "20% under warns");
        }

        [Test]
        public void Result_LinesWithProgressAndHighlights()
        {
            var c = _s.Battles.RunInstant("fight_1", 5).Value;
            var vm = new ResultViewModel(_s, c);
            Assert.IsTrue(vm.Victory);
            Assert.IsTrue(vm.NewBest);
            Assert.IsTrue(vm.HasNextFight);
            var lines = vm.Lines;
            Assert.IsTrue(lines.Any(l => l.Type == RewardLine.Kind.Currency && l.Amount >= 60));
            Assert.IsTrue(lines.Any(l => l.Type == RewardLine.Kind.Item && l.Item.HasValue));
            foreach (var s in lines.Where(l => l.Type == RewardLine.Kind.Shards)) Assert.AreEqual(s.ShardsBefore + s.Amount, s.ShardsAfter);
        }

        [Test]
        public void Settings_PersistedAndClamped()
        {
            var vm = new SettingsViewModel(_s);
            var changes = 0;
            vm.Changed += _ => changes++;
            vm.Settings.MusicVolume = 5f;
            Assert.AreEqual(1f, vm.Settings.MusicVolume);
            vm.CycleBattleSpeed();
            Assert.AreEqual(2f, vm.Settings.BattleSpeed);
            vm.CycleBattleSpeed();
            vm.CycleBattleSpeed();
            Assert.AreEqual(1f, vm.Settings.BattleSpeed);
            Assert.AreEqual(4, changes);
            Assert.AreEqual(1f, _s.State.Data.Settings.MusicVolume, "stored in the save");
        }

        [Test]
        public void Home_ContinueTargetAndCompletion()
        {
            var vm = new HomeViewModel(_s, _dots);
            Assert.AreEqual("fight_1", vm.NextFightId);
            Assert.AreEqual(0, vm.ChapterIndex);
            Assert.AreEqual(3, vm.ChapterTotal);
            new DebugCommands(_s).Execute("clear all");
            Assert.IsTrue(vm.CampaignComplete);
            vm.Dispose();
        }

        [Test]
        public void Inventory_BulkSellKeepsUpgrades()
        {
            Give("eq_sword", Rarity.Common); // upgrade for empty weapon slots → kept
            var vm = new InventoryViewModel(_s, _dots);
            Assert.AreEqual(0, vm.BulkSellCandidates(Rarity.Common).Count);
            _s.Inventory.Equip("hero_a", Give("eq_sword", Rarity.Legendary));
            _s.Inventory.Equip("hero_b", Give("eq_sword", Rarity.Legendary));
            Assert.AreEqual(1, vm.BulkSell(Rarity.Common));
            Assert.AreEqual(2, vm.Count);
        }
    }
}
