using System.Linq;
using Game.Core;
using Game.Services;

namespace Game.ViewModels
{
    /// <summary>Red-dot rules, recomputed on relevant events: hero unlockable / star-up affordable, better unequipped item.</summary>
    public sealed class NotificationsModel : ViewModelBase
    {
        private readonly GameSession _s;

        public NotificationsModel(GameSession s)
        {
            _s = s;
            On<ShardsChanged>(s.Bus, _ => Raise());
            On<CurrencyChanged>(s.Bus, _ => Raise());
            On<HeroUnlocked>(s.Bus, _ => Raise());
            On<HeroStarUpgraded>(s.Bus, _ => Raise());
            On<ItemAdded>(s.Bus, _ => Raise());
            On<ItemEquipped>(s.Bus, _ => Raise());
            On<ItemUnequipped>(s.Bus, _ => Raise());
            On<ItemSold>(s.Bus, _ => Raise());
            On<PartyChanged>(s.Bus, _ => Raise());
            On<StateReset>(s.Bus, _ => Raise());
        }

        public bool HeroBadge(string heroId) => _s.Roster.CanUnlockOrUpgrade(heroId);

        public bool AnyHeroBadge => _s.Db.Heroes.Any(h => HeroBadge(h.Id));

        public bool ItemBadge(string instanceId) => _s.Inventory.IsUpgradeForParty(instanceId, _s.Party.Members);

        public bool AnyItemBadge => _s.Inventory.Query(includeEquipped: false).Any(i => ItemBadge(i.InstanceId));

        /// <summary>Hero has an empty slot for which an unequipped item exists, or an upgrade in the bag.</summary>
        public bool HeroGearBadge(string heroId) =>
            _s.Roster.IsUnlocked(heroId) && _s.Inventory.Query(includeEquipped: false).Any(i => _s.Inventory.PowerDelta(heroId, i.InstanceId) > 0);
    }
}
