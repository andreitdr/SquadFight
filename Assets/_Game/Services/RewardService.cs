using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Services
{
    /// <summary>Rolls fight rewards and applies reward bundles atomically (one transaction, one immediate save).</summary>
    public sealed class RewardService : IFightRewardGranter
    {
        private readonly ServiceContext _ctx;
        private readonly CurrencyService _currency;
        private readonly RosterService _roster;
        private readonly InventoryService _inventory;
        private readonly PartyService _party;

        public RewardService(ServiceContext ctx, CurrencyService currency, RosterService roster, InventoryService inventory, PartyService party)
        {
            _ctx = ctx;
            _currency = currency;
            _roster = roster;
            _inventory = inventory;
            _party = party;
        }

        /// <summary>Rolls the tables that apply to this fight outcome (does not apply them).</summary>
        public RewardBundle RollFightRewards(FightDef fight, bool victory, bool firstClear, string chapterCompletedId)
        {
            var db = _ctx.Db;
            var bundle = new RewardBundle();
            if (victory)
            {
                if (db.TryGetDropTable(fight.DropTableId, out var drops)) bundle.Merge(DropRoller.Roll(drops, _ctx.Random, db));
                if (firstClear && db.TryGetDropTable(fight.FirstClearTableId, out var first)) bundle.Merge(DropRoller.Roll(first, _ctx.Random, db));
                if (chapterCompletedId != null && db.TryGetChapter(chapterCompletedId, out var ch) && db.TryGetDropTable(ch.ClearRewardTableId, out var clear))
                    bundle.Merge(DropRoller.Roll(clear, _ctx.Random, db));
                ApplyPity(fight, bundle);
            }
            else if (db.TryGetDropTable(db.Rules.DefeatConsolationTableId, out var consolation))
            {
                bundle.Merge(DropRoller.Roll(consolation, _ctx.Random, db));
            }
            return bundle;
        }

        /// <summary>Bad-luck protection: after N victories without any shard, add one shard of a hero this fight can drop.</summary>
        private void ApplyPity(FightDef fight, RewardBundle bundle)
        {
            var rules = _ctx.Db.Rules;
            if (!rules.PityEnabled) return;
            var pity = _ctx.State.Data.Pity;
            if (bundle.Shards.Values.Any(v => v > 0))
            {
                pity.FightsWithoutShard = 0;
                return;
            }
            pity.FightsWithoutShard++;
            if (pity.FightsWithoutShard < rules.PityFightsWithoutShard) return;
            var candidates = PityCandidates(fight);
            if (candidates.Count == 0) return;
            bundle.AddShards(candidates[_ctx.Random.NextInt(0, candidates.Count)], 1);
            pity.FightsWithoutShard = 0;
        }

        private List<string> PityCandidates(FightDef fight)
        {
            var db = _ctx.Db;
            var ids = new List<string>();
            if (db.TryGetDropTable(fight.DropTableId, out var t))
                foreach (var e in t.Guaranteed.Concat(t.Groups.SelectMany(g => g.Entries)))
                {
                    if (e.Type == DropType.HeroShard) ids.Add(e.RefId);
                    else if (e.Type == DropType.RandomHeroShardByRarity) ids.AddRange(db.Heroes.Where(h => h.Rarity == e.Rarity).Select(h => h.Id));
                }
            if (ids.Count == 0) ids.AddRange(db.Heroes.Where(h => !_roster.IsUnlocked(h.Id)).Select(h => h.Id));
            return ids.Distinct().Where(id => db.TryGetHero(id, out _)).OrderBy(id => id, StringComparer.Ordinal).ToList();
        }

        public RewardResult GrantFightRewards(FightDef fight, bool victory, bool firstClear, string chapterCompletedId)
        {
            if (!_ctx.InTransaction)
            {
                // Rolling mutates pity/RNG: keep it atomic with the apply.
                using (var tx = _ctx.BeginTransaction())
                {
                    var own = GrantFightRewards(fight, victory, firstClear, chapterCompletedId);
                    tx.Complete(saveNow: true);
                    return own;
                }
            }
            var bundle = RollFightRewards(fight, victory, firstClear, chapterCompletedId);
            var r = Apply(bundle);
            if (!r.Ok) throw new InvalidOperationException("Rewards could not be applied: " + r);
            return r.Value;
        }

        /// <summary>
        /// Applies everything or nothing. Inside an existing transaction the caller commits; otherwise this opens its own
        /// transaction and saves immediately. Items that do not fit a full inventory are converted to their sell value.
        /// </summary>
        public Result<RewardResult> Apply(RewardBundle bundle)
        {
            var check = Validate(bundle);
            if (!check.Ok) return Result<RewardResult>.Fail(check.Code, check.Detail);
            if (_ctx.InTransaction)
            {
                var inner = ApplyUnchecked(bundle);
                _ctx.Emit(new RewardsGranted(inner));
                return Result<RewardResult>.Success(inner);
            }
            using (var tx = _ctx.BeginTransaction())
            {
                var result = ApplyUnchecked(bundle);
                _ctx.Emit(new RewardsGranted(result));
                tx.Complete(saveNow: true);
                return Result<RewardResult>.Success(result);
            }
        }

        private Result Validate(RewardBundle bundle)
        {
            if (bundle == null) return Result.Fail(ErrorCode.InvalidArgument, "bundle");
            foreach (var c in bundle.Currencies)
            {
                if (!_ctx.Db.IsCurrency(c.Key)) return Result.Fail(ErrorCode.NotFound, "currency " + c.Key);
                if (c.Value < 0) return Result.Fail(ErrorCode.InvalidArgument, "negative currency");
            }
            foreach (var s in bundle.Shards)
            {
                if (!_ctx.Db.TryGetHero(s.Key, out _)) return Result.Fail(ErrorCode.NotFound, "hero " + s.Key);
                if (s.Value < 0) return Result.Fail(ErrorCode.InvalidArgument, "negative shards");
            }
            var ids = new HashSet<string>();
            foreach (var i in bundle.Items)
            {
                if (i == null || !_ctx.Db.TryGetEquipment(i.TemplateId, out _) || !_ctx.Db.TryGetRarity(i.Rarity, out _)) return Result.Fail(ErrorCode.NotFound, "item " + i?.TemplateId);
                // An id collision (astronomically unlikely) must not cost the player the fight: re-roll the id.
                while (string.IsNullOrEmpty(i.InstanceId) || !ids.Add(i.InstanceId) || _inventory.Has(i.InstanceId)) i.InstanceId = EquipmentFactory.NewId(_ctx.Random);
            }
            return Result.Success;
        }

        private RewardResult ApplyUnchecked(RewardBundle bundle)
        {
            var result = new RewardResult { Bundle = bundle };
            foreach (var c in bundle.Currencies.OrderBy(k => k.Key, StringComparer.Ordinal))
                if (c.Value > 0) Ensure(_currency.Add(c.Key, c.Value));

            foreach (var s in bundle.Shards.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (s.Value <= 0) continue;
                var couldUnlock = !_roster.IsUnlocked(s.Key) && _roster.GetUnlockInfo(s.Key).CanUnlock;
                var couldStarUp = _roster.IsUnlocked(s.Key) && _roster.GetStarUpInfo(s.Key).CanUpgrade;
                var before = _roster.GetShards(s.Key);
                Ensure(_roster.AddShards(s.Key, s.Value, out var converted));
                result.ShardProgress[s.Key] = (before, _roster.GetShards(s.Key));
                if (converted > 0) result.ConvertedShards[s.Key] = converted;
                if (!_roster.IsUnlocked(s.Key) && _roster.GetUnlockInfo(s.Key).CanUnlock && !couldUnlock) result.NowUnlockable.Add(s.Key);
                if (_roster.IsUnlocked(s.Key) && _roster.GetStarUpInfo(s.Key).CanUpgrade && !couldStarUp) result.StarUpAvailable.Add(s.Key);
            }

            foreach (var item in bundle.Items)
            {
                if (_inventory.IsFull)
                {
                    // D3: never lose a reward; convert what does not fit into its sell value.
                    var value = _ctx.Db.GetRarity(item.Rarity).ItemSellValue;
                    if (value > 0) Ensure(_currency.Add(_ctx.Db.Rules.PrimaryCurrencyId, value));
                    result.AutoSold.Add(item.InstanceId);
                    continue;
                }
                Ensure(_inventory.AddItem(item));
                try
                {
                    if (_inventory.IsUpgradeForParty(item.InstanceId, _party.Members)) result.BetterItems.Add(item.InstanceId);
                }
                catch (Exception e)
                {
                    _ctx.Log.Warning("[Rewards] upgrade highlight failed: " + e.Message); // cosmetic only
                }
            }
            return result;
        }

        private static void Ensure(Result r)
        {
            // Validation ran first; a failure here is a bug and must roll the transaction back.
            if (!r.Ok) throw new InvalidOperationException("Reward application failed after validation: " + r);
        }
    }
}
