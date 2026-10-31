using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;

namespace Game.ViewModels
{
    /// <summary>One row of the shop for the view: the offer plus the party hero it would help most.</summary>
    public sealed class ShopEntry
    {
        public ShopOffer Offer;
        /// <summary>Party hero whose power rises most with the item (null = no party upgrade).</summary>
        public string UpgradeFor;
        public int PowerGain;
    }

    /// <summary>Gear shop: current tier, rarity choice, offers with prices and party upgrade hints, buy.</summary>
    public sealed class ShopViewModel : ViewModelBase
    {
        private readonly GameSession _s;
        public Rarity SelectedRarity { get; private set; }

        public ShopViewModel(GameSession s)
        {
            _s = s;
            SelectedRarity = Rarities.Count > 0 ? Rarities[0] : Rarity.Common;
            On<CurrencyChanged>(s.Bus, _ => Raise());
            On<ItemEquipped>(s.Bus, _ => Raise());
            On<ItemUnequipped>(s.Bus, _ => Raise());
            On<PartyChanged>(s.Bus, _ => Raise());
            On<HeroStarUpgraded>(s.Bus, _ => Raise());
            On<StateReset>(s.Bus, _ => Raise());
        }

        public bool Enabled => _s.Shop.Enabled;
        public int Tier => _s.Shop.CurrentTier;
        public IReadOnlyList<Rarity> Rarities => _s.Shop.Rarities;
        public long Gold => _s.Currency.Get(_s.Db.Rules.PrimaryCurrencyId);

        public IReadOnlyList<ShopEntry> Entries
        {
            get
            {
                var party = _s.Party.Members.ToList();
                return _s.Shop.GetOffers(SelectedRarity).Select(o =>
                {
                    var best = _s.Shop.BestUpgrade(o, party);
                    return new ShopEntry { Offer = o, UpgradeFor = best?.HeroId, PowerGain = best?.PowerGain ?? 0 };
                }).ToList();
            }
        }

        public List<StatModifier> Stats(ShopOffer o) => EquipmentRules.GetStats(o.Template, o.Rarity, null, _s.Db);

        public void SelectRarity(Rarity r) { SelectedRarity = r; Raise(nameof(Entries)); }

        public CommandResult Buy(ShopOffer o) => CommandResult.From(_s.Shop.Buy(o.Template.Id, o.Rarity).AsResult());
    }
}
