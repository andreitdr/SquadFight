using Game.Core;

namespace Game.Services
{
    // Events published on the EventBus after a mutation is applied (and the save marked dirty).

    public readonly struct CurrencyChanged
    {
        public readonly string CurrencyId; public readonly long Old; public readonly long New;
        public CurrencyChanged(string id, long old, long @new) { CurrencyId = id; Old = old; New = @new; }
    }

    public readonly struct ShardsChanged
    {
        public readonly string HeroId; public readonly int Old; public readonly int New;
        public ShardsChanged(string heroId, int old, int @new) { HeroId = heroId; Old = old; New = @new; }
    }

    public readonly struct HeroUnlocked
    {
        public readonly string HeroId;
        public HeroUnlocked(string heroId) { HeroId = heroId; }
    }

    public readonly struct HeroStarUpgraded
    {
        public readonly string HeroId; public readonly int NewStar;
        public HeroStarUpgraded(string heroId, int newStar) { HeroId = heroId; NewStar = newStar; }
    }

    public readonly struct HeroSkillUpgraded
    {
        public readonly string HeroId; public readonly HeroSkillSlot Slot; public readonly int NewLevel;
        public HeroSkillUpgraded(string heroId, HeroSkillSlot slot, int newLevel) { HeroId = heroId; Slot = slot; NewLevel = newLevel; }
    }

    public readonly struct PartyChanged { }

    public readonly struct ItemAdded
    {
        public readonly string InstanceId;
        public ItemAdded(string id) { InstanceId = id; }
    }

    public readonly struct ItemEquipped
    {
        public readonly string HeroId; public readonly string InstanceId; public readonly int SlotIndex; public readonly string PreviousOwnerId;
        public ItemEquipped(string heroId, string instanceId, int slotIndex, string previousOwner) { HeroId = heroId; InstanceId = instanceId; SlotIndex = slotIndex; PreviousOwnerId = previousOwner; }
    }

    public readonly struct ItemUnequipped
    {
        public readonly string HeroId; public readonly string InstanceId; public readonly int SlotIndex;
        public ItemUnequipped(string heroId, string instanceId, int slotIndex) { HeroId = heroId; InstanceId = instanceId; SlotIndex = slotIndex; }
    }

    public readonly struct ItemSold
    {
        public readonly string InstanceId; public readonly long Value;
        public ItemSold(string id, long value) { InstanceId = id; Value = value; }
    }

    /// <summary>Published after the bought item's <see cref="ItemAdded"/>.</summary>
    public readonly struct ItemBought
    {
        public readonly string InstanceId; public readonly string TemplateId; public readonly Rarity Rarity; public readonly long Price;
        public ItemBought(string id, string templateId, Rarity rarity, long price) { InstanceId = id; TemplateId = templateId; Rarity = rarity; Price = price; }
    }

    public readonly struct FightCompleted
    {
        public readonly string FightId; public readonly bool Victory; public readonly int Stars; public readonly bool FirstClear;
        public FightCompleted(string fightId, bool victory, int stars, bool firstClear) { FightId = fightId; Victory = victory; Stars = stars; FirstClear = firstClear; }
    }

    public readonly struct ChapterCompleted
    {
        public readonly string ChapterId;
        public ChapterCompleted(string id) { ChapterId = id; }
    }

    public readonly struct RewardsGranted
    {
        public readonly RewardResult Result;
        public RewardsGranted(RewardResult r) { Result = r; }
    }

    /// <summary>The whole player state was replaced (reset / debug load). Views should rebuild.</summary>
    public readonly struct PlayerLevelChanged
    {
        public readonly int OldLevel; public readonly int NewLevel; public readonly int XpGained;
        public PlayerLevelChanged(int oldLevel, int newLevel, int xpGained) { OldLevel = oldLevel; NewLevel = newLevel; XpGained = xpGained; }
    }

    public readonly struct StateReset { }

    public readonly struct SettingsChanged { }
}
