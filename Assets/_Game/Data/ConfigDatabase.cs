using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Converts the authored <see cref="GameConfig"/> graph into the pure <see cref="GameDatabase"/> and keeps
    /// id → asset lookups so presentation code can find sprites/colors.
    /// </summary>
    public sealed class ConfigDatabase : IConfigProvider
    {
        public GameConfig Source { get; }
        public GameDatabase Database { get; }

        public readonly List<HeroDefinition> HeroAssets = new List<HeroDefinition>();
        public readonly List<EnemyDefinition> EnemyAssets = new List<EnemyDefinition>();
        public readonly List<SkillDefinition> SkillAssets = new List<SkillDefinition>();
        public readonly List<EquipmentTemplate> EquipmentAssets = new List<EquipmentTemplate>();
        public readonly List<DropTable> DropTableAssets = new List<DropTable>();
        public readonly List<ChapterDefinition> ChapterAssets = new List<ChapterDefinition>();
        public readonly List<FightDefinition> FightAssets = new List<FightDefinition>();
        public readonly List<StoryBeatDefinition> StoryBeatAssets = new List<StoryBeatDefinition>();

        /// <summary>Human-readable problems found while gathering (null list entries etc.).</summary>
        public readonly List<string> GatherIssues = new List<string>();

        private readonly Dictionary<string, HeroDefinition> _heroById = new Dictionary<string, HeroDefinition>();
        private readonly Dictionary<string, EnemyDefinition> _enemyById = new Dictionary<string, EnemyDefinition>();
        private readonly Dictionary<string, SkillDefinition> _skillById = new Dictionary<string, SkillDefinition>();
        private readonly Dictionary<string, EquipmentTemplate> _equipmentById = new Dictionary<string, EquipmentTemplate>();
        private readonly Dictionary<string, FightDefinition> _fightById = new Dictionary<string, FightDefinition>();
        private readonly Dictionary<string, ChapterDefinition> _chapterById = new Dictionary<string, ChapterDefinition>();
        private readonly Dictionary<string, StoryBeatDefinition> _beatById = new Dictionary<string, StoryBeatDefinition>();

        public ConfigDatabase(GameConfig source)
        {
            Source = source;
            Gather(source);
            Database = BuildDatabase();
        }

        public static ConfigDatabase LoadFromResources()
        {
            var config = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            return config ? new ConfigDatabase(config) : null;
        }

        private static void AddUnique<T>(List<T> list, T item) where T : Object
        {
            if (item && !list.Contains(item)) list.Add(item);
        }

        private void Gather(GameConfig c)
        {
            void Null(string where) => GatherIssues.Add($"null entry in {where}");

            foreach (var h in c.heroes) { if (!h) Null("GameConfig.heroes"); AddUnique(HeroAssets, h); }
            foreach (var h in c.startingHeroes) AddUnique(HeroAssets, h);
            foreach (var e in c.equipment) { if (!e) Null("GameConfig.equipment"); AddUnique(EquipmentAssets, e); }
            foreach (var ch in c.chapters)
            {
                if (!ch) { Null("GameConfig.chapters"); continue; }
                AddUnique(ChapterAssets, ch);
                AddUnique(DropTableAssets, ch.clearReward);
                foreach (var f in ch.fights)
                {
                    if (!f) { Null($"chapter {ch.id} fights"); continue; }
                    AddUnique(FightAssets, f);
                    AddUnique(DropTableAssets, f.dropTable);
                    AddUnique(DropTableAssets, f.firstClearTable);
                    foreach (var b in f.preBeats.Concat(f.postBeats))
                        if (!b) Null($"fight {f.id} story beats");
                        else AddUnique(StoryBeatAssets, b);
                    foreach (var w in f.waves)
                        foreach (var s in w.spawns)
                            if (s == null || !s.enemy) Null($"fight {f.id} waves");
                            else AddUnique(EnemyAssets, s.enemy);
                }
            }
            foreach (var e in c.extraEnemies) AddUnique(EnemyAssets, e);
            foreach (var t in c.extraDropTables) AddUnique(DropTableAssets, t);
            AddUnique(DropTableAssets, c.defeatConsolation);
            AddUnique(DropTableAssets, c.dailyBonusTable);
            foreach (var h in HeroAssets) { AddUnique(SkillAssets, h.basicSkill); AddUnique(SkillAssets, h.activeSkill); AddUnique(SkillAssets, h.passiveSkill); }
            foreach (var e in EnemyAssets) { AddUnique(SkillAssets, e.basicSkill); AddUnique(SkillAssets, e.activeSkill); AddUnique(SkillAssets, e.passiveSkill); }
            foreach (var s in c.extraSkills) AddUnique(SkillAssets, s);
        }

        private GameDatabase BuildDatabase()
        {
            var db = new GameDatabase
            {
                Rules = Source.ToRules(),
                Combat = Source.combatConfig ? Source.combatConfig.ToCore() : null,
                Rarities = Source.rarityConfig ? Source.rarityConfig.ToCore().ToList() : new List<RarityDef>(),
                StarLevels = Source.starConfig ? Source.starConfig.ToCore().ToList() : new List<StarLevelDef>(),
                Heroes = HeroAssets.Select(h => h.ToCore()).ToList(),
                Enemies = EnemyAssets.Select(e => e.ToCore()).ToList(),
                Skills = SkillAssets.Select(s => s.ToCore()).ToList(),
                Equipment = EquipmentAssets.Select(e => e.ToCore()).ToList(),
                DropTables = DropTableAssets.Select(t => t.ToCore()).ToList(),
                Chapters = ChapterAssets.Select(ch => ch.ToCore()).ToList(),
                Fights = FightAssets.Select(f => f.ToCore()).ToList(),
                StoryBeats = StoryBeatAssets.Select(b => b.ToCore()).ToList(),
            };
            db.Index();

            Map(_heroById, HeroAssets);
            Map(_enemyById, EnemyAssets);
            Map(_skillById, SkillAssets);
            Map(_equipmentById, EquipmentAssets);
            Map(_fightById, FightAssets);
            Map(_chapterById, ChapterAssets);
            Map(_beatById, StoryBeatAssets);
            return db;
        }

        private static void Map<T>(Dictionary<string, T> map, List<T> assets) where T : ContentDefinition
        {
            foreach (var a in assets)
                if (!string.IsNullOrEmpty(a.id) && !map.ContainsKey(a.id)) map[a.id] = a;
        }

        public HeroDefinition GetHeroAsset(string id) => _heroById.TryGetValue(id ?? "", out var a) ? a : null;
        public EnemyDefinition GetEnemyAsset(string id) => _enemyById.TryGetValue(id ?? "", out var a) ? a : null;
        public SkillDefinition GetSkillAsset(string id) => _skillById.TryGetValue(id ?? "", out var a) ? a : null;
        public EquipmentTemplate GetEquipmentAsset(string id) => _equipmentById.TryGetValue(id ?? "", out var a) ? a : null;
        public FightDefinition GetFightAsset(string id) => _fightById.TryGetValue(id ?? "", out var a) ? a : null;
        public ChapterDefinition GetChapterAsset(string id) => _chapterById.TryGetValue(id ?? "", out var a) ? a : null;
        public StoryBeatDefinition GetStoryBeatAsset(string id) => _beatById.TryGetValue(id ?? "", out var a) ? a : null;

        public Color GetRarityColor(Rarity r)
        {
            var e = Source.rarityConfig ? Source.rarityConfig.Get(r) : null;
            return e != null ? e.color : Color.white;
        }

        public CurrencyData GetCurrency(string id) => Source.currencies.FirstOrDefault(c => c != null && c.id == id);
    }
}
