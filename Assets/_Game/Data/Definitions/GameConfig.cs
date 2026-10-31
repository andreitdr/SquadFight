using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    /// <summary>Root content asset (lives in Resources so Boot can load it). Referenced assets are gathered transitively.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "GameConfig";

        [Header("Content")]
        public List<HeroDefinition> heroes = new List<HeroDefinition>();
        public List<EquipmentTemplate> equipment = new List<EquipmentTemplate>();
        [Tooltip("Ordered campaign. Fights, enemies, skills, story beats and drop tables are collected from here.")]
        public List<ChapterDefinition> chapters = new List<ChapterDefinition>();
        [Tooltip("Extra assets not reachable from the lists above (optional).")]
        public List<EnemyDefinition> extraEnemies = new List<EnemyDefinition>();
        public List<SkillDefinition> extraSkills = new List<SkillDefinition>();
        public List<DropTable> extraDropTables = new List<DropTable>();

        [Header("Tables")]
        public RarityConfig rarityConfig;
        public StarUpgradeConfig starConfig;
        public CombatConfig combatConfig;
        public EnemyRankConfig rankConfig;
        public PlayerLevelConfig levelConfig;

        [Header("Party & start")]
        [Range(1, 5)] public int partySize = 5;
        public List<HeroDefinition> startingHeroes = new List<HeroDefinition>();
        public List<CurrencyData> currencies = new List<CurrencyData>();
        public List<CurrencyAmountData> startingCurrencies = new List<CurrencyAmountData>();
        public string primaryCurrencyId = "gold";

        [Header("Heroes")]
        public bool consumeShardsOnUnlock = true;
        public string shardOverflowCurrencyId = "gold";

        [Header("Equipment")]
        public List<EquipmentSlot> slotLayout = new List<EquipmentSlot>
        {
            EquipmentSlot.Weapon, EquipmentSlot.Helmet, EquipmentSlot.Chest, EquipmentSlot.Gloves, EquipmentSlot.Boots, EquipmentSlot.Ring, EquipmentSlot.Ring,
        };
        [Tooltip("0 = unlimited.")] public int inventoryLimit;
        public bool equipmentRollVariation;
        [Range(0, 0.99f)] public float rollVariationPercent = 0.1f;
        public bool sellEnabled = true;

        [Header("Campaign")]
        public bool starsEnabled = true;
        public int defaultStarTurnLimit = 15;
        public DropTable defeatConsolation;

        [Header("Rewards")]
        public float currencyDropMultiplier = 1f;
        public float shardDropMultiplier = 1f;
        public bool pityEnabled;
        public int pityFightsWithoutShard = 5;
        public bool showDropOdds;

        [Header("Meta")]
        [Tooltip("D8: daily login bonus (device clock, tamper tolerant). Off by default.")]
        public bool dailyBonusEnabled;
        [Tooltip("Validator: every hero needs a passive, and no two heroes may share an active or passive behaviour.")]
        public bool uniqueHeroAbilities;
        public DropTable dailyBonusTable;

        [Header("Skill levels & shop")]
        [Tooltip("Gold-bought skill levels: level 1 = as authored (potency 1). Empty = skills cannot be levelled.")]
        public List<SkillLevelData> skillLevels = new List<SkillLevelData>();
        public bool shopEnabled;
        [Tooltip("Slots the gear shop sells.")]
        public List<EquipmentSlot> shopSlots = new List<EquipmentSlot>();
        [Tooltip("Price of a tier-N item at multiplier 1 (index 0 = tier 1).")]
        public List<int> shopTierPrices = new List<int>();
        [Tooltip("Rarities on sale and their price multipliers (also applied to the template's own rarity).")]
        public List<RarityPriceData> shopRarityPrices = new List<RarityPriceData>();

        public GameRules ToRules() => new GameRules
        {
            PartySize = partySize,
            SlotLayout = new List<EquipmentSlot>(slotLayout),
            StartingHeroIds = startingHeroes.Select(h => h ? h.id : null).ToList(),
            StartingCurrencies = startingCurrencies.Select(c => new CurrencyAmount(c.currencyId, c.amount)).ToList(),
            Currencies = currencies.Where(c => c != null).Select(c => new CurrencyDef { Id = c.id, NameKey = c.nameKey }).ToList(),
            PrimaryCurrencyId = primaryCurrencyId,
            ConsumeShardsOnUnlock = consumeShardsOnUnlock,
            ShardOverflowCurrencyId = shardOverflowCurrencyId,
            InventoryLimit = inventoryLimit,
            EquipmentRollVariation = equipmentRollVariation,
            RollVariationPercent = rollVariationPercent,
            SellEnabled = sellEnabled,
            StarsEnabled = starsEnabled,
            DefaultStarTurnLimit = defaultStarTurnLimit,
            DefeatConsolationTableId = defeatConsolation ? defeatConsolation.id : null,
            CurrencyDropMultiplier = currencyDropMultiplier,
            ShardDropMultiplier = shardDropMultiplier,
            PityEnabled = pityEnabled,
            PityFightsWithoutShard = pityFightsWithoutShard,
            ShowDropOdds = showDropOdds,
            DailyBonusEnabled = dailyBonusEnabled,
            UniqueHeroAbilities = uniqueHeroAbilities,
            DailyBonusTableId = dailyBonusTable ? dailyBonusTable.id : null,
            ShopEnabled = shopEnabled,
            ShopSlots = new List<EquipmentSlot>(shopSlots),
            ShopTierPrices = new List<int>(shopTierPrices),
            ShopRarityPrices = shopRarityPrices.Select(p => new RarityPrice(p.rarity, p.multiplier)).ToList(),
        };

        public IEnumerable<SkillLevelDef> SkillLevelsToCore() =>
            skillLevels.Where(l => l != null).Select(l => new SkillLevelDef { Level = l.level, GoldCost = l.goldCost, Potency = l.potency });
    }

    [Serializable]
    public sealed class SkillLevelData
    {
        public int level = 1;
        [Tooltip("Primary currency to reach this level from the one below.")]
        public int goldCost;
        [Tooltip("Multiplier on the skill's damage, healing, shields and status sizes.")]
        public float potency = 1f;
    }

    [Serializable]
    public struct RarityPriceData
    {
        public Rarity rarity;
        public float multiplier;
    }
}
