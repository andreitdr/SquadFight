using System.Linq;
using Game.Core;
using Game.Save;

namespace Game.Services
{
    /// <summary>Builds a fresh save from <see cref="GameRules"/>: starting heroes unlocked at star 1, starting currencies, party filled.</summary>
    public static class NewGameFactory
    {
        public static SaveData Create(GameDatabase db, IClock clock)
        {
            var now = clock.UtcNow;
            var data = new SaveData { CreatedUtc = now, LastSavedUtc = now };
            foreach (var c in db.Rules.Currencies) data.Currencies[c.Id] = 0;
            foreach (var c in db.Rules.StartingCurrencies)
            {
                data.Currencies.TryGetValue(c.CurrencyId, out var v);
                data.Currencies[c.CurrencyId] = v + c.Amount;
            }
            foreach (var h in db.Heroes)
            {
                var starting = db.Rules.StartingHeroIds.Contains(h.Id);
                data.Heroes.Add(new HeroSave { HeroId = h.Id, Unlocked = starting, Stars = starting ? 1 : 0, Shards = 0 });
            }
            var starters = db.Rules.StartingHeroIds.Where(id => db.TryGetHero(id, out _)).Distinct().Take(db.Rules.PartySize).ToList();
            for (var i = 0; i < db.Rules.PartySize; i++) data.Party.Add(i < starters.Count ? starters[i] : null);
            return data;
        }
    }
}
