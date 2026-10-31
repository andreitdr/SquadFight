using System.Collections.Generic;
using Game.Core;

namespace Game.Tests.EditMode
{
    /// <summary>Small, valid, hand-made content set used by pure tests. Numbers are chosen to make hand calculation easy.</summary>
    public static class TestContent
    {
        public const string Gold = "gold";

        public static StatBlock Stats(float hp, float atk, float def, float spd, float crit = 0f, float critDmg = 1.5f) =>
            new StatBlock((StatType.MaxHp, hp), (StatType.Attack, atk), (StatType.Defense, def), (StatType.Speed, spd), (StatType.CritChance, crit), (StatType.CritDamage, critDmg));

        public static GameDatabase Create()
        {
            var db = new GameDatabase();
            db.Rules = new GameRules
            {
                PartySize = 5,
                SlotLayout = new List<EquipmentSlot> { EquipmentSlot.Weapon, EquipmentSlot.Helmet, EquipmentSlot.Chest, EquipmentSlot.Gloves, EquipmentSlot.Boots, EquipmentSlot.Ring, EquipmentSlot.Ring },
                StartingHeroIds = new List<string> { "hero_a", "hero_b" },
                StartingCurrencies = new List<CurrencyAmount> { new CurrencyAmount(Gold, 100) },
                Currencies = new List<CurrencyDef> { new CurrencyDef { Id = Gold, NameKey = "currency_gold_name" } },
                PrimaryCurrencyId = Gold,
                ShardOverflowCurrencyId = Gold,
                DefaultStarTurnLimit = 10,
            };
            db.Combat = new CombatRules
            {
                MinDamage = 1, DefenseK = 1, MaxRounds = 100, CritChanceCap = 1, AiTargeting = AiTargeting.LowestHp,
                PowerWeights = new StatBlock((StatType.MaxHp, 0.1f), (StatType.Attack, 1f), (StatType.Defense, 0.8f), (StatType.Speed, 0.5f), (StatType.CritChance, 100f), (StatType.CritDamage, 20f)),
            };
            db.Rarities = new List<RarityDef>
            {
                new RarityDef { Rarity = Rarity.Common, EquipmentStatMultiplier = 1f, ShardsToUnlock = 10, StarCostMultiplier = 1f, ItemSellValue = 10, OverflowCurrencyPerShard = 5 },
                new RarityDef { Rarity = Rarity.Uncommon, EquipmentStatMultiplier = 1.5f, ShardsToUnlock = 20, StarCostMultiplier = 1.5f, ItemSellValue = 25, OverflowCurrencyPerShard = 10 },
                new RarityDef { Rarity = Rarity.Rare, EquipmentStatMultiplier = 2f, ShardsToUnlock = 40, StarCostMultiplier = 2f, ItemSellValue = 60, OverflowCurrencyPerShard = 20 },
                new RarityDef { Rarity = Rarity.Epic, EquipmentStatMultiplier = 3f, ShardsToUnlock = 80, StarCostMultiplier = 2.5f, ItemSellValue = 150, OverflowCurrencyPerShard = 40 },
                new RarityDef { Rarity = Rarity.Legendary, EquipmentStatMultiplier = 4f, ShardsToUnlock = 160, StarCostMultiplier = 3f, ItemSellValue = 400, OverflowCurrencyPerShard = 80 },
            };
            db.StarLevels = new List<StarLevelDef>
            {
                new StarLevelDef { Star = 1, ShardsRequired = 0, CurrencyCost = 0, StatMultiplier = 1f },
                new StarLevelDef { Star = 2, ShardsRequired = 10, CurrencyCost = 50, StatMultiplier = 1.2f },
                new StarLevelDef { Star = 3, ShardsRequired = 20, CurrencyCost = 100, StatMultiplier = 1.5f },
            };
            db.Skills = new List<SkillDef>
            {
                new SkillDef { Id = "skill_hit", NameKey = "k", Targeting = TargetingRule.SingleEnemy, Effects = { new EffectDef { Type = EffectType.Damage, Power = 1f } } },
                new SkillDef { Id = "skill_aoe", NameKey = "k", Targeting = TargetingRule.AllEnemies, Cooldown = 2, Effects = { new EffectDef { Type = EffectType.Damage, Power = 0.5f } } },
                new SkillDef { Id = "skill_heal", NameKey = "k", Targeting = TargetingRule.LowestHpAlly, Cooldown = 2, Effects = { new EffectDef { Type = EffectType.Heal, Power = 1f } } },
                new SkillDef { Id = "skill_boss_slam", NameKey = "k", Targeting = TargetingRule.HighestAttackEnemy, Cooldown = 3, InitialCooldown = 1, Effects = { new EffectDef { Type = EffectType.Damage, Power = 1.5f } } },
                new SkillDef { Id = "passive_boss_rage", NameKey = "k", Trigger = PassiveTrigger.OnLowHp, Threshold = 0.3f, OncePerBattle = true, Targeting = TargetingRule.Self, Effects = { new EffectDef { Type = EffectType.ApplyStatus, Stat = StatType.Attack, Value = 0.2f, Duration = 3 } } },
                new SkillDef { Id = "special_boss_quake", NameKey = "k", Targeting = TargetingRule.AllEnemies, Threshold = 0.5f, Effects = { new EffectDef { Type = EffectType.Damage, Power = 0.8f } } },
            };
            // Minion ×1 HP keeps hand calculations on enemy_x simple.
            db.Ranks = new List<RankDef>
            {
                new RankDef { Rank = EnemyRank.Minion, HpMultiplier = 1f, Xp = 2, MinSkills = 0 },
                new RankDef { Rank = EnemyRank.Soldier, HpMultiplier = 1.2f, Xp = 3, MinSkills = 1 },
                new RankDef { Rank = EnemyRank.Elite, HpMultiplier = 1.4f, Xp = 5, MinSkills = 2 },
                new RankDef { Rank = EnemyRank.Champion, HpMultiplier = 1.7f, Xp = 8, MinSkills = 3 },
                new RankDef { Rank = EnemyRank.Boss, HpMultiplier = 2f, Xp = 20, MinSkills = 4 },
            };
            db.Heroes = new List<HeroDef>
            {
                Hero("hero_a", Rarity.Common, Stats(500, 50, 20, 100), "skill_aoe"),
                Hero("hero_b", Rarity.Common, Stats(400, 60, 10, 110), null),
                Hero("hero_c", Rarity.Uncommon, Stats(450, 45, 15, 105), "skill_heal"),
                Hero("hero_d", Rarity.Rare, Stats(700, 70, 30, 95), null),
                Hero("hero_e", Rarity.Legendary, Stats(1000, 100, 40, 105), "skill_aoe"),
            };
            db.Enemies = new List<EnemyDef>
            {
                new EnemyDef { Id = "enemy_x", NameKey = "k", Rank = EnemyRank.Minion, BaseStats = Stats(300, 30, 10, 90), BasicSkillId = "skill_hit" },
                new EnemyDef
                {
                    Id = "enemy_boss", NameKey = "k", Rank = EnemyRank.Boss, BaseStats = Stats(750, 60, 25, 85), BasicSkillId = "skill_hit", ActiveSkillId = "skill_aoe",
                    PassiveSkillId = "passive_boss_rage", ExtraSkillIds = { "skill_boss_slam" }, SpecialSkillIds = { "special_boss_quake" },
                },
            };
            db.Equipment = new List<EquipmentTemplateDef>
            {
                new EquipmentTemplateDef { Id = "eq_sword", NameKey = "k", Slot = EquipmentSlot.Weapon, Rarity = Rarity.Common, Stats = { new StatModifier(StatType.Attack, 10) } },
                new EquipmentTemplateDef { Id = "eq_helm", NameKey = "k", Slot = EquipmentSlot.Helmet, Rarity = Rarity.Common, Stats = { new StatModifier(StatType.Defense, 5), new StatModifier(StatType.MaxHp, 50) } },
                new EquipmentTemplateDef { Id = "eq_ring", NameKey = "k", Slot = EquipmentSlot.Ring, Rarity = Rarity.Common, Stats = { new StatModifier(StatType.Attack, 0.1f, ModifierKind.Percent) } },
                new EquipmentTemplateDef { Id = "eq_boots", NameKey = "k", Slot = EquipmentSlot.Boots, Rarity = Rarity.Common, Stats = { new StatModifier(StatType.Speed, 5) } },
            };
            db.DropTables = new List<DropTableDef>
            {
                new DropTableDef
                {
                    Id = "drops_basic",
                    Guaranteed = { new DropEntryDef { Type = DropType.Currency, RefId = Gold, AmountMin = 10, AmountMax = 10 } },
                    Groups =
                    {
                        new RollGroupDef
                        {
                            Entries =
                            {
                                new DropEntryDef { Type = DropType.HeroShard, RefId = "hero_c", Weight = 3, AmountMin = 1, AmountMax = 2 },
                                new DropEntryDef { Type = DropType.RandomHeroShardByRarity, Rarity = Rarity.Rare, Weight = 1 },
                                new DropEntryDef { Type = DropType.RandomEquipment, Weight = 2, RarityWeights = { new RarityWeight(Rarity.Common, 3), new RarityWeight(Rarity.Uncommon, 1) } },
                                new DropEntryDef { Type = DropType.HeroShard, RefId = "hero_e", Weight = 1 },
                            },
                        },
                    },
                },
                new DropTableDef
                {
                    Id = "drops_first",
                    Guaranteed =
                    {
                        new DropEntryDef { Type = DropType.Currency, RefId = Gold, AmountMin = 50, AmountMax = 50 },
                        new DropEntryDef { Type = DropType.Equipment, RefId = "eq_sword", Rarity = Rarity.Uncommon },
                    },
                },
            };
            db.StoryBeats = new List<StoryBeatDef> { new StoryBeatDef { Id = "beat_1", SpeakerKey = "k", TextKey = "k" } };
            db.Fights = new List<FightDef>
            {
                Fight("fight_1", 1, "drops_first"),
                Fight("fight_2", 1, null),
                Fight("fight_3", 2, null, boss: true),
                Fight("fight_4", 1, null),
            };
            db.Fights[0].PreBeatIds.Add("beat_1");
            db.Chapters = new List<ChapterDef>
            {
                new ChapterDef { Id = "chapter_1", NameKey = "k", FightIds = { "fight_1", "fight_2", "fight_3" }, ClearRewardTableId = "drops_first" },
                new ChapterDef { Id = "chapter_2", NameKey = "k", FightIds = { "fight_4" } },
            };
            return db.Index();
        }

        public static HeroDef Hero(string id, Rarity rarity, StatBlock stats, string active) =>
            new HeroDef { Id = id, NameKey = id + "_name", Rarity = rarity, BaseStats = stats, BasicSkillId = "skill_hit", ActiveSkillId = active };

        public static FightDef Fight(string id, int enemies, string firstClear, bool boss = false)
        {
            var f = new FightDef { Id = id, NameKey = id + "_name", DropTableId = "drops_basic", FirstClearTableId = firstClear, RecommendedPower = 100 };
            var wave = new WaveDef();
            for (var i = 0; i < enemies; i++) wave.Spawns.Add(new EnemySpawnDef { EnemyId = "enemy_x" });
            f.Waves.Add(wave);
            if (boss) f.Waves.Add(new WaveDef { Spawns = { new EnemySpawnDef { EnemyId = "enemy_boss", StatMultiplier = 1f } } });
            return f;
        }
    }
}
