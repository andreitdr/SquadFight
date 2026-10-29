using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Services
{
    /// <summary>
    /// Builds a <see cref="GameDatabase"/> straight from the content CSVs (same schema as the editor importer), so balance
    /// tools and simulators can run outside Unity. A Unity test asserts it matches the asset pipeline exactly.
    /// </summary>
    public static class ContentCsvLoader
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Table
        {
            public readonly List<Dictionary<string, string>> Rows = new List<Dictionary<string, string>>();
        }

        private static Table Read(string folder, string name)
        {
            var t = new Table();
            var rows = TableLocalizer.ParseCsv(File.ReadAllText(Path.Combine(folder, name + ".csv")).Replace("\r", ""))
                .Where(r => r.Length > 0 && !(r.Length == 1 && r[0].Trim() == "") && !r[0].StartsWith("#")).ToList();
            if (rows.Count == 0) return t;
            var header = rows[0].Select(h => h.Trim()).ToArray();
            foreach (var r in rows.Skip(1))
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < header.Length; i++) d[header[i]] = i < r.Length ? r[i].Trim() : "";
                t.Rows.Add(d);
            }
            return t;
        }

        private static string S(Dictionary<string, string> r, string k) => r.TryGetValue(k, out var v) ? v : "";
        private static int I(Dictionary<string, string> r, string k, int fb = 0) => string.IsNullOrEmpty(S(r, k)) ? fb : int.Parse(S(r, k), Inv);
        private static float F(Dictionary<string, string> r, string k, float fb = 0) => string.IsNullOrEmpty(S(r, k)) ? fb : float.Parse(S(r, k), NumberStyles.Float, Inv);
        private static bool B(Dictionary<string, string> r, string k) => S(r, k).ToLowerInvariant() == "true" || S(r, k) == "1";
        private static T E<T>(string v, T fb = default) where T : struct => string.IsNullOrEmpty(v) ? fb : (T)Enum.Parse(typeof(T), v.Trim(), true);
        private static List<string> L(string v) => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split(';').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

        public static GameDatabase Load(string folder)
        {
            var db = new GameDatabase();
            var cfg = Read(folder, "config").Rows.ToDictionary(r => S(r, "key"), r => S(r, "value"));
            string C(string k, string fb) => cfg.TryGetValue(k, out var v) && v.Length > 0 ? v : fb;
            float CF(string k, float fb) => cfg.TryGetValue(k, out var v) && v.Length > 0 ? float.Parse(v, Inv) : fb;
            bool CB(string k, bool fb) => cfg.TryGetValue(k, out var v) && v.Length > 0 ? v.Trim().ToLowerInvariant() == "true" : fb;

            db.Rarities = Read(folder, "rarities").Rows.Select(r => new RarityDef
            {
                Rarity = E<Rarity>(S(r, "rarity")), ColorHex = S(r, "color").ToUpperInvariant(), EquipmentStatMultiplier = F(r, "equipmentStatMultiplier", 1), ShardsToUnlock = I(r, "shardsToUnlock"),
                StarCostMultiplier = F(r, "starCostMultiplier", 1), DropWeightModifier = F(r, "dropWeightModifier", 1), ItemSellValue = I(r, "itemSellValue"), OverflowCurrencyPerShard = I(r, "overflowCurrencyPerShard"),
            }).ToList();
            db.StarLevels = Read(folder, "stars").Rows.Select(r => new StarLevelDef { Star = I(r, "star"), ShardsRequired = I(r, "shardsRequired"), CurrencyCost = I(r, "currencyCost"), StatMultiplier = F(r, "statMultiplier", 1) }).ToList();
            db.Combat = new CombatRules
            {
                MinDamage = CF("combat.minDamage", 1), DefenseK = CF("combat.defenseK", 1), MaxRounds = (int)CF("combat.maxRounds", 100), CritChanceCap = CF("combat.critChanceCap", 1),
                AiTargeting = E<AiTargeting>(C("combat.aiTargeting", "LowestHp")), PowerWeights = StatBlockOf(C("combat.powerWeights", "")),
            };

            db.Skills = Read(folder, "skills").Rows.Select(r => new SkillDef
            {
                Id = S(r, "id"), NameKey = S(r, "id") + "_name", DescriptionKey = S(r, "description").Length > 0 ? S(r, "id") + "_desc" : S(r, "id") + "_desc",
                Targeting = E<TargetingRule>(S(r, "targeting")), TargetCount = Math.Max(1, I(r, "targetCount", 1)), Cooldown = I(r, "cooldown"), InitialCooldown = I(r, "initialCooldown"),
                Effects = Effects(S(r, "effects")),
            }).ToList();
            StatBlock Stats(Dictionary<string, string> r)
            {
                var b = new StatBlock();
                foreach (var s in StatBlock.All) b[s] = F(r, char.ToLowerInvariant(s.ToString()[0]) + s.ToString().Substring(1));
                return b;
            }
            db.Heroes = Read(folder, "heroes").Rows.Select(r => new HeroDef
            {
                Id = S(r, "id"), NameKey = S(r, "id") + "_name", LoreKey = S(r, "id") + "_lore", Rarity = E<Rarity>(S(r, "rarity")), Class = E<HeroClass>(S(r, "class")),
                BaseStats = Stats(r), BasicSkillId = S(r, "basicSkill"), ActiveSkillId = S(r, "activeSkill").Length > 0 ? S(r, "activeSkill") : null,
            }).ToList();
            db.Enemies = Read(folder, "enemies").Rows.Select(r => new EnemyDef
            {
                Id = S(r, "id"), NameKey = S(r, "id") + "_name", IsBoss = B(r, "isBoss"), BaseStats = Stats(r), BasicSkillId = S(r, "basicSkill"), ActiveSkillId = S(r, "activeSkill").Length > 0 ? S(r, "activeSkill") : null,
            }).ToList();
            db.Equipment = Read(folder, "equipment").Rows.Select(r => new EquipmentTemplateDef
            {
                Id = S(r, "id"), NameKey = S(r, "id") + "_name", Slot = E<EquipmentSlot>(S(r, "slot")), Rarity = E<Rarity>(S(r, "rarity")), Stats = Modifiers(S(r, "stats")), SetId = S(r, "setId"),
            }).ToList();
            db.StoryBeats = Read(folder, "story").Rows.Select(r =>
            {
                var speaker = S(r, "speaker");
                var isUnit = db.Heroes.Any(h => h.Id == speaker) || db.Enemies.Any(e => e.Id == speaker);
                return new StoryBeatDef { Id = S(r, "id"), SpeakerKey = isUnit ? speaker + "_name" : "speaker_" + speaker, TextKey = S(r, "id") + "_text" };
            }).ToList();

            foreach (var grp in Read(folder, "droptables").Rows.GroupBy(r => S(r, "table")))
            {
                var t = new DropTableDef { Id = grp.Key };
                var groups = new Dictionary<string, RollGroupDef>();
                foreach (var r in grp)
                {
                    var e = new DropEntryDef
                    {
                        Type = E<DropType>(S(r, "type")), RefId = S(r, "refId"), Rarity = E(S(r, "rarity"), Rarity.Common), Weight = I(r, "weight", 1), AmountMin = I(r, "amountMin", 1),
                        SlotFilter = S(r, "slot").Length == 0 ? (EquipmentSlot?)null : E<EquipmentSlot>(S(r, "slot")),
                    };
                    e.AmountMax = I(r, "amountMax", e.AmountMin);
                    foreach (var part in L(S(r, "rarityWeights")))
                    {
                        var kv = part.Split('=');
                        e.RarityWeights.Add(new RarityWeight(E<Rarity>(kv[0]), int.Parse(kv[1].Trim(), Inv)));
                    }
                    var g = S(r, "group");
                    if (g.Equals("G", StringComparison.OrdinalIgnoreCase)) { t.Guaranteed.Add(e); continue; }
                    if (!groups.TryGetValue(g, out var rg))
                    {
                        var min = I(r, "minRolls", 1);
                        rg = new RollGroupDef { MinRolls = min, MaxRolls = I(r, "maxRolls", min), WithoutReplacement = B(r, "withoutReplacement") };
                        groups[g] = rg;
                        t.Groups.Add(rg);
                    }
                    rg.Entries.Add(e);
                }
                db.DropTables.Add(t);
            }

            var fights = Read(folder, "fights").Rows;
            db.Fights = fights.Select(r => new FightDef
            {
                Id = S(r, "id"), NameKey = S(r, "id") + "_name", RecommendedPower = I(r, "recommendedPower"), DropTableId = Null(S(r, "dropTable")), FirstClearTableId = Null(S(r, "firstClearTable")),
                StarTurnLimit = I(r, "starTurnLimit"), BackgroundKey = "background", PreBeatIds = L(S(r, "preBeats")), PostBeatIds = L(S(r, "postBeats")), UnlockConditions = Conditions(S(r, "unlockConditions")),
                Waves = S(r, "waves").Split('/').Select(w => w.Trim()).Where(w => w.Length > 0).Select(w => new WaveDef
                {
                    Spawns = w.Split('+').Select(x => x.Trim()).Where(x => x.Length > 0).Select(x =>
                    {
                        var p = x.Split('@');
                        return new EnemySpawnDef { EnemyId = p[0].Trim(), StatMultiplier = p.Length > 1 ? float.Parse(p[1].Trim(), Inv) : 1f };
                    }).ToList(),
                }).ToList(),
            }).ToList();
            db.Chapters = Read(folder, "chapters").Rows.OrderBy(r => I(r, "order")).Select(r => new ChapterDef
            {
                Id = S(r, "id"), NameKey = S(r, "id") + "_name", ClearRewardTableId = Null(S(r, "clearReward")), UnlockConditions = Conditions(S(r, "unlockConditions")),
                FightIds = fights.Where(f => S(f, "chapter") == S(r, "id")).OrderBy(f => I(f, "order")).Select(f => S(f, "id")).ToList(),
            }).ToList();
            var fightOrder = db.Chapters.SelectMany(c => c.FightIds).ToList();
            db.Fights = db.Fights.OrderBy(f => fightOrder.IndexOf(f.Id)).ToList();

            db.Rules = new GameRules
            {
                PartySize = (int)CF("partySize", 5),
                PrimaryCurrencyId = C("primaryCurrency", "gold"),
                Currencies = L(C("currencies", "gold:Gold")).Select(c => new CurrencyDef { Id = c.Split(':')[0].Trim(), NameKey = "currency_" + c.Split(':')[0].Trim() + "_name" }).ToList(),
                StartingHeroIds = L(C("startingHeroes", "")),
                StartingCurrencies = L(C("startingCurrencies", "")).Select(c => new CurrencyAmount(c.Split(':')[0].Trim(), c.Contains(":") ? long.Parse(c.Split(':')[1].Trim(), Inv) : 0)).ToList(),
                ConsumeShardsOnUnlock = CB("consumeShardsOnUnlock", true),
                ShardOverflowCurrencyId = C("shardOverflowCurrency", "gold"),
                SlotLayout = L(C("slotLayout", "Weapon;Helmet;Chest;Gloves;Boots;Ring;Ring")).Select(x => E<EquipmentSlot>(x)).ToList(),
                InventoryLimit = (int)CF("inventoryLimit", 0),
                EquipmentRollVariation = CB("equipmentRollVariation", false),
                RollVariationPercent = CF("rollVariationPercent", 0.1f),
                SellEnabled = CB("sellEnabled", true),
                StarsEnabled = CB("starsEnabled", true),
                DefaultStarTurnLimit = (int)CF("defaultStarTurnLimit", 15),
                DefeatConsolationTableId = Null(C("defeatConsolation", "")),
                CurrencyDropMultiplier = CF("currencyDropMultiplier", 1),
                ShardDropMultiplier = CF("shardDropMultiplier", 1),
                PityEnabled = CB("pityEnabled", false),
                PityFightsWithoutShard = (int)CF("pityFightsWithoutShard", 5),
                ShowDropOdds = CB("showDropOdds", false),
                DailyBonusEnabled = CB("dailyBonusEnabled", false),
                DailyBonusTableId = Null(C("dailyBonusTable", "")),
            };
            return db.Index();
        }

        private static string Null(string v) => string.IsNullOrEmpty(v) ? null : v;

        private static StatBlock StatBlockOf(string v)
        {
            var b = new StatBlock();
            foreach (var part in L(v)) { var kv = part.Split('='); b[E<StatType>(kv[0])] = float.Parse(kv[1].Trim(), Inv); }
            return b;
        }

        private static List<StatModifier> Modifiers(string v) => L(v).Select(part =>
        {
            var kv = part.Split('=');
            var raw = kv[1].Trim();
            var pct = raw.EndsWith("%");
            var val = float.Parse(pct ? raw.TrimEnd('%') : raw, Inv);
            return new StatModifier(E<StatType>(kv[0]), pct ? val / 100f : val, pct ? ModifierKind.Percent : ModifierKind.Flat);
        }).ToList();

        private static List<UnlockConditionDef> Conditions(string v) => L(v).Select(c =>
        {
            var p = c.Split(':');
            var type = E<UnlockConditionType>(p[0]);
            return type == UnlockConditionType.MinPartyPower ? new UnlockConditionDef { Type = type, Value = int.Parse(p[1].Trim(), Inv) } : new UnlockConditionDef { Type = type, RefId = p[1].Trim() };
        }).ToList();

        private static List<EffectDef> Effects(string v)
        {
            var list = new List<EffectDef>();
            foreach (var raw in v.Split('|').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var spec = raw;
                var e = new EffectDef { Target = EffectTarget.SkillTargets, MaxStacks = 1 };
                if (spec.StartsWith("self>", StringComparison.OrdinalIgnoreCase)) { e.Target = EffectTarget.Self; spec = spec.Substring(5); }
                var p = spec.Split(':').Select(s => s.Trim()).ToArray();
                float Fp(int i, float fb = 0) => i < p.Length && p[i].Length > 0 ? float.Parse(p[i], Inv) : fb;
                switch (p[0].ToLowerInvariant())
                {
                    case "damage": e.Type = EffectType.Damage; e.Power = Fp(1, 1); break;
                    case "heal": e.Type = EffectType.Heal; e.Power = Fp(1, 1); break;
                    case "shield": e.Type = EffectType.Shield; e.Power = Fp(1, 1); e.Duration = (int)Fp(2, 2); break;
                    case "stun": e.Type = EffectType.Stun; e.Power = 0; e.Duration = (int)Fp(1, 1); e.Chance = Fp(2); break;
                    case "status":
                        e.Type = EffectType.ApplyStatus; e.Power = 0; e.Stat = E<StatType>(p[1]); e.Value = Fp(2); e.Duration = (int)Fp(3, 1);
                        e.Stacking = p.Length > 4 && p[4].Length > 0 ? E<StackingRule>(p[4]) : StackingRule.Refresh; e.MaxStacks = (int)Fp(5, 1); e.Chance = Fp(6);
                        break;
                    default: throw new FormatException("unknown effect " + raw);
                }
                list.Add(e);
            }
            return list;
        }
    }
}
