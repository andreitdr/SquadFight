using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Services
{
    public readonly struct HeroInfo
    {
        public readonly HeroDef Def;
        public readonly bool Unlocked;
        public readonly int Star;
        public readonly int Shards;

        public HeroInfo(HeroDef def, bool unlocked, int star, int shards)
        {
            Def = def;
            Unlocked = unlocked;
            Star = star;
            Shards = shards;
        }
    }

    /// <summary>Hero collection: queries, shards, unlocking and star-ups.</summary>
    public sealed partial class RosterService
    {
        private readonly ServiceContext _ctx;

        public RosterService(ServiceContext ctx) => _ctx = ctx;

        /// <summary>All heroes in content order.</summary>
        public IReadOnlyList<HeroDef> AllHeroes => _ctx.Db.Heroes;

        public bool Exists(string heroId) => _ctx.Db.TryGetHero(heroId, out _);

        public bool IsUnlocked(string heroId) => _ctx.State.GetHero(heroId)?.Unlocked == true && Exists(heroId);

        public int GetStar(string heroId)
        {
            var h = _ctx.State.GetHero(heroId);
            return h != null && h.Unlocked ? h.Stars : 0;
        }

        public int GetShards(string heroId) => _ctx.State.GetHero(heroId)?.Shards ?? 0;

        public IEnumerable<string> UnlockedHeroIds => _ctx.Db.Heroes.Where(h => IsUnlocked(h.Id)).Select(h => h.Id);

        public HeroInfo GetInfo(string heroId)
        {
            var def = _ctx.Db.GetHero(heroId);
            var save = _ctx.State.GetHero(heroId);
            var unlocked = save?.Unlocked == true;
            return new HeroInfo(def, unlocked, unlocked ? save.Stars : 0, save?.Shards ?? 0);
        }
    }
}
