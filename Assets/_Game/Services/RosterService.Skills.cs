using Game.Core;

namespace Game.Services
{
    public readonly struct SkillUpgradeInfo
    {
        public readonly HeroSkillSlot Slot;
        public readonly int Level;
        public readonly int MaxLevel;
        public readonly bool IsMaxLevel;
        public readonly int Cost;
        public readonly bool CanAfford;
        public readonly bool CanUpgrade;
        public readonly float Potency;
        public readonly float NextPotency;

        public SkillUpgradeInfo(HeroSkillSlot slot, int level, int maxLevel, bool isMax, int cost, bool canAfford, bool canUpgrade, float potency, float next)
        {
            Slot = slot; Level = level; MaxLevel = maxLevel; IsMaxLevel = isMax; Cost = cost; CanAfford = canAfford; CanUpgrade = canUpgrade;
            Potency = potency; NextPotency = next;
        }
    }

    /// <summary>Gold-bought skill levels for a hero's active and passive (owner request 2026-10-31).</summary>
    public sealed partial class RosterService
    {
        /// <summary>The skill in that slot as authored (null if the hero has none).</summary>
        public SkillDef GetSkill(string heroId, HeroSkillSlot slot)
        {
            if (!_ctx.Db.TryGetHero(heroId, out var def)) return null;
            var id = slot == HeroSkillSlot.Active ? def.ActiveSkillId : def.PassiveSkillId;
            return _ctx.Db.TryGetSkill(id, out var s) ? s : null;
        }

        public int GetSkillLevel(string heroId, HeroSkillSlot slot)
        {
            var h = _ctx.State.GetHero(heroId);
            if (h == null) return 1;
            var level = slot == HeroSkillSlot.Active ? h.ActiveSkillLevel : h.PassiveSkillLevel;
            return System.Math.Max(1, System.Math.Min(level, _ctx.Db.MaxSkillLevel));
        }

        public SkillUpgradeInfo GetSkillUpgradeInfo(string heroId, HeroSkillSlot slot)
        {
            var level = GetSkillLevel(heroId, slot);
            var max = _ctx.Db.MaxSkillLevel;
            var potency = SkillLevelRules.Potency(level, _ctx.Db);
            var cost = SkillLevelRules.NextCost(level, _ctx.Db);
            if (cost == null || !IsUnlocked(heroId) || GetSkill(heroId, slot) == null)
                return new SkillUpgradeInfo(slot, level, max, cost == null, 0, false, false, potency, potency);
            var afford = Currency != null && Currency.CanAfford(_ctx.Db.Rules.PrimaryCurrencyId, cost.Value);
            return new SkillUpgradeInfo(slot, level, max, false, cost.Value, afford, afford, potency, SkillLevelRules.Potency(level + 1, _ctx.Db));
        }

        /// <summary>Spends the primary currency and raises the skill one level.</summary>
        public Result UpgradeSkill(string heroId, HeroSkillSlot slot)
        {
            if (!_ctx.Db.TryGetHero(heroId, out _)) return Result.Fail(ErrorCode.NotFound, heroId);
            var save = _ctx.State.GetHero(heroId);
            if (save == null || !save.Unlocked) return Result.Fail(ErrorCode.NotUnlocked, heroId);
            if (GetSkill(heroId, slot) == null) return Result.Fail(ErrorCode.NotFound, $"{heroId} has no {slot} skill");
            var level = GetSkillLevel(heroId, slot);
            var cost = SkillLevelRules.NextCost(level, _ctx.Db);
            if (cost == null) return Result.Fail(ErrorCode.MaxSkillLevelReached, heroId);
            var currencyId = _ctx.Db.Rules.PrimaryCurrencyId;
            if (cost.Value > 0 && (Currency == null || !Currency.CanAfford(currencyId, cost.Value)))
                return Result.Fail(ErrorCode.InsufficientFunds, $"need {cost.Value} {currencyId}");

            if (cost.Value > 0) Currency.Spend(currencyId, cost.Value);
            if (slot == HeroSkillSlot.Active) save.ActiveSkillLevel = level + 1;
            else save.PassiveSkillLevel = level + 1;
            _ctx.Commit();
            _ctx.Emit(new HeroSkillUpgraded(heroId, slot, level + 1));
            return Result.Success;
        }
    }
}
