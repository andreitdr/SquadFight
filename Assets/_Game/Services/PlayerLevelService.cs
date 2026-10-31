using System.Collections.Generic;
using Game.Core;

namespace Game.Services
{
    /// <summary>What one XP grant changed.</summary>
    public sealed class PlayerLevelResult
    {
        public int XpGained;
        public int LevelBefore;
        public int LevelAfter;
        public int XpBefore;
        public int XpAfter;
        public readonly List<int> LevelsReached = new List<int>();
        /// <summary>Rewards of every level reached (null when no level was reached or the tables were empty).</summary>
        public RewardResult Rewards;
        public bool LeveledUp => LevelAfter > LevelBefore;
    }

    /// <summary>Player level: XP from defeated enemies (see <see cref="PlayerLevelRules"/>) and the rewards of each level reached.</summary>
    public sealed class PlayerLevelService
    {
        private readonly ServiceContext _ctx;
        private readonly RewardService _rewards;

        public PlayerLevelService(ServiceContext ctx, RewardService rewards)
        {
            _ctx = ctx;
            _rewards = rewards;
        }

        public int Level => _ctx.State.Data.Player.Level;
        public int Xp => _ctx.State.Data.Player.Xp;
        public long TotalXp => _ctx.State.Data.Player.TotalXp;
        /// <summary>XP needed for the next level; 0 at the cap.</summary>
        public int XpToNext => PlayerLevelRules.XpToNext(Level, _ctx.Db);
        public bool AtCap => Level >= _ctx.Db.MaxPlayerLevel;

        /// <summary>
        /// Adds XP, raises the level and grants every reached level's reward table, all in one transaction (the caller's,
        /// or its own with an immediate save).
        /// </summary>
        public PlayerLevelResult AddXp(int xp)
        {
            if (!_ctx.InTransaction)
            {
                using (var tx = _ctx.BeginTransaction())
                {
                    var own = AddXp(xp);
                    tx.Complete(saveNow: own.LeveledUp);
                    return own;
                }
            }
            var p = _ctx.State.Data.Player;
            var result = new PlayerLevelResult { XpGained = System.Math.Max(0, xp), LevelBefore = p.Level, XpBefore = p.Xp };
            if (result.XpGained > 0)
            {
                p.TotalXp += result.XpGained;
                var level = p.Level;
                var into = p.Xp;
                result.LevelsReached.AddRange(PlayerLevelRules.AddXp(ref level, ref into, result.XpGained, _ctx.Db));
                p.Level = level;
                p.Xp = into;
            }
            result.LevelAfter = p.Level;
            result.XpAfter = p.Xp;

            if (result.LevelsReached.Count > 0)
            {
                var bundle = new RewardBundle();
                foreach (var lvl in result.LevelsReached)
                    if (_ctx.Db.TryGetDropTable(_ctx.Db.GetPlayerLevel(lvl)?.RewardTableId, out var table)) bundle.Merge(DropRoller.Roll(table, _ctx.Random, _ctx.Db));
                if (!bundle.IsEmpty)
                {
                    var applied = _rewards.Apply(bundle);
                    if (!applied.Ok) throw new System.InvalidOperationException("Level rewards could not be applied: " + applied);
                    result.Rewards = applied.Value;
                }
            }
            if (result.XpGained > 0) _ctx.Commit();
            if (result.XpGained > 0) _ctx.Emit(new PlayerLevelChanged(result.LevelBefore, result.LevelAfter, result.XpGained));
            return result;
        }
    }
}
