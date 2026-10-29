using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Services
{
    public readonly struct FightProgress
    {
        public readonly bool Unlocked;
        public readonly bool Cleared;
        public readonly int BestStars;
        public readonly int Clears;
        public readonly bool FirstClearClaimed;
        public FightProgress(bool unlocked, bool cleared, int bestStars, int clears, bool firstClaimed) { Unlocked = unlocked; Cleared = cleared; BestStars = bestStars; Clears = clears; FirstClearClaimed = firstClaimed; }
    }

    /// <summary>Outcome of <see cref="ProgressService.CompleteFight"/>, used by the result screen.</summary>
    public sealed class FightCompletion
    {
        public string FightId;
        public bool Victory;
        public int Stars;
        public int PreviousBestStars;
        public bool FirstClear;
        public string ChapterCompletedId;
        public readonly List<string> NewlyUnlockedFights = new List<string>();
        public RewardResult Rewards;
        public BattleResult Battle;
    }

    /// <summary>Grants rewards for a finished fight (implemented by RewardService). Must apply inside the caller's transaction.</summary>
    public interface IFightRewardGranter
    {
        RewardResult GrantFightRewards(FightDef fight, bool victory, bool firstClear, string chapterCompletedId);
    }

    /// <summary>Campaign state: unlocks, clears, stars, first-clear and chapter rewards, story beats seen.</summary>
    public sealed class ProgressService : IProgressView
    {
        private readonly ServiceContext _ctx;
        private readonly RosterService _roster;
        private readonly PartyService _party;

        public IFightRewardGranter Rewards { get; set; }

        public ProgressService(ServiceContext ctx, RosterService roster, PartyService party)
        {
            _ctx = ctx;
            _roster = roster;
            _party = party;
        }

        // IProgressView
        public bool IsFightCleared(string fightId) => _ctx.State.GetFight(fightId)?.Cleared == true;
        public bool IsHeroUnlocked(string heroId) => _roster.IsUnlocked(heroId);
        public int PartyPower => _roster.GetTotalPower(_party.Members);

        public bool IsFightUnlocked(string fightId) => ProgressionRules.IsFightUnlocked(fightId, _ctx.Db, this);
        public bool IsChapterUnlocked(string chapterId) => ProgressionRules.IsChapterUnlocked(chapterId, _ctx.Db, this);

        public bool IsChapterCleared(string chapterId) =>
            _ctx.Db.TryGetChapter(chapterId, out var c) && c.FightIds.Count > 0 && c.FightIds.All(IsFightCleared);

        public FightProgress GetFightProgress(string fightId)
        {
            var f = _ctx.State.GetFight(fightId);
            return new FightProgress(IsFightUnlocked(fightId), f?.Cleared == true, f?.BestStars ?? 0, f?.Clears ?? 0, f?.FirstClearClaimed == true);
        }

        public int ChapterStars(string chapterId) =>
            _ctx.Db.TryGetChapter(chapterId, out var c) ? c.FightIds.Sum(id => _ctx.State.GetFight(id)?.BestStars ?? 0) : 0;

        public int TotalClearedFights => _ctx.State.Data.Progress.Fights.Count(f => f.Cleared);

        /// <summary>First unlocked fight that is not cleared yet (campaign order); otherwise the last unlocked fight.</summary>
        public string GetNextRecommendedFight()
        {
            string lastUnlocked = null;
            foreach (var c in _ctx.Db.Chapters)
                foreach (var id in c.FightIds)
                {
                    if (!IsFightUnlocked(id)) continue;
                    if (!IsFightCleared(id)) return id;
                    lastUnlocked = id;
                }
            return lastUnlocked ?? _ctx.Db.Fights.FirstOrDefault()?.Id;
        }

        /// <summary>
        /// Records a finished battle: stars, clears, first-clear and chapter-clear rewards. Everything (progress + rewards)
        /// is applied in one transaction and saved immediately, before the result is shown.
        /// </summary>
        /// <param name="unlockVerifiedAtStart">True when the battle was started through <see cref="BattleFlowController"/>, which
        /// checked the unlock at start (party power may legitimately drop mid-battle; a won fight must still be recorded).</param>
        public Result<FightCompletion> CompleteFight(string fightId, BattleResult battle, bool unlockVerifiedAtStart = false)
        {
            if (!_ctx.Db.TryGetFight(fightId, out var fight)) return Result<FightCompletion>.Fail(ErrorCode.NotFound, fightId);
            if (!unlockVerifiedAtStart && !IsFightUnlocked(fightId)) return Result<FightCompletion>.Fail(ErrorCode.FightLocked, fightId);
            if (battle == null) return Result<FightCompletion>.Fail(ErrorCode.InvalidArgument, "battle result");

            var unlockedBefore = _ctx.Db.Fights.Where(f => IsFightUnlocked(f.Id)).Select(f => f.Id).ToList();
            var completion = new FightCompletion { FightId = fightId, Victory = battle.Victory, Battle = battle };

            try
            {
                using (var tx = _ctx.BeginTransaction())
                {
                    var record = _ctx.State.GetOrCreateFight(fightId);
                    completion.PreviousBestStars = record.BestStars;
                    _ctx.State.Data.Progress.TotalFights++;

                    if (battle.Victory)
                    {
                        completion.Stars = ProgressionRules.EvaluateStars(battle, fight, _ctx.Db.Rules);
                        completion.FirstClear = !record.FirstClearClaimed;
                        record.Cleared = true;
                        record.Clears++;
                        record.BestStars = System.Math.Max(record.BestStars, completion.Stars);
                        record.FirstClearClaimed = true;

                        var chapterId = fight.ChapterId;
                        if (IsChapterCleared(chapterId) && !_ctx.State.Data.Progress.ClaimedChapterRewards.Contains(chapterId))
                        {
                            _ctx.State.Data.Progress.ClaimedChapterRewards.Add(chapterId);
                            completion.ChapterCompletedId = chapterId;
                        }
                    }

                    completion.Rewards = Rewards?.GrantFightRewards(fight, battle.Victory, completion.FirstClear, completion.ChapterCompletedId) ?? new RewardResult();
                    _ctx.Commit();
                    _ctx.Emit(new FightCompleted(fightId, battle.Victory, completion.Stars, completion.FirstClear));
                    if (completion.ChapterCompletedId != null) _ctx.Emit(new ChapterCompleted(completion.ChapterCompletedId));
                    tx.Complete(saveNow: true);
                }
            }
            catch (System.Exception e)
            {
                // The transaction was rolled back by Dispose: nothing was recorded or granted.
                _ctx.Log.Error($"[Progress] CompleteFight {fightId} failed and was rolled back: {e.Message}");
                return Result<FightCompletion>.Fail(ErrorCode.Unknown, e.Message);
            }

            completion.NewlyUnlockedFights.AddRange(_ctx.Db.Fights.Where(f => IsFightUnlocked(f.Id) && !unlockedBefore.Contains(f.Id)).Select(f => f.Id));
            return Result<FightCompletion>.Success(completion);
        }

        // ---------- story ----------

        public bool HasSeenBeat(string beatId) => _ctx.State.Data.Progress.SeenStoryBeats.Contains(beatId);

        /// <summary>Beats to show before a fight: unseen ones only (or all when <paramref name="replay"/>).</summary>
        public IReadOnlyList<StoryBeatDef> GetBeats(string fightId, bool post, bool replay = false)
        {
            if (!_ctx.Db.TryGetFight(fightId, out var f)) return new List<StoryBeatDef>();
            var ids = post ? f.PostBeatIds : f.PreBeatIds;
            return ids.Where(id => replay || !HasSeenBeat(id)).Select(id => _ctx.Db.TryGetStoryBeat(id, out var b) ? b : null).Where(b => b != null).ToList();
        }

        public void MarkBeatsSeen(IEnumerable<string> beatIds)
        {
            var seen = _ctx.State.Data.Progress.SeenStoryBeats;
            var changed = false;
            foreach (var id in beatIds)
                if (!seen.Contains(id)) { seen.Add(id); changed = true; }
            if (changed) _ctx.Commit();
        }

        /// <summary>All beats of fights the player has reached (story gallery).</summary>
        public IReadOnlyList<StoryBeatDef> GetSeenBeats() =>
            _ctx.State.Data.Progress.SeenStoryBeats.Select(id => _ctx.Db.TryGetStoryBeat(id, out var b) ? b : null).Where(b => b != null).ToList();
    }
}
