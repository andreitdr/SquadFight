using Game.Core;

namespace Game.Services
{
    public readonly struct UnlockInfo
    {
        public readonly int Have;
        public readonly int Need;
        public readonly bool Unlocked;
        public readonly bool CanUnlock;
        public UnlockInfo(int have, int need, bool unlocked, bool canUnlock) { Have = have; Need = need; Unlocked = unlocked; CanUnlock = canUnlock; }
    }

    public readonly struct StarUpInfo
    {
        public readonly int Star;
        public readonly bool IsMaxStar;
        public readonly int HaveShards;
        public readonly int NeedShards;
        public readonly int CurrencyCost;
        public readonly bool CanAffordCurrency;
        public readonly bool CanUpgrade;
        public readonly float CurrentMultiplier;
        public readonly float NextMultiplier;

        public StarUpInfo(int star, bool isMax, int have, int need, int currencyCost, bool canAffordCurrency, bool canUpgrade, float current, float next)
        {
            Star = star; IsMaxStar = isMax; HaveShards = have; NeedShards = need; CurrencyCost = currencyCost;
            CanAffordCurrency = canAffordCurrency; CanUpgrade = canUpgrade; CurrentMultiplier = current; NextMultiplier = next;
        }
    }

    public sealed partial class RosterService
    {
        /// <summary>Needed by star-ups (currency cost) and overflow conversion. Set by <see cref="GameSession"/>.</summary>
        internal CurrencyService Currency { get; set; }

        public UnlockInfo GetUnlockInfo(string heroId)
        {
            var def = _ctx.Db.GetHero(heroId);
            var need = HeroProgressionRules.ShardsToUnlock(def, _ctx.Db);
            var unlocked = IsUnlocked(heroId);
            var have = GetShards(heroId);
            return new UnlockInfo(have, need, unlocked, HeroProgressionRules.CanUnlock(unlocked, have, need));
        }

        public StarUpInfo GetStarUpInfo(string heroId)
        {
            var def = _ctx.Db.GetHero(heroId);
            var star = GetStar(heroId);
            var have = GetShards(heroId);
            var cost = HeroProgressionRules.NextStarCost(def, star, _ctx.Db);
            var current = HeroProgressionRules.StarMultiplier(star, _ctx.Db);
            if (!IsUnlocked(heroId) || cost == null)
                return new StarUpInfo(star, IsUnlocked(heroId) && cost == null, have, 0, 0, false, false, current, current);
            var c = cost.Value;
            var affordCurrency = Currency == null || Currency.CanAfford(_ctx.Db.Rules.PrimaryCurrencyId, c.Currency);
            return new StarUpInfo(star, false, have, c.Shards, c.Currency, affordCurrency, have >= c.Shards && affordCurrency,
                current, HeroProgressionRules.StarMultiplier(star + 1, _ctx.Db));
        }

        public bool CanUnlockOrUpgrade(string heroId) =>
            Exists(heroId) && (IsUnlocked(heroId) ? GetStarUpInfo(heroId).CanUpgrade : GetUnlockInfo(heroId).CanUnlock);

        public Result AddShards(string heroId, int amount) => AddShards(heroId, amount, out _);

        /// <summary>Adds shards; for a hero at max star they are converted to currency instead (<paramref name="converted"/>).</summary>
        public Result AddShards(string heroId, int amount, out int converted)
        {
            converted = 0;
            if (amount <= 0) return Result.Fail(ErrorCode.InvalidArgument, "amount must be > 0");
            if (!_ctx.Db.TryGetHero(heroId, out var def)) return Result.Fail(ErrorCode.NotFound, heroId);
            var save = _ctx.State.GetOrCreateHero(heroId);
            if (save.Unlocked && HeroProgressionRules.IsMaxStar(save.Stars, _ctx.Db))
            {
                converted = amount;
                return ConvertOverflow(def, amount);
            }
            var old = save.Shards;
            save.Shards = old > int.MaxValue - amount ? int.MaxValue : old + amount;
            _ctx.Commit();
            _ctx.Emit(new ShardsChanged(heroId, old, save.Shards));
            return Result.Success;
        }

        public Result UnlockHero(string heroId)
        {
            if (!_ctx.Db.TryGetHero(heroId, out var def)) return Result.Fail(ErrorCode.NotFound, heroId);
            var save = _ctx.State.GetOrCreateHero(heroId);
            if (save.Unlocked) return Result.Fail(ErrorCode.AlreadyUnlocked, heroId);
            var need = HeroProgressionRules.ShardsToUnlock(def, _ctx.Db);
            if (save.Shards < need) return Result.Fail(ErrorCode.InsufficientShards, $"have {save.Shards}, need {need}");
            var old = save.Shards;
            if (_ctx.Db.Rules.ConsumeShardsOnUnlock) save.Shards -= need;
            save.Unlocked = true;
            save.Stars = 1;
            _ctx.Commit();
            if (old != save.Shards) _ctx.Emit(new ShardsChanged(heroId, old, save.Shards));
            _ctx.Emit(new HeroUnlocked(heroId));
            return Result.Success;
        }

        public Result UpgradeStar(string heroId)
        {
            if (!_ctx.Db.TryGetHero(heroId, out var def)) return Result.Fail(ErrorCode.NotFound, heroId);
            var save = _ctx.State.GetHero(heroId);
            if (save == null || !save.Unlocked) return Result.Fail(ErrorCode.NotUnlocked, heroId);
            var cost = HeroProgressionRules.NextStarCost(def, save.Stars, _ctx.Db);
            if (cost == null) return Result.Fail(ErrorCode.MaxStarReached, heroId);
            var c = cost.Value;
            if (save.Shards < c.Shards) return Result.Fail(ErrorCode.InsufficientShards, $"have {save.Shards}, need {c.Shards}");
            var currencyId = _ctx.Db.Rules.PrimaryCurrencyId;
            if (c.Currency > 0 && (Currency == null || !Currency.CanAfford(currencyId, c.Currency)))
                return Result.Fail(ErrorCode.InsufficientFunds, $"need {c.Currency} {currencyId}");

            if (c.Currency > 0) Currency.Spend(currencyId, c.Currency);
            var old = save.Shards;
            save.Shards -= c.Shards;
            save.Stars++;
            _ctx.Commit();
            _ctx.Emit(new ShardsChanged(heroId, old, save.Shards));
            _ctx.Emit(new HeroStarUpgraded(heroId, save.Stars));
            // Leftover shards at max star can never be used: convert them now (D2).
            if (HeroProgressionRules.IsMaxStar(save.Stars, _ctx.Db) && save.Shards > 0)
            {
                var leftover = save.Shards;
                save.Shards = 0;
                _ctx.Emit(new ShardsChanged(heroId, leftover, 0));
                ConvertOverflow(def, leftover);
            }
            return Result.Success;
        }

        private Result ConvertOverflow(HeroDef def, int shards)
        {
            var value = HeroProgressionRules.OverflowValue(def, shards, _ctx.Db);
            if (value <= 0 || Currency == null) return Result.Success;
            return Currency.Add(_ctx.Db.Rules.ShardOverflowCurrencyId, value);
        }
    }
}
