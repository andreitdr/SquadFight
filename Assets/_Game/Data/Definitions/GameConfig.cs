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
        };
    }
}
