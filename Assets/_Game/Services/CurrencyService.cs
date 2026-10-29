using Game.Core;

namespace Game.Services
{
    public sealed class CurrencyService
    {
        private readonly ServiceContext _ctx;

        public CurrencyService(ServiceContext ctx) => _ctx = ctx;

        public string Primary => _ctx.Db.Rules.PrimaryCurrencyId;

        public long Get(string currencyId) => currencyId != null && _ctx.State.Data.Currencies.TryGetValue(currencyId, out var v) ? v : 0;

        public bool CanAfford(string currencyId, long amount) => amount <= 0 || Get(currencyId) >= amount;

        public Result Add(string currencyId, long amount)
        {
            var check = Validate(currencyId, amount);
            if (!check.Ok) return check;
            if (amount == 0) return Result.Success;
            var old = Get(currencyId);
            var updated = old > long.MaxValue - amount ? long.MaxValue : old + amount;
            Set(currencyId, old, updated);
            return Result.Success;
        }

        public Result Spend(string currencyId, long amount)
        {
            var check = Validate(currencyId, amount);
            if (!check.Ok) return check;
            if (amount == 0) return Result.Success;
            var old = Get(currencyId);
            if (old < amount) return Result.Fail(ErrorCode.InsufficientFunds, $"{currencyId}: have {old}, need {amount}");
            Set(currencyId, old, old - amount);
            return Result.Success;
        }

        private Result Validate(string currencyId, long amount)
        {
            if (amount < 0) return Result.Fail(ErrorCode.InvalidArgument, "amount must be >= 0");
            if (!_ctx.Db.IsCurrency(currencyId)) return Result.Fail(ErrorCode.NotFound, $"unknown currency '{currencyId}'");
            return Result.Success;
        }

        private void Set(string currencyId, long old, long value)
        {
            _ctx.State.Data.Currencies[currencyId] = value;
            _ctx.Commit();
            _ctx.Emit(new CurrencyChanged(currencyId, old, value));
        }
    }
}
