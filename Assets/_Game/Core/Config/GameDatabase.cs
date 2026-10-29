using System;
using System.Collections.Generic;

namespace Game.Core
{
    public interface IConfigProvider
    {
        GameDatabase Database { get; }
    }

    /// <summary>
    /// Immutable-after-build view of all content with ID lookups. Lists keep authoring order;
    /// lookups keep the first definition of a duplicated ID (duplicates are reported by the validator).
    /// </summary>
    public sealed class GameDatabase : IConfigProvider
    {
        public GameRules Rules = new GameRules();
        public CombatRules Combat = new CombatRules();
        public List<RarityDef> Rarities = new List<RarityDef>();
        public List<StarLevelDef> StarLevels = new List<StarLevelDef>();
        public List<HeroDef> Heroes = new List<HeroDef>();
        public List<EnemyDef> Enemies = new List<EnemyDef>();
        public List<SkillDef> Skills = new List<SkillDef>();
        public List<EquipmentTemplateDef> Equipment = new List<EquipmentTemplateDef>();
        public List<DropTableDef> DropTables = new List<DropTableDef>();
        public List<ChapterDef> Chapters = new List<ChapterDef>();
        public List<FightDef> Fights = new List<FightDef>();
        public List<StoryBeatDef> StoryBeats = new List<StoryBeatDef>();

        private readonly Dictionary<string, HeroDef> _heroes = new Dictionary<string, HeroDef>();
        private readonly Dictionary<string, EnemyDef> _enemies = new Dictionary<string, EnemyDef>();
        private readonly Dictionary<string, SkillDef> _skills = new Dictionary<string, SkillDef>();
        private readonly Dictionary<string, EquipmentTemplateDef> _equipment = new Dictionary<string, EquipmentTemplateDef>();
        private readonly Dictionary<string, DropTableDef> _dropTables = new Dictionary<string, DropTableDef>();
        private readonly Dictionary<string, ChapterDef> _chapters = new Dictionary<string, ChapterDef>();
        private readonly Dictionary<string, FightDef> _fights = new Dictionary<string, FightDef>();
        private readonly Dictionary<string, StoryBeatDef> _beats = new Dictionary<string, StoryBeatDef>();
        private readonly Dictionary<Rarity, RarityDef> _rarities = new Dictionary<Rarity, RarityDef>();
        private readonly Dictionary<string, int> _fightOrder = new Dictionary<string, int>();
        private bool _indexed;

        GameDatabase IConfigProvider.Database => this;

        /// <summary>IDs that appear more than once, as "type:id". Filled by <see cref="Index"/>.</summary>
        public readonly List<string> DuplicateIds = new List<string>();

        /// <summary>Builds lookups. Call after filling the lists (or after editing them).</summary>
        public GameDatabase Index()
        {
            DuplicateIds.Clear();
            Fill(_heroes, Heroes, h => h.Id, "hero");
            Fill(_enemies, Enemies, e => e.Id, "enemy");
            Fill(_skills, Skills, s => s.Id, "skill");
            Fill(_equipment, Equipment, e => e.Id, "equipment");
            Fill(_dropTables, DropTables, d => d.Id, "droptable");
            Fill(_chapters, Chapters, c => c.Id, "chapter");
            Fill(_beats, StoryBeats, b => b.Id, "storybeat");

            // Fights: global order follows chapter order then fight order in the chapter.
            Fill(_fights, Fights, f => f.Id, "fight");
            _fightOrder.Clear();
            var order = 0;
            foreach (var c in Chapters)
                foreach (var fid in c.FightIds)
                {
                    if (fid != null && _fights.TryGetValue(fid, out var f))
                    {
                        if (string.IsNullOrEmpty(f.ChapterId)) f.ChapterId = c.Id;
                        if (!_fightOrder.ContainsKey(fid)) _fightOrder[fid] = order++;
                    }
                }

            _rarities.Clear();
            foreach (var r in Rarities)
                if (r != null && !_rarities.ContainsKey(r.Rarity)) _rarities[r.Rarity] = r;
                else if (r != null) DuplicateIds.Add("rarity:" + r.Rarity);

            StarLevels.Sort((a, b) => a.Star.CompareTo(b.Star));
            _indexed = true;
            return this;
        }

        private void Fill<T>(Dictionary<string, T> map, List<T> list, Func<T, string> id, string type) where T : class
        {
            map.Clear();
            foreach (var item in list)
            {
                if (item == null) continue;
                var key = id(item);
                if (string.IsNullOrEmpty(key)) continue;
                if (map.ContainsKey(key)) DuplicateIds.Add(type + ":" + key);
                else map[key] = item;
            }
        }

        private void EnsureIndexed()
        {
            if (!_indexed) Index();
        }

        public bool TryGetHero(string id, out HeroDef def) { EnsureIndexed(); return _heroes.TryGetValue(id ?? "", out def); }
        public bool TryGetEnemy(string id, out EnemyDef def) { EnsureIndexed(); return _enemies.TryGetValue(id ?? "", out def); }
        public bool TryGetSkill(string id, out SkillDef def) { EnsureIndexed(); return _skills.TryGetValue(id ?? "", out def); }
        public bool TryGetEquipment(string id, out EquipmentTemplateDef def) { EnsureIndexed(); return _equipment.TryGetValue(id ?? "", out def); }
        public bool TryGetDropTable(string id, out DropTableDef def) { EnsureIndexed(); return _dropTables.TryGetValue(id ?? "", out def); }
        public bool TryGetChapter(string id, out ChapterDef def) { EnsureIndexed(); return _chapters.TryGetValue(id ?? "", out def); }
        public bool TryGetFight(string id, out FightDef def) { EnsureIndexed(); return _fights.TryGetValue(id ?? "", out def); }
        public bool TryGetStoryBeat(string id, out StoryBeatDef def) { EnsureIndexed(); return _beats.TryGetValue(id ?? "", out def); }
        public bool TryGetRarity(Rarity rarity, out RarityDef def) { EnsureIndexed(); return _rarities.TryGetValue(rarity, out def); }

        public HeroDef GetHero(string id) => TryGetHero(id, out var d) ? d : throw Missing("hero", id);
        public EnemyDef GetEnemy(string id) => TryGetEnemy(id, out var d) ? d : throw Missing("enemy", id);
        public SkillDef GetSkill(string id) => TryGetSkill(id, out var d) ? d : throw Missing("skill", id);
        public EquipmentTemplateDef GetEquipment(string id) => TryGetEquipment(id, out var d) ? d : throw Missing("equipment", id);
        public DropTableDef GetDropTable(string id) => TryGetDropTable(id, out var d) ? d : throw Missing("drop table", id);
        public ChapterDef GetChapter(string id) => TryGetChapter(id, out var d) ? d : throw Missing("chapter", id);
        public FightDef GetFight(string id) => TryGetFight(id, out var d) ? d : throw Missing("fight", id);
        public RarityDef GetRarity(Rarity r) => TryGetRarity(r, out var d) ? d : throw Missing("rarity", r.ToString());

        public bool IsCurrency(string id)
        {
            foreach (var c in Rules.Currencies) if (c != null && c.Id == id) return true;
            return false;
        }

        /// <summary>Position of the fight in the whole campaign (chapter order, then fight order); -1 if not in a chapter.</summary>
        public int GetFightOrder(string fightId)
        {
            EnsureIndexed();
            return _fightOrder.TryGetValue(fightId ?? "", out var o) ? o : -1;
        }

        public int MaxStar => StarLevels.Count == 0 ? 1 : StarLevels[StarLevels.Count - 1].Star;

        public StarLevelDef GetStarLevel(int star)
        {
            foreach (var s in StarLevels) if (s.Star == star) return s;
            return null;
        }

        private static KeyNotFoundException Missing(string type, string id) => new KeyNotFoundException($"Unknown {type} id '{id}'.");
    }
}
