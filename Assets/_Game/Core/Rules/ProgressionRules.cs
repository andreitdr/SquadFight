using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>What progression rules need to know about the player.</summary>
    public interface IProgressView
    {
        bool IsFightCleared(string fightId);
        bool IsHeroUnlocked(string heroId);
        int PartyPower { get; }
    }

    /// <summary>Unlock chain and star rating. Stateless.</summary>
    public static class ProgressionRules
    {
        public static bool ConditionsMet(IReadOnlyList<UnlockConditionDef> conditions, IProgressView p)
        {
            foreach (var c in conditions)
            {
                switch (c.Type)
                {
                    case UnlockConditionType.ClearFight: if (!p.IsFightCleared(c.RefId)) return false; break;
                    case UnlockConditionType.HeroOwned: if (!p.IsHeroUnlocked(c.RefId)) return false; break;
                    case UnlockConditionType.MinPartyPower: if (p.PartyPower < c.Value) return false; break;
                }
            }
            return true;
        }

        /// <summary>Default: first chapter open; others open when the previous chapter's last fight is cleared. Conditions override.</summary>
        public static bool IsChapterUnlocked(string chapterId, GameDatabase db, IProgressView p)
        {
            if (!db.TryGetChapter(chapterId, out var chapter)) return false;
            if (chapter.UnlockConditions.Count > 0) return ConditionsMet(chapter.UnlockConditions, p);
            var index = db.Chapters.IndexOf(chapter);
            if (index <= 0) return true;
            var previous = db.Chapters[index - 1];
            return previous.FightIds.Count == 0 || p.IsFightCleared(previous.FightIds[previous.FightIds.Count - 1]);
        }

        /// <summary>
        /// Default: chapter open and previous main fight in the chapter cleared (first fight: chapter open). Conditions override the fight rule.
        /// Side quests: chapter open and their conditions met (they are never part of the chain).
        /// </summary>
        public static bool IsFightUnlocked(string fightId, GameDatabase db, IProgressView p)
        {
            if (!db.TryGetFight(fightId, out var fight) || string.IsNullOrEmpty(fight.ChapterId)) return false;
            if (!IsChapterUnlocked(fight.ChapterId, db, p)) return false;
            if (fight.IsSideQuest || fight.UnlockConditions.Count > 0) return ConditionsMet(fight.UnlockConditions, p);
            var chapter = db.GetChapter(fight.ChapterId);
            var i = chapter.FightIds.IndexOf(fightId);
            return i <= 0 || p.IsFightCleared(chapter.FightIds[i - 1]);
        }

        /// <summary>0 on loss. Win = 1; +1 no hero died; +1 within the round limit. With stars disabled a win is 1.</summary>
        public static int EvaluateStars(BattleResult result, FightDef fight, GameRules rules)
        {
            if (result == null || !result.Victory) return 0;
            if (!rules.StarsEnabled) return 1;
            var stars = 1;
            if (result.PlayerDeaths == 0) stars++;
            var limit = fight.StarTurnLimit > 0 ? fight.StarTurnLimit : rules.DefaultStarTurnLimit;
            if (result.Rounds <= limit) stars++;
            return stars;
        }

        public static int StarTurnLimit(FightDef fight, GameRules rules) => fight.StarTurnLimit > 0 ? fight.StarTurnLimit : rules.DefaultStarTurnLimit;
    }
}
