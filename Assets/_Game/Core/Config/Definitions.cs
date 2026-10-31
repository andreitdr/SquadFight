using System.Collections.Generic;

namespace Game.Core
{
    // Plain runtime config models. Built from ScriptableObjects by Game.Data (or directly by tests/tools).
    // No logic beyond trivial helpers; all numbers come from content.

    public sealed class HeroDef
    {
        public string Id;
        public string NameKey;
        public string LoreKey;
        public Rarity Rarity;
        public HeroClass Class;
        public StatBlock BaseStats = new StatBlock();
        public string BasicSkillId;
        public string ActiveSkillId;
        public string PassiveSkillId;
        /// <summary>Story act the hero belongs to; random shard drops can be limited to an origin pool.</summary>
        public string Origin;
    }

    public sealed class EnemyDef
    {
        public string Id;
        public string NameKey;
        public bool IsBoss;
        public StatBlock BaseStats = new StatBlock();
        public string BasicSkillId;
        public string ActiveSkillId;
        public string PassiveSkillId;
    }

    public sealed class SkillDef
    {
        public string Id;
        public string NameKey;
        public string DescriptionKey;
        public TargetingRule Targeting;
        /// <summary>Number of targets for <see cref="TargetingRule.RandomEnemies"/>.</summary>
        public int TargetCount = 1;
        public int Cooldown;
        /// <summary>Turns before the skill is first usable.</summary>
        public int InitialCooldown;
        public List<EffectDef> Effects = new List<EffectDef>();

        // Passives only (Trigger != None). Cooldown = holder turns between triggers.
        public PassiveTrigger Trigger;
        /// <summary>OnLowHp: HP ratio at or below which it fires (once).</summary>
        public float Threshold;
        /// <summary>Chance 0..1 to fire; 0 is treated as 1.</summary>
        public float Chance;
        public bool OncePerBattle;
        public bool IsPassive => Trigger != PassiveTrigger.None;
    }

    /// <summary>
    /// One effect of a skill. Fields per type ("attack" = caster attack when the effect lands):
    /// Damage/Heal/Shield: Power × attack (Shield lasts Duration) · Stun/Silence/Taunt: Duration, Chance ·
    /// ApplyStatus: Stat ± Value for Duration · DamageOverTime: StatusId (burn/poison/bleed), Power × attack per turn ·
    /// HealOverTime: Power × attack per turn · Drain: Power, heals the caster Value × damage · Execute: Power, ×Value2 at or below Value HP ·
    /// PercentHpDamage: Value × target max HP, capped at Value2 × attack · Cleanse/Dispel: removes Value statuses (0 = all) ·
    /// Revive: one fallen ally of the caster's team at Value × max HP · Thorns: reflects Value of direct damage ·
    /// Dodge: Value chance to evade direct damage · CooldownShift: adds Value to active cooldowns · Mark: +Value damage taken.
    /// Statuses tick down at the end of the holder's turn; DoT/HoT apply at the start of it.
    /// </summary>
    public sealed class EffectDef
    {
        public EffectType Type;
        public EffectTarget Target;
        public float Power;
        public StatType Stat;
        public float Value;
        public float Value2;
        /// <summary>Duration in the holder's turns.</summary>
        public int Duration;
        public StackingRule Stacking;
        public int MaxStacks = 1;
        /// <summary>Identity for stacking; defaults to "skillId#index" for stat changes and to the kind name for other statuses.</summary>
        public string StatusId;
        /// <summary>Chance 0..1 to apply. 0 is treated as 1.</summary>
        public float Chance;
    }

    public sealed class EquipmentTemplateDef
    {
        public string Id;
        public string NameKey;
        public EquipmentSlot Slot;
        /// <summary>Default rarity this template drops at; instances may have any rarity.</summary>
        public Rarity Rarity;
        public List<StatModifier> Stats = new List<StatModifier>();
        /// <summary>Reserved for item sets (v1.1).</summary>
        public string SetId;
    }

    public sealed class RarityDef
    {
        public Rarity Rarity;
        public string ColorHex = "#FFFFFF";
        public float EquipmentStatMultiplier = 1f;
        public int ShardsToUnlock;
        public float StarCostMultiplier = 1f;
        public float DropWeightModifier = 1f;
        public int ItemSellValue;
        public int OverflowCurrencyPerShard;
    }

    public sealed class StarLevelDef
    {
        public int Star;
        /// <summary>Shards needed to go from Star-1 to Star (before rarity multiplier). 0 for star 1.</summary>
        public int ShardsRequired;
        /// <summary>Primary currency cost to go from Star-1 to Star.</summary>
        public int CurrencyCost;
        public float StatMultiplier = 1f;
    }

    public sealed class CurrencyDef
    {
        public string Id;
        public string NameKey;
    }

    public struct CurrencyAmount
    {
        public string CurrencyId;
        public long Amount;
        public CurrencyAmount(string id, long amount) { CurrencyId = id; Amount = amount; }
    }

    public struct RarityWeight
    {
        public Rarity Rarity;
        public int Weight;
        public RarityWeight(Rarity rarity, int weight) { Rarity = rarity; Weight = weight; }
    }

    public sealed class DropEntryDef
    {
        public DropType Type;
        /// <summary>Currency id, hero id or equipment template id depending on <see cref="Type"/>.</summary>
        public string RefId;
        /// <summary>RandomHeroShardByRarity: hero rarity. Equipment: item rarity.</summary>
        public Rarity Rarity;
        /// <summary>RandomEquipment: restrict to one slot (null = any).</summary>
        public EquipmentSlot? SlotFilter;
        /// <summary>RandomEquipment: rarity distribution.</summary>
        public List<RarityWeight> RarityWeights = new List<RarityWeight>();
        public int Weight = 1;
        public int AmountMin = 1;
        public int AmountMax = 1;
    }

    public sealed class RollGroupDef
    {
        public int MinRolls = 1;
        public int MaxRolls = 1;
        public bool WithoutReplacement;
        public List<DropEntryDef> Entries = new List<DropEntryDef>();
    }

    public sealed class DropTableDef
    {
        public string Id;
        public List<DropEntryDef> Guaranteed = new List<DropEntryDef>();
        public List<RollGroupDef> Groups = new List<RollGroupDef>();
    }

    public sealed class EnemySpawnDef
    {
        public string EnemyId;
        /// <summary>Scales the enemy's base stats for this fight (difficulty curve lives in content).</summary>
        public float StatMultiplier = 1f;
    }

    public sealed class WaveDef
    {
        public List<EnemySpawnDef> Spawns = new List<EnemySpawnDef>();
    }

    public sealed class UnlockConditionDef
    {
        public UnlockConditionType Type;
        public string RefId;
        public int Value;
    }

    public sealed class FightDef
    {
        public string Id;
        public string NameKey;
        /// <summary>Filled by <see cref="GameDatabase"/> from the chapter that lists the fight.</summary>
        public string ChapterId;
        public List<WaveDef> Waves = new List<WaveDef>();
        public int RecommendedPower;
        public string DropTableId;
        public string FirstClearTableId;
        /// <summary>Third star: win within this many rounds (0 = use GameRules default).</summary>
        public int StarTurnLimit;
        public string BackgroundKey;
        public List<string> PreBeatIds = new List<string>();
        public List<string> PostBeatIds = new List<string>();
        /// <summary>Overrides the default "previous fight cleared" rule when non-empty. Side quests open only through these.</summary>
        public List<UnlockConditionDef> UnlockConditions = new List<UnlockConditionDef>();
        /// <summary>Optional zone for lore and better items; never counts toward chapter completion, stars or the unlock chain.</summary>
        public bool IsSideQuest;
    }

    public sealed class ChapterDef
    {
        public string Id;
        public string NameKey;
        /// <summary>Main fights in order: these alone decide completion, stars and what unlocks next.</summary>
        public List<string> FightIds = new List<string>();
        /// <summary>Optional side quests of the chapter (see <see cref="FightDef.IsSideQuest"/>).</summary>
        public List<string> SideQuestIds = new List<string>();
        /// <summary>Overrides the default "previous chapter cleared" rule when non-empty.</summary>
        public List<UnlockConditionDef> UnlockConditions = new List<UnlockConditionDef>();
        public string ClearRewardTableId;
    }

    public sealed class StoryBeatDef
    {
        public string Id;
        public string SpeakerKey;
        public string TextKey;
    }

    public sealed class CombatRules
    {
        public float MinDamage = 1f;
        public float DefenseK = 1f;
        /// <summary>Rounds before a battle times out (counted as defeat).</summary>
        public int MaxRounds = 100;
        public float CritChanceCap = 1f;
        public AiTargeting AiTargeting = AiTargeting.LowestHp;
        /// <summary>Weights for <c>PowerRating</c>: power = Σ stat × weight.</summary>
        public StatBlock PowerWeights = new StatBlock();
    }

    public sealed class GameRules
    {
        public int PartySize = 5;
        public List<EquipmentSlot> SlotLayout = new List<EquipmentSlot>();
        public List<string> StartingHeroIds = new List<string>();
        public List<CurrencyAmount> StartingCurrencies = new List<CurrencyAmount>();
        public List<CurrencyDef> Currencies = new List<CurrencyDef>();
        public string PrimaryCurrencyId = "gold";

        // Heroes (step 05)
        public bool ConsumeShardsOnUnlock = true;
        public string ShardOverflowCurrencyId = "gold";

        // Equipment (step 06)
        public int InventoryLimit;
        public bool EquipmentRollVariation;
        public float RollVariationPercent;
        public bool SellEnabled = true;

        // Campaign (step 09)
        public bool StarsEnabled = true;
        public int DefaultStarTurnLimit = 15;
        public string DefeatConsolationTableId;

        // Rewards (step 10)
        public float CurrencyDropMultiplier = 1f;
        public float ShardDropMultiplier = 1f;
        public bool PityEnabled;
        public int PityFightsWithoutShard = 5;
        public bool ShowDropOdds;

        // Meta (step 18)
        public bool DailyBonusEnabled;

        // Roster ("The Hundred")
        /// <summary>Validator rejects two heroes with the same active or passive behaviour, and heroes without a passive.</summary>
        public bool UniqueHeroAbilities;
        public string DailyBonusTableId;
    }
}
