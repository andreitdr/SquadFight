using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Services
{
    /// <summary>One shop entry: a template at the chosen rarity.</summary>
    public readonly struct ShopOffer
    {
        public readonly EquipmentTemplateDef Template;
        public readonly Rarity Rarity;
        public readonly long Price;
        public readonly bool CanAfford;

        public ShopOffer(EquipmentTemplateDef template, Rarity rarity, long price, bool canAfford)
        {
            Template = template; Rarity = rarity; Price = price; CanAfford = canAfford;
        }

        public EquipmentSlot Slot => Template.Slot;
    }

    /// <summary>Gear shop (owner request 2026-10-31): buy weapons and armour of the current gear tier with the primary currency.</summary>
    public sealed class ShopService
    {
        private readonly ServiceContext _ctx;
        private readonly CurrencyService _currency;
        private readonly InventoryService _inventory;
        private readonly ProgressService _progress;

        public ShopService(ServiceContext ctx, CurrencyService currency, InventoryService inventory, ProgressService progress)
        {
            _ctx = ctx;
            _currency = currency;
            _inventory = inventory;
            _progress = progress;
        }

        public bool Enabled => _ctx.Db.Rules.ShopEnabled;

        /// <summary>Best gear tier reached in the campaign (see <see cref="ShopRules.CurrentTier"/>).</summary>
        public int CurrentTier => ShopRules.CurrentTier(_ctx.Db, _progress.IsFightUnlocked);

        public IReadOnlyList<Rarity> Rarities => ShopRules.Rarities(_ctx.Db.Rules);

        public IReadOnlyList<ShopOffer> GetOffers(Rarity rarity)
        {
            if (!Enabled) return new List<ShopOffer>();
            var currencyId = _ctx.Db.Rules.PrimaryCurrencyId;
            return ShopRules.Catalog(CurrentTier, _ctx.Db)
                .Select(t => (t, price: ShopRules.Price(t, rarity, _ctx.Db.Rules)))
                .Where(x => x.price != null)
                .Select(x => new ShopOffer(x.t, rarity, x.price.Value, _currency.CanAfford(currencyId, x.price.Value)))
                .ToList();
        }

        /// <summary>Price if the item is on sale right now (current tier only); otherwise null.</summary>
        public long? GetPrice(string templateId, Rarity rarity)
        {
            if (!Enabled || !_ctx.Db.TryGetEquipment(templateId, out var t) || t.Tier != CurrentTier) return null;
            return ShopRules.Price(t, rarity, _ctx.Db.Rules);
        }

        /// <summary>The hero among <paramref name="heroIds"/> whose power would rise most with this item, and by how much (null if nobody's).</summary>
        public (string HeroId, int PowerGain)? BestUpgrade(ShopOffer offer, IEnumerable<string> heroIds)
        {
            (string, int)? best = null;
            foreach (var h in heroIds)
            {
                if (h == null || !_ctx.Db.TryGetHero(h, out _)) continue;
                var gain = PowerRating.Compute(_inventory.PreviewNewItem(h, offer.Template, offer.Rarity), _ctx.Db.Combat) - _inventory.Roster.GetPower(h);
                if (gain > 0 && (best == null || gain > best.Value.Item2)) best = (h, gain);
            }
            return best;
        }

        /// <summary>Spends the price and adds a new item to the bag, all or nothing (one transaction, saved at once).</summary>
        public Result<string> Buy(string templateId, Rarity rarity)
        {
            if (!Enabled) return Result<string>.Fail(ErrorCode.FeatureDisabled, "shop");
            if (!_ctx.Db.TryGetEquipment(templateId, out var template)) return Result<string>.Fail(ErrorCode.NotFound, templateId);
            var price = GetPrice(templateId, rarity);
            if (price == null) return Result<string>.Fail(ErrorCode.InvalidArgument, $"{templateId} ({rarity}) is not on sale");
            if (_inventory.IsFull) return Result<string>.Fail(ErrorCode.InventoryFull);
            var currencyId = _ctx.Db.Rules.PrimaryCurrencyId;
            if (!_currency.CanAfford(currencyId, price.Value)) return Result<string>.Fail(ErrorCode.InsufficientFunds, $"need {price.Value} {currencyId}");

            var item = EquipmentFactory.Create(template, rarity, _ctx.Random, _ctx.Db);
            while (_inventory.Has(item.InstanceId)) item.InstanceId = EquipmentFactory.NewId(_ctx.Random);
            using (var tx = _ctx.BeginTransaction())
            {
                var spent = _currency.Spend(currencyId, price.Value);
                if (!spent.Ok) return Result<string>.Fail(spent.Code, spent.Detail);
                var added = _inventory.AddItem(item);
                if (!added.Ok) return Result<string>.Fail(added.Code, added.Detail); // disposing rolls the gold back
                _ctx.Emit(new ItemBought(item.InstanceId, templateId, rarity, price.Value));
                tx.Complete(saveNow: true);
            }
            return Result<string>.Success(item.InstanceId);
        }
    }
}
