using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Services
{
    /// <summary>One scripted onboarding step: highlight an anchor, show a hint, advance when it is tapped.</summary>
    public sealed class TutorialStep
    {
        public string Id;
        /// <summary>TutorialAnchor id to highlight; tapping it advances.</summary>
        public string Anchor;
        public string TextKey;
        /// <summary>Step to restart from after an app restart: screens are not restored, so deeper steps go back to one reachable from Home.</summary>
        public int ResumeAt;
        /// <summary>True when the player already did this step's action (e.g. before a restart): entering it skips <see cref="SkipCount"/> steps.</summary>
        public Func<PlayerState, bool> AlreadyDone;
        public int SkipCount = 1;

        public TutorialStep(string id, string anchor, string textKey, int resumeAt, Func<PlayerState, bool> alreadyDone = null, int skipCount = 1)
        {
            Id = id;
            Anchor = anchor;
            TextKey = textKey;
            ResumeAt = resumeAt;
            AlreadyDone = alreadyDone;
            SkipCount = skipCount;
        }
    }

    public readonly struct TutorialChanged { }

    /// <summary>
    /// First-session script: fight → reward → guided unlock (the first fight grants enough shards) → party → equip → second fight.
    /// Progress is saved after every step so the tutorial resumes after a restart. Skippable at any time.
    /// </summary>
    public sealed class TutorialService
    {
        private readonly ServiceContext _ctx;

        public const string FirstFight = "fight_1_1", SecondFight = "fight_1_2", GuidedHero = "hero_cleric";

        // Steps whose target disappears once done (unlock, add to party, equip, fights) are skipped when already done, so a
        // restart in the middle never waits on a button that no longer exists. Popup steps right after an action are
        // skipped together with it (SkipCount 2): their popup only shows when the action happens.
        public static readonly IReadOnlyList<TutorialStep> Steps = new[]
        {
            new TutorialStep("continue", "home_continue", "tut_continue", 0, s => Cleared(s, FirstFight), 3),
            new TutorialStep("fight", "preview_start", "tut_fight", 0),
            new TutorialStep("result_home", "result_home", "tut_result_home", 0),
            new TutorialStep("heroes_tab", "tab_heroes", "tut_heroes_tab", 3),
            new TutorialStep("pick_hero", "hero_" + GuidedHero, "tut_pick_hero", 3),
            new TutorialStep("unlock", "hero_unlock", "tut_unlock", 3, s => s.GetHero(GuidedHero)?.Unlocked == true, 2),
            new TutorialStep("unlock_ok", "unlock_ok", "tut_unlock_ok", 3),
            new TutorialStep("add_party", "hero_add_party", "tut_add_party", 3, s => s.Data.Party.Contains(GuidedHero)),
            new TutorialStep("equip_slot", "equip_slot0", "tut_equip_slot", 3, s => s.Data.Equipment.Any(e => e.EquippedByHeroId == GuidedHero && e.EquippedSlotIndex == 0), 2),
            new TutorialStep("equip_item", "picker_first", "tut_equip_item", 3),
            new TutorialStep("home_tab", "tab_home", "tut_home_tab", 10),
            new TutorialStep("continue2", "home_continue", "tut_continue2", 11, s => Cleared(s, SecondFight), 2),
            new TutorialStep("fight2", "preview_start", "tut_fight2", 11),
        };

        private static bool Cleared(PlayerState s, string fightId) => s.GetFight(fightId)?.Cleared == true;

        /// <summary>One-time contextual tips (shown by the UI the first time the situation occurs).</summary>
        public static readonly string[] Tips = { "tip_shards", "tip_starup", "tip_equipment", "tip_party_power", "tip_daily" };

        public TutorialService(ServiceContext ctx)
        {
            _ctx = ctx;
            Resume();
        }

        /// <summary>
        /// After a restart the app opens on Home: go back to the step's resume point and skip what the player already did.
        /// Not saved here; the next commit persists it.
        /// </summary>
        private void Resume()
        {
            if (IsComplete) return;
            S.Step = Steps[S.Step].ResumeAt;
            SkipDone();
        }

        private void SkipDone()
        {
            while (!IsComplete && Steps[S.Step].AlreadyDone?.Invoke(_ctx.State) == true) S.Step += Steps[S.Step].SkipCount;
        }

        private Game.Save.TutorialSave S => _ctx.State.Data.Tutorial;

        public bool IsComplete => S.Skipped || S.Step >= Steps.Count;
        public int StepIndex => Math.Min(S.Step, Steps.Count);
        public TutorialStep Current => IsComplete ? null : Steps[S.Step];

        /// <summary>Call when the player tapped <paramref name="anchorId"/>; advances if it is the current step's anchor.</summary>
        public bool OnAnchorTapped(string anchorId)
        {
            var step = Current;
            if (step == null || step.Anchor != anchorId) return false;
            S.Step++;
            SkipDone();
            _ctx.Commit();
            _ctx.Emit(new TutorialChanged());
            return true;
        }

        public void Skip()
        {
            if (IsComplete) return;
            S.Skipped = true;
            _ctx.Commit();
            _ctx.Emit(new TutorialChanged());
        }

        /// <summary>Lets the player see the tutorial again (Settings).</summary>
        public void Restart()
        {
            S.Skipped = false;
            S.Step = 0;
            _ctx.Commit();
            _ctx.Emit(new TutorialChanged());
        }

        public bool ShouldShowTip(string tipId) => IsComplete && !S.SeenTips.Contains(tipId);

        public void MarkTipSeen(string tipId)
        {
            if (S.SeenTips.Contains(tipId)) return;
            S.SeenTips.Add(tipId);
            _ctx.Commit();
        }
    }

    /// <summary>
    /// Optional daily login bonus (GameRules.DailyBonusEnabled, D8 default off). Uses the device's local date; tolerant of
    /// clock tampering: a claim is allowed only on a date strictly after the last claim, so moving the clock back never
    /// grants extra rewards, and moving it forward only skips days (streak resets on gaps).
    /// </summary>
    public sealed class DailyBonusService
    {
        private readonly ServiceContext _ctx;
        private readonly RewardService _rewards;

        public DailyBonusService(ServiceContext ctx, RewardService rewards)
        {
            _ctx = ctx;
            _rewards = rewards;
        }

        public bool Enabled => _ctx.Db.Rules.DailyBonusEnabled && _ctx.Db.TryGetDropTable(_ctx.Db.Rules.DailyBonusTableId, out _);

        private static string Day(DateTime localNow) => localNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        public bool CanClaim(DateTime localNow)
        {
            if (!Enabled) return false;
            var last = _ctx.State.Data.Daily.LastClaimDate;
            return string.IsNullOrEmpty(last) || string.CompareOrdinal(Day(localNow), last) > 0;
        }

        public int Streak => _ctx.State.Data.Daily.Streak;

        public Result<RewardResult> Claim(DateTime localNow)
        {
            if (!Enabled) return Result<RewardResult>.Fail(ErrorCode.FeatureDisabled);
            if (!CanClaim(localNow)) return Result<RewardResult>.Fail(ErrorCode.AlreadyClaimed);
            var daily = _ctx.State.Data.Daily;
            var today = Day(localNow);
            var yesterday = Day(localNow.AddDays(-1));
            using (var tx = _ctx.BeginTransaction())
            {
                daily.Streak = daily.LastClaimDate == yesterday ? daily.Streak + 1 : 1;
                daily.LastClaimDate = today;
                var bundle = DropRoller.Roll(_ctx.Db.GetDropTable(_ctx.Db.Rules.DailyBonusTableId), _ctx.Random, _ctx.Db);
                var r = _rewards.Apply(bundle);
                if (!r.Ok) return r;
                _ctx.Commit();
                tx.Complete(saveNow: true);
                return r;
            }
        }
    }
}
