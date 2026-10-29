using System.Linq;
using Game.Core;

namespace Game.Services
{
    public sealed partial class RosterService
    {
        internal InventoryService Inventory { get; set; }

        /// <summary>Final out-of-combat stats (base × star + equipment). Locked heroes are shown at star 1 without gear.</summary>
        public StatBlock GetFinalStats(string heroId)
        {
            var def = _ctx.Db.GetHero(heroId);
            var unlocked = IsUnlocked(heroId);
            var equipment = unlocked ? Inventory?.GetEquipmentStats(heroId) : null;
            return StatCalculator.Compute(def, unlocked ? GetStar(heroId) : 1, equipment, _ctx.Db);
        }

        public StatBreakdown GetStatBreakdown(string heroId)
        {
            var def = _ctx.Db.GetHero(heroId);
            var unlocked = IsUnlocked(heroId);
            return StatCalculator.Breakdown(def, unlocked ? GetStar(heroId) : 1, unlocked ? Inventory?.GetEquipmentStats(heroId) : null, _ctx.Db);
        }

        public int GetPower(string heroId) => PowerRating.Compute(GetFinalStats(heroId), _ctx.Db.Combat);

        /// <summary>Sum of the power of the given heroes (e.g. the party).</summary>
        public int GetTotalPower(System.Collections.Generic.IEnumerable<string> heroIds) => heroIds.Where(h => h != null && Exists(h)).Sum(GetPower);
    }
}
