using System.Collections.Generic;
using System.Linq;
using Game.Services;

namespace Game.ViewModels
{
    /// <summary>Currencies shown in the top bar; refreshes on CurrencyChanged / StateReset.</summary>
    public sealed class CurrencyBarViewModel : ViewModelBase
    {
        private readonly GameSession _s;

        public CurrencyBarViewModel(GameSession session)
        {
            _s = session;
            On<CurrencyChanged>(session.Bus, e => Raise(e.CurrencyId));
            On<StateReset>(session.Bus, _ => Raise());
        }

        public IReadOnlyList<string> CurrencyIds => _s.Db.Rules.Currencies.Select(c => c.Id).ToList();

        public long Amount(string currencyId) => _s.Currency.Get(currencyId);
    }
}
