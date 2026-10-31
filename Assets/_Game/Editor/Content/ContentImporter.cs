using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools.Content
{
    /// <summary>
    /// Creates/updates content ScriptableObjects from the CSV files in Content/Source, matching assets by id.
    /// Idempotent; never deletes assets (ids missing from the CSVs are reported as orphans).
    /// Schema: CONTENT_SCHEMA.md at the repository root.
    /// </summary>
    public static class ContentImporter
    {
        public sealed class Report
        {
            public readonly List<string> Created = new List<string>();
            public readonly List<string> Updated = new List<string>();
            public readonly List<string> Unchanged = new List<string>();
            public readonly List<string> Orphans = new List<string>();
            public readonly List<string> Errors = new List<string>();
            public bool DryRun;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Content import{(DryRun ? " (preview)" : "")}: {Created.Count} created, {Updated.Count} updated, {Unchanged.Count} unchanged, {Orphans.Count} orphan(s), {Errors.Count} error(s)");
                foreach (var e in Errors) sb.AppendLine("ERROR " + e);
                foreach (var c in Created) sb.AppendLine("+ " + c);
                foreach (var u in Updated) sb.AppendLine("~ " + u);
                foreach (var o in Orphans) sb.AppendLine("? orphan (not deleted): " + o);
                return sb.ToString();
            }
        }

        public static readonly string[] SourceFiles = { "config", "rarities", "stars", "ranks", "levels", "skills", "passives", "heroes", "enemies", "equipment", "story", "droptables", "chapters", "fights" };

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [MenuItem("Tools/Game/Content/Import CSV Content")]
        public static void ImportMenu()
        {
            var preview = Import(dryRun: true);
            if (preview.Errors.Count > 0)
            {
                Debug.LogError(preview.ToString());
                EditorUtility.DisplayDialog("Content import", $"{preview.Errors.Count} error(s); nothing was changed. See Console.", "OK");
                return;
            }
            Debug.Log(preview.ToString());
            var msg = $"{preview.Created.Count} asset(s) will be created and {preview.Updated.Count} updated.\n{preview.Orphans.Count} orphan asset(s) will be kept.\nDetails (diff) are in the Console.";
            if (!EditorUtility.DisplayDialog("Content import", msg + "\n\nApply?", "Import", "Cancel")) return;
            Debug.Log(Import(dryRun: false).ToString());
        }

        /// <summary>Batch entry point (no dialogs).</summary>
        public static void ImportBatch()
        {
            var report = Import(dryRun: false);
            Debug.Log(report.ToString());
            if (report.Errors.Count > 0) throw new Exception("Content import failed:\n" + string.Join("\n", report.Errors));
        }

        public static Report Import(bool dryRun, string sourceFolder = ContentPaths.Source)
        {
            var report = new Report { DryRun = dryRun };
            var src = new Dictionary<string, CsvTable>();
            foreach (var name in SourceFiles)
            {
                var p = Path.Combine(sourceFolder, name + ".csv");
                if (!File.Exists(p)) { report.Errors.Add($"missing source file {p}"); continue; }
                src[name] = Csv.Read(p);
            }
            if (report.Errors.Count > 0) return report;

            if (!dryRun)
            {
                // Parse/validate everything in preview mode first so a bad row aborts before anything is written.
                var check = Import(dryRun: true, sourceFolder);
                if (check.Errors.Count > 0) { report.Errors.AddRange(check.Errors); return report; }
                PlaceholderArt.EnsureAll();
            }

            var ctx = new ImportContext(report, dryRun);
            try
            {
                ctx.Run(src);
            }
            catch (FormatException e) { report.Errors.Add(e.Message); }
            finally
            {
                ctx.Cleanup();
            }
            if (!dryRun && report.Errors.Count == 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            return report;
        }

        private sealed class ImportContext
        {
            private readonly Report _r;
            private readonly bool _dry;
            private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();
            private readonly Dictionary<Type, Dictionary<string, ContentDefinition>> _existing = new Dictionary<Type, Dictionary<string, ContentDefinition>>();
            private readonly Dictionary<Type, HashSet<string>> _seen = new Dictionary<Type, HashSet<string>>();
            private readonly HashSet<UnityEngine.Object> _created = new HashSet<UnityEngine.Object>();

            public ImportContext(Report r, bool dry) { _r = r; _dry = dry; }

            /// <summary>Preview mode creates in-memory instances only; destroy them.</summary>
            public void Cleanup()
            {
                if (!_dry) return;
                foreach (var o in _created) if (o) UnityEngine.Object.DestroyImmediate(o);
            }

            public void Run(Dictionary<string, CsvTable> src)
            {
                var cfg = new Dictionary<string, string>();
                foreach (var r in src["config"].Rows) cfg[r.Req("key")] = r.Str("value");

                var rarity = GetOrCreateSingleton<RarityConfig>(ContentPaths.Config + "/RarityConfig.asset");
                Track(rarity, "RarityConfig", () =>
                {
                    rarity.entries = src["rarities"].Rows.Select(r => new RarityConfig.Entry
                    {
                        rarity = r.Enum<Rarity>("rarity"), color = ParseColor(r.Req("color"), r.Context),
                        equipmentStatMultiplier = r.Float("equipmentStatMultiplier", 1), shardsToUnlock = r.Int("shardsToUnlock"),
                        starCostMultiplier = r.Float("starCostMultiplier", 1), dropWeightModifier = r.Float("dropWeightModifier", 1),
                        itemSellValue = r.Int("itemSellValue"), overflowCurrencyPerShard = r.Int("overflowCurrencyPerShard"),
                    }).ToList();
                });

                var stars = GetOrCreateSingleton<StarUpgradeConfig>(ContentPaths.Config + "/StarUpgradeConfig.asset");
                Track(stars, "StarUpgradeConfig", () =>
                {
                    stars.levels = src["stars"].Rows.Select(r => new StarUpgradeConfig.Level
                    {
                        star = r.Int("star"), shardsRequired = r.Int("shardsRequired"), currencyCost = r.Int("currencyCost"), statMultiplier = r.Float("statMultiplier", 1),
                    }).ToList();
                });

                var ranks = GetOrCreateSingleton<EnemyRankConfig>(ContentPaths.Config + "/EnemyRankConfig.asset");
                Track(ranks, "EnemyRankConfig", () =>
                {
                    ranks.entries = src["ranks"].Rows.Select(r => new EnemyRankConfig.Entry
                    {
                        rank = r.Enum<EnemyRank>("rank"), hpMultiplier = r.Float("hpMultiplier", 1), xp = r.Int("xp"), minSkills = r.Int("minSkills"),
                    }).ToList();
                });

                var combat = GetOrCreateSingleton<CombatConfig>(ContentPaths.Config + "/CombatConfig.asset");
                Track(combat, "CombatConfig", () =>
                {
                    combat.minDamage = CfgFloat(cfg, "combat.minDamage", 1);
                    combat.defenseK = CfgFloat(cfg, "combat.defenseK", 1);
                    combat.maxRounds = (int)CfgFloat(cfg, "combat.maxRounds", 100);
                    combat.critChanceCap = CfgFloat(cfg, "combat.critChanceCap", 1);
                    combat.aiTargeting = ParseEnum<AiTargeting>(Cfg(cfg, "combat.aiTargeting", "LowestHp"), "config combat.aiTargeting");
                    combat.powerWeights.Set(ParseStatBlock(Cfg(cfg, "combat.powerWeights", ""), "config combat.powerWeights"));
                });

                // Skills
                var skills = new Dictionary<string, SkillDefinition>();
                foreach (var row in src["skills"].Rows)
                {
                    var id = row.Req("id");
                    var s = GetOrCreate<SkillDefinition>(ContentPaths.Skills, id, row.Context);
                    skills[id] = s;
                    Track(s, "skill " + id, () =>
                    {
                        s.nameKey = Text(id + "_name", row.Req("name"));
                        s.descriptionKey = Text(id + "_desc", row.Str("description"));
                        s.targeting = row.Enum<TargetingRule>("targeting");
                        s.targetCount = Math.Max(1, row.Int("targetCount", 1));
                        s.cooldown = row.Int("cooldown");
                        s.initialCooldown = row.Int("initialCooldown");
                        s.effects = ParseEffects(row.Req("effects"), row.Context);
                        s.icon = PlaceholderArt.Get("star");
                        s.trigger = PassiveTrigger.None;
                        s.threshold = row.Float("threshold"); // boss specials only
                        s.chance = 0;
                        s.oncePerBattle = false;
                    });
                }
                // Passives: skills with a trigger (same asset type, same folder)
                foreach (var row in src["passives"].Rows)
                {
                    var id = row.Req("id");
                    if (skills.ContainsKey(id)) throw new FormatException($"{row.Context}: passive id '{id}' is also a skill id");
                    var s = GetOrCreate<SkillDefinition>(ContentPaths.Skills, id, row.Context);
                    skills[id] = s;
                    Track(s, "passive " + id, () =>
                    {
                        s.nameKey = Text(id + "_name", row.Req("name"));
                        s.descriptionKey = Text(id + "_desc", row.Str("description"));
                        s.trigger = row.Enum<PassiveTrigger>("trigger");
                        if (s.trigger == PassiveTrigger.None) throw new FormatException($"{row.Context}: passive needs a trigger");
                        s.threshold = row.Float("threshold");
                        s.chance = row.Float("chance");
                        s.cooldown = row.Int("cooldown");
                        s.initialCooldown = 0;
                        s.oncePerBattle = row.Bool("oncePerBattle");
                        s.targeting = string.IsNullOrEmpty(row.Str("targeting")) ? TargetingRule.Self : row.Enum<TargetingRule>("targeting");
                        s.targetCount = Math.Max(1, row.Int("targetCount", 1));
                        s.effects = ParseEffects(row.Req("effects"), row.Context);
                        s.icon = PlaceholderArt.Get("star");
                    });
                }

                SkillDefinition SkillRef(CsvRow row, string col, bool required)
                {
                    var v = row.Str(col);
                    if (string.IsNullOrEmpty(v)) { if (required) throw new FormatException($"{row.Context}: '{col}' is required"); return null; }
                    if (!skills.TryGetValue(v, out var s)) throw new FormatException($"{row.Context}: unknown skill '{v}'");
                    return s;
                }

                List<SkillDefinition> SkillList(CsvRow row, string col) => ParseList(row.Str(col))
                    .Select(v => skills.TryGetValue(v, out var s) ? s : throw new FormatException($"{row.Context}: unknown skill '{v}' in '{col}'")).ToList();

                // Heroes
                var heroes = new Dictionary<string, HeroDefinition>();
                var heroOrder = new List<HeroDefinition>();
                foreach (var row in src["heroes"].Rows)
                {
                    var id = row.Req("id");
                    var h = GetOrCreate<HeroDefinition>(ContentPaths.Heroes, id, row.Context);
                    heroes[id] = h;
                    heroOrder.Add(h);
                    Track(h, "hero " + id, () =>
                    {
                        h.nameKey = Text(id + "_name", row.Req("name"));
                        h.loreKey = Text(id + "_lore", row.Str("lore"));
                        h.rarity = row.Enum<Rarity>("rarity");
                        h.heroClass = row.Enum<HeroClass>("class");
                        h.baseStats.Set(ReadStats(row));
                        h.basicSkill = SkillRef(row, "basicSkill", true);
                        h.activeSkill = SkillRef(row, "activeSkill", false);
                        h.passiveSkill = SkillRef(row, "passive", false);
                        h.origin = row.Str("origin");
                        h.tint = ParseColor(row.Req("tint"), row.Context);
                        if (h.portrait == null) h.portrait = PlaceholderArt.Get("circle"); // keep real art
                        if (h.shardIcon == null) h.shardIcon = PlaceholderArt.Get("shard");
                    });
                }

                // Enemies
                var enemies = new Dictionary<string, EnemyDefinition>();
                foreach (var row in src["enemies"].Rows)
                {
                    var id = row.Req("id");
                    var e = GetOrCreate<EnemyDefinition>(ContentPaths.Enemies, id, row.Context);
                    enemies[id] = e;
                    Track(e, "enemy " + id, () =>
                    {
                        e.nameKey = Text(id + "_name", row.Req("name"));
                        e.rank = row.Str("rank").Length > 0 ? row.Enum<EnemyRank>("rank") : row.Bool("isBoss") ? EnemyRank.Boss : EnemyRank.Soldier;
                        e.baseStats.Set(ReadStats(row));
                        e.basicSkill = SkillRef(row, "basicSkill", true);
                        e.activeSkill = SkillRef(row, "activeSkill", false);
                        e.passiveSkill = SkillRef(row, "passive", false);
                        e.extraSkills = SkillList(row, "extraSkills");
                        e.specials = SkillList(row, "specials");
                        e.artFrom = row.Str("art");
                        e.tint = ParseColor(row.Req("tint"), row.Context);
                        if (e.sprite == null) e.sprite = PlaceholderArt.Get(e.isBoss ? "star" : "diamond"); // keep real art
                    });
                }

                // Equipment
                var equipment = new List<EquipmentTemplate>();
                foreach (var row in src["equipment"].Rows)
                {
                    var id = row.Req("id");
                    var t = GetOrCreate<EquipmentTemplate>(ContentPaths.Equipment, id, row.Context);
                    equipment.Add(t);
                    Track(t, "equipment " + id, () =>
                    {
                        t.nameKey = Text(id + "_name", row.Req("name"));
                        t.slot = row.Enum<EquipmentSlot>("slot");
                        t.rarity = row.Enum<Rarity>("rarity");
                        t.stats = ParseModifiers(row.Req("stats"), row.Context);
                        t.setId = row.Str("setId");
                        t.tier = row.Int("tier", 1);
                        if (t.icon == null) t.icon = PlaceholderArt.Get("icon_" + t.slot.ToString().ToLowerInvariant()); // keep real art
                    });
                }

                // Story beats
                var beats = new Dictionary<string, StoryBeatDefinition>();
                foreach (var row in src["story"].Rows)
                {
                    var id = row.Req("id");
                    var b = GetOrCreate<StoryBeatDefinition>(ContentPaths.Story, id, row.Context);
                    beats[id] = b;
                    Track(b, "story " + id, () =>
                    {
                        var speaker = row.Req("speaker");
                        var isUnit = heroes.ContainsKey(speaker) || enemies.ContainsKey(speaker);
                        b.speakerKey = isUnit ? speaker + "_name" : Text("speaker_" + speaker, row.Str("speakerName", speaker));
                        b.textKey = Text(id + "_text", row.Req("text"));
                        if (b.portrait == null) b.portrait = PlaceholderArt.Get(heroes.ContainsKey(speaker) ? "circle" : enemies.ContainsKey(speaker) ? "diamond" : "rounded"); // keep real art
                    });
                }

                // Drop tables
                var tables = new Dictionary<string, DropTable>();
                foreach (var grp in src["droptables"].Rows.GroupBy(r => r.Req("table")))
                {
                    var id = grp.Key;
                    var t = GetOrCreate<DropTable>(ContentPaths.DropTables, id, grp.First().Context);
                    tables[id] = t;
                    Track(t, "droptable " + id, () =>
                    {
                        t.guaranteed = new List<DropEntryData>();
                        t.groups = new List<RollGroupData>();
                        var groups = new Dictionary<string, RollGroupData>();
                        foreach (var row in grp)
                        {
                            var entry = ParseDropEntry(row);
                            var g = row.Req("group");
                            if (g.Equals("G", StringComparison.OrdinalIgnoreCase)) { t.guaranteed.Add(entry); continue; }
                            if (!groups.TryGetValue(g, out var rg))
                            {
                                var min = row.Int("minRolls", 1);
                                rg = new RollGroupData { minRolls = min, maxRolls = row.Int("maxRolls", min), withoutReplacement = row.Bool("withoutReplacement") };
                                groups[g] = rg;
                                t.groups.Add(rg);
                            }
                            rg.entries.Add(entry);
                        }
                    });
                }

                DropTable TableRef(CsvRow row, string col)
                {
                    var v = row.Str(col);
                    if (string.IsNullOrEmpty(v)) return null;
                    if (!tables.TryGetValue(v, out var t)) throw new FormatException($"{row.Context}: unknown drop table '{v}'");
                    return t;
                }

                StoryBeatDefinition BeatRef(CsvRow row, string b) =>
                    beats.TryGetValue(b, out var x) ? x : throw new FormatException($"{row.Context}: unknown story beat '{b}'");

                // Fights
                var fightsByChapter = new Dictionary<string, List<(int order, FightDefinition fight)>>();
                var sideQuests = new HashSet<FightDefinition>();
                foreach (var row in src["fights"].Rows)
                {
                    var id = row.Req("id");
                    var f = GetOrCreate<FightDefinition>(ContentPaths.Fights, id, row.Context);
                    var ch = row.Req("chapter");
                    if (!fightsByChapter.TryGetValue(ch, out var list)) fightsByChapter[ch] = list = new List<(int, FightDefinition)>();
                    list.Add((row.Int("order"), f));
                    var kind = row.Str("kind");
                    if (kind.Length == 0) kind = "Main";
                    if (!kind.Equals("Main", StringComparison.OrdinalIgnoreCase) && !kind.Equals("Side", StringComparison.OrdinalIgnoreCase))
                        throw new FormatException($"{row.Context}: kind must be Main or Side");
                    var isSide = kind.Equals("Side", StringComparison.OrdinalIgnoreCase);
                    if (isSide) sideQuests.Add(f);
                    Track(f, "fight " + id, () =>
                    {
                        f.nameKey = Text(id + "_name", row.Req("name"));
                        f.waves = ParseWaves(row.Req("waves"), enemies, row.Context);
                        f.recommendedPower = row.Int("recommendedPower");
                        f.dropTable = TableRef(row, "dropTable");
                        f.firstClearTable = TableRef(row, "firstClearTable");
                        f.starTurnLimit = row.Int("starTurnLimit");
                        if (f.background == null) f.background = PlaceholderArt.Get("background");
                        f.preBeats = ParseList(row.Str("preBeats")).Select(b => BeatRef(row, b)).ToList();
                        f.postBeats = ParseList(row.Str("postBeats")).Select(b => BeatRef(row, b)).ToList();
                        f.unlockConditions = ParseConditions(row.Str("unlockConditions"), row.Context);
                        f.isSideQuest = isSide;
                    });
                }

                // Chapters
                var chapters = new List<(int order, ChapterDefinition chapter)>();
                foreach (var row in src["chapters"].Rows)
                {
                    var id = row.Req("id");
                    var c = GetOrCreate<ChapterDefinition>(ContentPaths.Chapters, id, row.Context);
                    chapters.Add((row.Int("order"), c));
                    Track(c, "chapter " + id, () =>
                    {
                        c.nameKey = Text(id + "_name", row.Req("name"));
                        c.clearReward = TableRef(row, "clearReward");
                        c.unlockConditions = ParseConditions(row.Str("unlockConditions"), row.Context);
                        var ordered = fightsByChapter.TryGetValue(id, out var fl) ? fl.OrderBy(x => x.order).Select(x => x.fight).ToList() : new List<FightDefinition>();
                        c.fights = ordered.Where(x => !sideQuests.Contains(x)).ToList();
                        c.sideQuests = ordered.Where(sideQuests.Contains).ToList();
                    });
                }
                foreach (var ch in fightsByChapter.Keys)
                    if (chapters.All(c => c.chapter.id != ch)) throw new FormatException($"fights.csv references unknown chapter '{ch}'");

                // Root config
                var levels = GetOrCreateSingleton<PlayerLevelConfig>(ContentPaths.Config + "/PlayerLevelConfig.asset");
                Track(levels, "PlayerLevelConfig", () =>
                {
                    levels.levels = src["levels"].Rows.Select(r =>
                    {
                        var reward = r.Str("reward");
                        return new PlayerLevelConfig.Level
                        {
                            level = r.Int("level"), xpToNext = r.Int("xpToNext"),
                            reward = string.IsNullOrEmpty(reward) ? null : tables.TryGetValue(reward, out var t) ? t : throw new FormatException($"{r.Context}: unknown reward table '{reward}'"),
                        };
                    }).ToList();
                });

                var gc = GetOrCreateSingleton<GameConfig>(ContentPaths.GameConfigAsset);
                Track(gc, "GameConfig", () =>
                {
                    gc.heroes = heroOrder;
                    gc.equipment = equipment;
                    gc.chapters = chapters.OrderBy(c => c.order).Select(c => c.chapter).ToList();
                    gc.rarityConfig = rarity;
                    gc.starConfig = stars;
                    gc.combatConfig = combat;
                    gc.rankConfig = ranks;
                    gc.levelConfig = levels;
                    gc.extraEnemies = enemies.Values.ToList();
                    gc.extraSkills = skills.Values.ToList();
                    gc.extraDropTables = tables.Values.ToList();
                    gc.partySize = (int)CfgFloat(cfg, "partySize", 5);
                    gc.primaryCurrencyId = Cfg(cfg, "primaryCurrency", "gold");
                    gc.currencies = ParseList(Cfg(cfg, "currencies", "gold:Gold")).Select(c =>
                    {
                        var parts = c.Split(':');
                        var cid = parts[0].Trim();
                        return new CurrencyData { id = cid, nameKey = Text("currency_" + cid + "_name", parts.Length > 1 ? parts[1].Trim() : cid), icon = PlaceholderArt.Get("circle") };
                    }).ToList();
                    gc.startingHeroes = ParseList(Cfg(cfg, "startingHeroes", "")).Select(h => heroes.TryGetValue(h, out var x) ? x : throw new FormatException($"config startingHeroes: unknown hero '{h}'")).ToList();
                    gc.startingCurrencies = ParseList(Cfg(cfg, "startingCurrencies", "")).Select(c =>
                    {
                        var parts = c.Split(':');
                        return new CurrencyAmountData { currencyId = parts[0].Trim(), amount = parts.Length > 1 ? long.Parse(parts[1].Trim(), Inv) : 0 };
                    }).ToList();
                    gc.consumeShardsOnUnlock = CfgBool(cfg, "consumeShardsOnUnlock", true);
                    gc.shardOverflowCurrencyId = Cfg(cfg, "shardOverflowCurrency", "gold");
                    gc.slotLayout = ParseList(Cfg(cfg, "slotLayout", "Weapon;Helmet;Chest;Gloves;Boots;Ring;Ring")).Select(s => ParseEnum<EquipmentSlot>(s, "config slotLayout")).ToList();
                    gc.inventoryLimit = (int)CfgFloat(cfg, "inventoryLimit", 0);
                    gc.equipmentRollVariation = CfgBool(cfg, "equipmentRollVariation", false);
                    gc.rollVariationPercent = CfgFloat(cfg, "rollVariationPercent", 0.1f);
                    gc.sellEnabled = CfgBool(cfg, "sellEnabled", true);
                    gc.starsEnabled = CfgBool(cfg, "starsEnabled", true);
                    gc.defaultStarTurnLimit = (int)CfgFloat(cfg, "defaultStarTurnLimit", 15);
                    var defeat = Cfg(cfg, "defeatConsolation", "");
                    gc.defeatConsolation = string.IsNullOrEmpty(defeat) ? null : tables.TryGetValue(defeat, out var dt) ? dt : throw new FormatException($"config defeatConsolation: unknown table '{defeat}'");
                    gc.currencyDropMultiplier = CfgFloat(cfg, "currencyDropMultiplier", 1);
                    gc.shardDropMultiplier = CfgFloat(cfg, "shardDropMultiplier", 1);
                    gc.pityEnabled = CfgBool(cfg, "pityEnabled", false);
                    gc.pityFightsWithoutShard = (int)CfgFloat(cfg, "pityFightsWithoutShard", 5);
                    gc.showDropOdds = CfgBool(cfg, "showDropOdds", false);
                    gc.dailyBonusEnabled = CfgBool(cfg, "dailyBonusEnabled", false);
                    gc.uniqueHeroAbilities = CfgBool(cfg, "uniqueHeroAbilities", false);
                    var daily = Cfg(cfg, "dailyBonusTable", "");
                    gc.dailyBonusTable = string.IsNullOrEmpty(daily) ? null : tables.TryGetValue(daily, out var dbt) ? dbt : throw new FormatException($"config dailyBonusTable: unknown table '{daily}'");
                });

                ReportOrphans();
                WriteStrings();
            }

            // ---------- asset helpers ----------

            private T GetOrCreate<T>(string folder, string id, string context) where T : ContentDefinition
            {
                if (!Ids.IsValid(id)) throw new FormatException($"{context}: invalid id '{id}' (lowercase snake_case)");
                if (!_existing.TryGetValue(typeof(T), out var map))
                {
                    map = new Dictionary<string, ContentDefinition>();
                    foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { ContentPaths.Root }))
                    {
                        var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                        if (a && !string.IsNullOrEmpty(a.id) && !map.ContainsKey(a.id)) map[a.id] = a;
                    }
                    _existing[typeof(T)] = map;
                    _seen[typeof(T)] = new HashSet<string>();
                }
                if (!_seen[typeof(T)].Add(id)) throw new FormatException($"{context}: duplicate id '{id}'");
                if (map.TryGetValue(id, out var existing)) return (T)existing;

                var asset = ScriptableObject.CreateInstance<T>();
                asset.id = id;
                asset.name = id;
                _r.Created.Add($"{typeof(T).Name} {id}");
                if (!_dry)
                {
                    Directory.CreateDirectory(folder);
                    AssetDatabase.CreateAsset(asset, $"{folder}/{id}.asset");
                }
                map[id] = asset;
                _created.Add(asset);
                return asset;
            }

            private T GetOrCreateSingleton<T>(string path) where T : ScriptableObject
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(path);
                if (a) return a;
                a = ScriptableObject.CreateInstance<T>();
                _r.Created.Add($"{typeof(T).Name} {path}");
                if (!_dry)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    AssetDatabase.CreateAsset(a, path);
                }
                _created.Add(a);
                return a;
            }

            /// <summary>Applies <paramref name="apply"/> and records whether the serialized data changed. Preview mode restores the asset.</summary>
            private void Track(ScriptableObject asset, string label, Action apply)
            {
                var isNew = _created.Contains(asset);
                var before = EditorJsonUtility.ToJson(asset);
                try { apply(); }
                catch
                {
                    if (!isNew) EditorJsonUtility.FromJsonOverwrite(before, asset);
                    throw;
                }
                var after = EditorJsonUtility.ToJson(asset);
                if (!isNew)
                {
                    if (before != after) _r.Updated.Add(label);
                    else _r.Unchanged.Add(label);
                }
                if (_dry)
                {
                    if (!isNew) EditorJsonUtility.FromJsonOverwrite(before, asset);
                }
                else if (isNew || before != after) EditorUtility.SetDirty(asset);
            }

            private void ReportOrphans()
            {
                foreach (var kv in _existing)
                    foreach (var id in kv.Value.Keys)
                        if (!_seen[kv.Key].Contains(id)) _r.Orphans.Add($"{kv.Key.Name} {id}");
            }

            private string Text(string key, string english)
            {
                if (string.IsNullOrEmpty(english)) return key;
                _strings[key] = english;
                return key;
            }

            private void WriteStrings()
            {
                if (_dry) return;
                Directory.CreateDirectory(ContentPaths.Localization);
                var csv = Csv.Write(new[] { "key", "en" }, _strings.OrderBy(k => k.Key, StringComparer.Ordinal).Select(kv => new[] { kv.Key, kv.Value }));
                var path = ContentPaths.ContentStrings;
                if (!File.Exists(path) || File.ReadAllText(path) != csv) File.WriteAllText(path, csv);
            }

            // ---------- parsing ----------

            private static string Cfg(Dictionary<string, string> cfg, string key, string fallback) => cfg.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;
            private static float CfgFloat(Dictionary<string, string> cfg, string key, float fallback) => cfg.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? CsvRow.ParseFloat(v, "config " + key) : fallback;
            private static bool CfgBool(Dictionary<string, string> cfg, string key, bool fallback) => cfg.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v.Trim().ToLowerInvariant() == "true" : fallback;

            private static T ParseEnum<T>(string v, string ctx) where T : struct =>
                Enum.TryParse<T>(v.Trim(), true, out var r) && Enum.IsDefined(typeof(T), r) ? r : throw new FormatException($"{ctx}: '{v}' is not one of {string.Join("/", Enum.GetNames(typeof(T)))}");

            private static List<string> ParseList(string v) => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

            private static Color ParseColor(string v, string ctx) => ColorUtility.TryParseHtmlString(v.StartsWith("#") ? v : "#" + v, out var c) ? c : throw new FormatException($"{ctx}: bad color '{v}'");

            private static StatBlock ReadStats(CsvRow row)
            {
                var b = new StatBlock();
                foreach (var s in StatBlock.All)
                {
                    var name = s.ToString();
                    b[s] = row.Float(char.ToLowerInvariant(name[0]) + name.Substring(1));
                }
                return b;
            }

            /// <summary>"Attack=1;MaxHp=0.1" → StatBlock.</summary>
            private static StatBlock ParseStatBlock(string v, string ctx)
            {
                var b = new StatBlock();
                foreach (var part in ParseList(v))
                {
                    var kv = part.Split('=');
                    if (kv.Length != 2) throw new FormatException($"{ctx}: bad stat '{part}' (expected Stat=value)");
                    b[ParseEnum<StatType>(kv[0], ctx)] = CsvRow.ParseFloat(kv[1].Trim(), ctx);
                }
                return b;
            }

            /// <summary>"Attack=12;MaxHp=5%" → modifiers (a % suffix makes a percent modifier: 5% → 0.05).</summary>
            private static List<StatModifierData> ParseModifiers(string v, string ctx)
            {
                var list = new List<StatModifierData>();
                foreach (var part in ParseList(v))
                {
                    var kv = part.Split('=');
                    if (kv.Length != 2) throw new FormatException($"{ctx}: bad stat '{part}' (expected Stat=value or Stat=value%)");
                    var raw = kv[1].Trim();
                    var pct = raw.EndsWith("%");
                    var val = CsvRow.ParseFloat(pct ? raw.TrimEnd('%') : raw, ctx);
                    list.Add(new StatModifierData { stat = ParseEnum<StatType>(kv[0], ctx), value = pct ? val / 100f : val, kind = pct ? ModifierKind.Percent : ModifierKind.Flat });
                }
                return list;
            }

            /// <summary>Effect mini-syntax, parsed by the shared Core parser (<see cref="EffectSpec"/>).</summary>
            private static List<EffectData> ParseEffects(string v, string ctx)
            {
                try { return EffectSpec.Parse(v).Select(EffectData.FromCore).ToList(); }
                catch (FormatException ex) { throw new FormatException($"{ctx}: {ex.Message}"); }
            }

            /// <summary>Waves separated by '/', spawns by '+': "goblin@1.0+wolf / goblin_king@1.5".</summary>
            private static List<WaveData> ParseWaves(string v, Dictionary<string, EnemyDefinition> enemies, string ctx)
            {
                var waves = new List<WaveData>();
                foreach (var w in v.Split('/').Select(s => s.Trim()).Where(s => s.Length > 0))
                {
                    var wave = new WaveData();
                    foreach (var sp in w.Split('+').Select(s => s.Trim()).Where(s => s.Length > 0))
                    {
                        var parts = sp.Split('@');
                        var id = parts[0].Trim();
                        if (!enemies.TryGetValue(id, out var e)) throw new FormatException($"{ctx}: unknown enemy '{id}'");
                        wave.spawns.Add(new SpawnData { enemy = e, statMultiplier = parts.Length > 1 ? CsvRow.ParseFloat(parts[1].Trim(), ctx) : 1f });
                    }
                    waves.Add(wave);
                }
                return waves;
            }

            /// <summary>"ClearFight:fight_x;MinPartyPower:500;HeroOwned:hero_y".</summary>
            private static List<UnlockConditionData> ParseConditions(string v, string ctx) =>
                ParseList(v).Select(c =>
                {
                    var p = c.Split(':');
                    if (p.Length != 2) throw new FormatException($"{ctx}: bad unlock condition '{c}'");
                    var type = ParseEnum<UnlockConditionType>(p[0], ctx);
                    return type == UnlockConditionType.MinPartyPower
                        ? new UnlockConditionData { type = type, value = int.Parse(p[1].Trim(), Inv) }
                        : new UnlockConditionData { type = type, refId = p[1].Trim() };
                }).ToList();

            private static DropEntryData ParseDropEntry(CsvRow row)
            {
                var e = new DropEntryData
                {
                    type = row.Enum<DropType>("type"),
                    refId = row.Str("refId"),
                    rarity = row.Enum("rarity", Rarity.Common),
                    weight = row.Int("weight", 1),
                    amountMin = row.Int("amountMin", 1),
                };
                e.amountMax = row.Int("amountMax", e.amountMin);
                var slot = row.Str("slot");
                e.anySlot = string.IsNullOrEmpty(slot);
                if (!e.anySlot) e.slot = ParseEnum<EquipmentSlot>(slot, row.Context);
                foreach (var part in ParseList(row.Str("rarityWeights")))
                {
                    var kv = part.Split('=');
                    if (kv.Length != 2) throw new FormatException($"{row.Context}: bad rarity weight '{part}'");
                    e.rarityWeights.Add(new RarityWeightData { rarity = ParseEnum<Rarity>(kv[0], row.Context), weight = int.Parse(kv[1].Trim(), Inv) });
                }
                return e;
            }
        }
    }
}
