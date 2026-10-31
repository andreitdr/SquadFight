using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.Core
{
    public enum IssueSeverity { Warning, Error }

    public readonly struct ValidationIssue
    {
        public readonly IssueSeverity Severity;
        public readonly string Context;
        public readonly string Message;

        public ValidationIssue(IssueSeverity severity, string context, string message)
        {
            Severity = severity;
            Context = context;
            Message = message;
        }

        public override string ToString() => $"[{Severity}] {Context}: {Message}";
    }

    public sealed class ValidationReport
    {
        public readonly List<ValidationIssue> Issues = new List<ValidationIssue>();
        public IEnumerable<ValidationIssue> Errors => Issues.Where(i => i.Severity == IssueSeverity.Error);
        public IEnumerable<ValidationIssue> Warnings => Issues.Where(i => i.Severity == IssueSeverity.Warning);
        public bool HasErrors => Issues.Any(i => i.Severity == IssueSeverity.Error);

        public void Error(string context, string message) => Issues.Add(new ValidationIssue(IssueSeverity.Error, context, message));
        public void Warn(string context, string message) => Issues.Add(new ValidationIssue(IssueSeverity.Warning, context, message));

        public bool Contains(string fragment) => Issues.Any(i => i.Message.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0 || i.Context.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Config validation: {Errors.Count()} error(s), {Warnings.Count()} warning(s)");
            foreach (var i in Issues.OrderByDescending(i => i.Severity)) sb.AppendLine(i.ToString());
            return sb.ToString();
        }
    }

    /// <summary>Content rules checked before every build (and on demand). Pure: works on a <see cref="GameDatabase"/>.</summary>
    public static class ConfigValidation
    {
        public static ValidationReport Validate(GameDatabase db)
        {
            var r = new ValidationReport();
            db.Index();
            foreach (var d in db.DuplicateIds) r.Error(d, "duplicate id");

            CheckIds(r, "hero", db.Heroes.Select(h => h?.Id));
            CheckIds(r, "enemy", db.Enemies.Select(h => h?.Id));
            CheckIds(r, "skill", db.Skills.Select(h => h?.Id));
            CheckIds(r, "equipment", db.Equipment.Select(h => h?.Id));
            CheckIds(r, "droptable", db.DropTables.Select(h => h?.Id));
            CheckIds(r, "chapter", db.Chapters.Select(h => h?.Id));
            CheckIds(r, "fight", db.Fights.Select(h => h?.Id));
            CheckIds(r, "storybeat", db.StoryBeats.Select(h => h?.Id));
            CheckIds(r, "currency", db.Rules.Currencies.Select(c => c?.Id));

            ValidateRarities(db, r);
            ValidateStars(db, r);
            ValidateCombat(db, r);
            ValidateRules(db, r);
            foreach (var s in db.Skills.Where(s => s != null)) ValidateSkill(s, r);
            foreach (var h in db.Heroes.Where(h => h != null)) ValidateUnit($"hero:{h.Id}", h.BaseStats, h.BasicSkillId, h.ActiveSkillId, h.PassiveSkillId, h.NameKey, db, r);
            foreach (var e in db.Enemies.Where(e => e != null)) ValidateUnit($"enemy:{e.Id}", e.BaseStats, e.BasicSkillId, e.ActiveSkillId, e.PassiveSkillId, e.NameKey, db, r);
            if (db.Rules.UniqueHeroAbilities) ValidateHeroUniqueness(db, r);
            foreach (var e in db.Equipment.Where(e => e != null)) ValidateEquipment(e, db, r);
            foreach (var t in db.DropTables.Where(t => t != null)) ValidateDropTable(t, db, r);
            ValidateCampaign(db, r);
            ValidateOrphans(db, r);
            return r;
        }

        private static void CheckIds(ValidationReport r, string type, IEnumerable<string> ids)
        {
            var i = 0;
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) r.Error($"{type}[{i}]", "empty id (or missing entry)");
                else if (!Ids.IsValid(id)) r.Error($"{type}:{id}", "id must be lowercase snake_case (a-z, 0-9, _), start with a letter, max 64 chars");
                i++;
            }
        }

        private static void ValidateRarities(GameDatabase db, ValidationReport r)
        {
            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
            {
                if (!db.TryGetRarity(rarity, out var def)) { r.Error("rarity:" + rarity, "missing rarity config"); continue; }
                var ctx = "rarity:" + rarity;
                if (def.ShardsToUnlock <= 0) r.Error(ctx, "shardsToUnlock must be > 0");
                if (def.EquipmentStatMultiplier <= 0) r.Error(ctx, "equipment stat multiplier must be > 0");
                if (def.StarCostMultiplier <= 0) r.Error(ctx, "star cost multiplier must be > 0");
                if (def.DropWeightModifier < 0) r.Error(ctx, "drop weight modifier must be >= 0");
                if (def.ItemSellValue < 0 || def.OverflowCurrencyPerShard < 0) r.Error(ctx, "sell/overflow values must be >= 0");
            }
            var ordered = db.Rarities.Where(x => x != null).OrderBy(x => x.Rarity).ToList();
            for (var i = 1; i < ordered.Count; i++)
                if (ordered[i].EquipmentStatMultiplier < ordered[i - 1].EquipmentStatMultiplier)
                    r.Warn("rarity:" + ordered[i].Rarity, "equipment multiplier lower than the previous rarity");
        }

        private static void ValidateStars(GameDatabase db, ValidationReport r)
        {
            if (db.StarLevels.Count == 0) { r.Error("stars", "star table is empty"); return; }
            for (var i = 0; i < db.StarLevels.Count; i++)
            {
                var s = db.StarLevels[i];
                var ctx = "star:" + s.Star;
                if (s.Star != i + 1) r.Error(ctx, $"star table has a gap or duplicate (expected star {i + 1})");
                if (s.StatMultiplier <= 0) r.Error(ctx, "stat multiplier must be > 0");
                if (i == 0 && s.ShardsRequired != 0) r.Warn(ctx, "star 1 shard cost is ignored (heroes start at star 1)");
                if (i > 0 && s.ShardsRequired <= 0) r.Error(ctx, "shardsRequired must be > 0");
                if (s.CurrencyCost < 0) r.Error(ctx, "currency cost must be >= 0");
                if (i > 0 && s.StatMultiplier < db.StarLevels[i - 1].StatMultiplier) r.Error(ctx, "stat multiplier must not decrease");
            }
        }

        private static void ValidateCombat(GameDatabase db, ValidationReport r)
        {
            var c = db.Combat;
            if (c == null) { r.Error("combat", "missing combat config"); return; }
            if (c.DefenseK <= 0) r.Error("combat", "defenseK must be > 0");
            if (c.MinDamage < 0) r.Error("combat", "minDamage must be >= 0");
            if (c.MaxRounds < 1) r.Error("combat", "maxRounds must be >= 1");
            if (c.CritChanceCap <= 0 || c.CritChanceCap > 1) r.Error("combat", "critChanceCap must be in (0, 1]");
            if (c.PowerWeights == null || StatBlock.All.All(s => c.PowerWeights[s] == 0)) r.Error("combat", "power weights are all zero");
        }

        private static void ValidateRules(GameDatabase db, ValidationReport r)
        {
            var g = db.Rules;
            if (g.PartySize < 1 || g.PartySize > 5) r.Error("rules", "party size must be 1..5");
            if (g.SlotLayout.Count == 0) r.Error("rules", "slot layout is empty");
            if (g.StartingHeroIds.Count == 0) r.Error("rules", "no starting heroes (the party would be empty)");
            foreach (var id in g.StartingHeroIds)
                if (!db.TryGetHero(id, out _)) r.Error("rules", $"starting hero '{id}' does not exist");
            if (g.Currencies.Count == 0) r.Error("rules", "no currencies defined");
            if (!db.IsCurrency(g.PrimaryCurrencyId)) r.Error("rules", $"primary currency '{g.PrimaryCurrencyId}' is not defined");
            if (!db.IsCurrency(g.ShardOverflowCurrencyId)) r.Error("rules", $"overflow currency '{g.ShardOverflowCurrencyId}' is not defined");
            foreach (var c in g.StartingCurrencies)
            {
                if (!db.IsCurrency(c.CurrencyId)) r.Error("rules", $"starting currency '{c.CurrencyId}' is not defined");
                if (c.Amount < 0) r.Error("rules", $"starting currency '{c.CurrencyId}' is negative");
            }
            if (g.InventoryLimit < 0) r.Error("rules", "inventory limit must be >= 0 (0 = unlimited)");
            if (g.EquipmentRollVariation && (g.RollVariationPercent <= 0 || g.RollVariationPercent >= 1)) r.Error("rules", "roll variation percent must be in (0, 1)");
            if (g.DefaultStarTurnLimit < 1) r.Error("rules", "default star turn limit must be >= 1");
            if (!string.IsNullOrEmpty(g.DefeatConsolationTableId) && !db.TryGetDropTable(g.DefeatConsolationTableId, out _))
                r.Error("rules", $"defeat consolation table '{g.DefeatConsolationTableId}' does not exist");
            if (g.CurrencyDropMultiplier < 0 || g.ShardDropMultiplier < 0) r.Error("rules", "drop multipliers must be >= 0");
            if (g.PityEnabled && g.PityFightsWithoutShard < 1) r.Error("rules", "pity threshold must be >= 1");
            if (g.DailyBonusEnabled && !db.TryGetDropTable(g.DailyBonusTableId, out _)) r.Error("rules", "daily bonus is enabled but its table is missing");
        }

        private static void ValidateUnit(string ctx, StatBlock stats, string basic, string active, string passive, string nameKey, GameDatabase db, ValidationReport r)
        {
            if (string.IsNullOrEmpty(nameKey)) r.Error(ctx, "missing name key");
            if (stats == null) { r.Error(ctx, "missing base stats"); return; }
            if (stats[StatType.MaxHp] <= 0) r.Error(ctx, "MaxHp must be > 0");
            if (stats[StatType.Attack] <= 0) r.Error(ctx, "Attack must be > 0");
            if (stats[StatType.Speed] <= 0) r.Error(ctx, "Speed must be > 0");
            if (stats[StatType.Defense] < 0) r.Error(ctx, "Defense must be >= 0");
            if (stats[StatType.CritChance] < 0 || stats[StatType.CritChance] > 1) r.Error(ctx, "CritChance must be in [0, 1]");
            if (stats[StatType.CritDamage] < 1) r.Error(ctx, "CritDamage must be >= 1");
            if (string.IsNullOrEmpty(basic) || !db.TryGetSkill(basic, out _)) r.Error(ctx, $"missing basic skill '{basic}'");
            if (!string.IsNullOrEmpty(active) && !db.TryGetSkill(active, out _)) r.Error(ctx, $"missing active skill '{active}'");
            if (db.TryGetSkill(basic, out var b) && b.IsPassive) r.Error(ctx, $"basic skill '{basic}' is a passive");
            if (db.TryGetSkill(active, out var a) && a.IsPassive) r.Error(ctx, $"active skill '{active}' is a passive");
            if (!string.IsNullOrEmpty(passive))
            {
                if (!db.TryGetSkill(passive, out var p)) r.Error(ctx, $"missing passive skill '{passive}'");
                else if (!p.IsPassive) r.Error(ctx, $"passive '{passive}' has no trigger");
            }
        }

        /// <summary>Every hero must feel different: no two heroes share an active or a passive (by behaviour, not by name).</summary>
        private static void ValidateHeroUniqueness(GameDatabase db, ValidationReport r)
        {
            void Check(Func<HeroDef, string> skillId, string what)
            {
                var seen = new Dictionary<string, string>();
                foreach (var h in db.Heroes.Where(h => h != null))
                {
                    var id = skillId(h);
                    if (string.IsNullOrEmpty(id)) { if (what == "passive") r.Error("hero:" + h.Id, "hero has no passive"); continue; }
                    if (!db.TryGetSkill(id, out var s)) continue;
                    var sig = EffectSpec.Signature(s);
                    if (seen.TryGetValue(sig, out var other)) r.Error("hero:" + h.Id, $"{what} '{id}' behaves exactly like the {what} of '{other}' (abilities must be unique)");
                    else seen[sig] = h.Id;
                }
            }
            Check(h => h.ActiveSkillId, "active");
            Check(h => h.PassiveSkillId, "passive");
        }

        private static void ValidateSkill(SkillDef s, ValidationReport r)
        {
            var ctx = "skill:" + s.Id;
            if (string.IsNullOrEmpty(s.NameKey)) r.Error(ctx, "missing name key");
            if (s.Effects.Count == 0) r.Error(ctx, "skill has no effects");
            if (s.Cooldown < 0 || s.InitialCooldown < 0) r.Error(ctx, "cooldowns must be >= 0");
            if ((s.Targeting == TargetingRule.RandomEnemies || s.Targeting == TargetingRule.RandomAllies) && s.TargetCount < 1) r.Error(ctx, $"{s.Targeting} needs targetCount >= 1");
            if (s.IsPassive)
            {
                if (s.Trigger == PassiveTrigger.OnLowHp && (s.Threshold <= 0 || s.Threshold >= 1)) r.Error(ctx, "OnLowHp passive needs a threshold in (0, 1)");
                if (s.Chance < 0 || s.Chance > 1) r.Error(ctx, "passive chance must be in [0, 1]");
            }
            for (var i = 0; i < s.Effects.Count; i++)
            {
                var e = s.Effects[i];
                var ectx = $"{ctx}#effect{i}";
                if (e == null) { r.Error(ectx, "null effect"); continue; }
                switch (e.Type)
                {
                    case EffectType.Damage:
                    case EffectType.Heal:
                    case EffectType.Shield:
                    case EffectType.Drain:
                    case EffectType.Execute:
                    case EffectType.DamageOverTime:
                    case EffectType.HealOverTime:
                        if (e.Power <= 0) r.Error(ectx, $"{e.Type} power must be > 0");
                        break;
                    case EffectType.ApplyStatus:
                        if (e.Value == 0) r.Error(ectx, "status value must not be 0");
                        if (e.Value <= -1) r.Error(ectx, "status value must be > -1 (a stat cannot drop below 0%)");
                        break;
                }
                switch (e.Type)
                {
                    case EffectType.Drain:
                        if (e.Value <= 0 || e.Value > 2) r.Error(ectx, "drain ratio must be in (0, 2]");
                        break;
                    case EffectType.Execute:
                        if (e.Value <= 0 || e.Value >= 1) r.Error(ectx, "execute threshold must be in (0, 1)");
                        if (e.Value2 <= 1) r.Error(ectx, "execute multiplier must be > 1");
                        break;
                    case EffectType.PercentHpDamage:
                        if (e.Value <= 0 || e.Value > 1) r.Error(ectx, "percent HP damage must be in (0, 1]");
                        if (e.Value2 <= 0) r.Error(ectx, "percent HP damage cap must be > 0");
                        break;
                    case EffectType.Revive:
                        if (e.Value <= 0 || e.Value > 1) r.Error(ectx, "revive HP ratio must be in (0, 1]");
                        break;
                    case EffectType.Thorns:
                    case EffectType.Mark:
                        if (e.Value <= 0 || e.Value > 2) r.Error(ectx, $"{e.Type} value must be in (0, 2]");
                        break;
                    case EffectType.Dodge:
                        if (e.Value <= 0 || e.Value >= 1) r.Error(ectx, "dodge chance must be in (0, 1)");
                        break;
                    case EffectType.CooldownShift:
                        if (e.Value == 0) r.Error(ectx, "cooldown shift must not be 0");
                        break;
                    case EffectType.Cleanse:
                    case EffectType.Dispel:
                        if (e.Value < 0) r.Error(ectx, "count must be >= 0 (0 = all)");
                        break;
                }
                if (EffectSpec.StatusKindOf(e.Type) != null || e.Type == EffectType.Stun || e.Type == EffectType.Shield)
                    if (e.Duration < 1) r.Error(ectx, "duration must be >= 1");
                if (e.Stacking == StackingRule.Stack && e.MaxStacks < 1) r.Error(ectx, "maxStacks must be >= 1");
                if (e.Chance < 0 || e.Chance > 1) r.Error(ectx, "chance must be in [0, 1]");
            }
        }

        private static void ValidateEquipment(EquipmentTemplateDef e, GameDatabase db, ValidationReport r)
        {
            var ctx = "equipment:" + e.Id;
            if (string.IsNullOrEmpty(e.NameKey)) r.Error(ctx, "missing name key");
            if (e.Stats.Count == 0) r.Error(ctx, "item has no stats");
            if (!db.Rules.SlotLayout.Contains(e.Slot)) r.Error(ctx, $"slot {e.Slot} is not in the slot layout");
            foreach (var m in e.Stats)
                if (m.Value <= 0) r.Warn(ctx, $"non-positive stat {m}");
        }

        private static void ValidateDropTable(DropTableDef t, GameDatabase db, ValidationReport r)
        {
            var ctx = "droptable:" + t.Id;
            if (t.Guaranteed.Count == 0 && t.Groups.Count == 0) r.Error(ctx, "table gives nothing");
            for (var i = 0; i < t.Guaranteed.Count; i++) ValidateEntry(t.Guaranteed[i], $"{ctx}#guaranteed{i}", db, r, guaranteed: true);
            for (var g = 0; g < t.Groups.Count; g++)
            {
                var group = t.Groups[g];
                var gctx = $"{ctx}#group{g}";
                if (group == null || group.Entries.Count == 0) { r.Error(gctx, "empty roll group"); continue; }
                if (group.MinRolls < 0 || group.MaxRolls < group.MinRolls) r.Error(gctx, "rolls must satisfy 0 <= min <= max");
                if (group.Entries.Sum(e => Math.Max(0, e?.Weight ?? 0)) <= 0) r.Error(gctx, "total weight must be > 0");
                if (group.WithoutReplacement && group.MaxRolls > group.Entries.Count(e => e != null && e.Weight > 0))
                    r.Warn(gctx, "more rolls than entries without replacement; extra rolls give nothing");
                for (var i = 0; i < group.Entries.Count; i++) ValidateEntry(group.Entries[i], $"{gctx}#entry{i}", db, r, guaranteed: false);
            }
        }

        private static void ValidateEntry(DropEntryDef e, string ctx, GameDatabase db, ValidationReport r, bool guaranteed)
        {
            if (e == null) { r.Error(ctx, "null entry"); return; }
            if (!guaranteed)
            {
                if (e.Weight < 0) r.Error(ctx, "weight must be >= 0");
                else if (e.Weight == 0) r.Warn(ctx, "zero weight: entry can never drop");
            }
            if (e.AmountMin < 1 || e.AmountMax < e.AmountMin) r.Error(ctx, "amounts must satisfy 1 <= min <= max");
            switch (e.Type)
            {
                case DropType.Currency:
                    if (!db.IsCurrency(e.RefId)) r.Error(ctx, $"unknown currency '{e.RefId}'");
                    break;
                case DropType.HeroShard:
                    if (!db.TryGetHero(e.RefId, out _)) r.Error(ctx, $"unknown hero '{e.RefId}'");
                    break;
                case DropType.RandomHeroShardByRarity:
                    if (DropRoller.ShardCandidates(e, db).Count == 0)
                        r.Error(ctx, string.IsNullOrEmpty(e.RefId) ? $"no hero of rarity {e.Rarity}" : $"no {e.Rarity} hero with origin '{e.RefId}'");
                    break;
                case DropType.Equipment:
                    if (!db.TryGetEquipment(e.RefId, out _)) r.Error(ctx, $"unknown equipment template '{e.RefId}'");
                    break;
                case DropType.RandomEquipment:
                    if (!db.Equipment.Any(x => x != null && (e.SlotFilter == null || x.Slot == e.SlotFilter)))
                        r.Error(ctx, $"no equipment template for slot {e.SlotFilter}");
                    if (e.RarityWeights.Count == 0 || e.RarityWeights.Sum(w => Math.Max(0, w.Weight)) <= 0)
                        r.Error(ctx, "random equipment needs rarity weights with a positive sum");
                    if (e.RarityWeights.Any(w => w.Weight < 0)) r.Error(ctx, "rarity weights must be >= 0");
                    break;
            }
        }

        private static void ValidateCampaign(GameDatabase db, ValidationReport r)
        {
            if (db.Chapters.Count == 0) r.Error("campaign", "no chapters");
            var fightOwners = new Dictionary<string, string>();
            foreach (var c in db.Chapters.Where(c => c != null))
            {
                var ctx = "chapter:" + c.Id;
                if (string.IsNullOrEmpty(c.NameKey)) r.Error(ctx, "missing name key");
                if (c.FightIds.Count == 0) r.Error(ctx, "chapter has no fights");
                foreach (var fid in c.FightIds)
                {
                    if (!db.TryGetFight(fid, out _)) r.Error(ctx, $"unknown fight '{fid}'");
                    else if (fightOwners.TryGetValue(fid, out var other)) r.Error(ctx, $"fight '{fid}' is also listed in chapter '{other}' (fight ids must be unique globally)");
                    else fightOwners[fid] = c.Id;
                }
                if (!string.IsNullOrEmpty(c.ClearRewardTableId) && !db.TryGetDropTable(c.ClearRewardTableId, out _))
                    r.Error(ctx, $"unknown chapter reward table '{c.ClearRewardTableId}'");
                ValidateConditions(c.UnlockConditions, ctx, db, r);
            }
            foreach (var f in db.Fights.Where(f => f != null))
            {
                var ctx = "fight:" + f.Id;
                if (!fightOwners.ContainsKey(f.Id ?? "")) r.Warn(ctx, "fight is not part of any chapter");
                if (string.IsNullOrEmpty(f.NameKey)) r.Error(ctx, "missing name key");
                if (f.Waves.Count == 0) r.Error(ctx, "fight has no waves");
                for (var w = 0; w < f.Waves.Count; w++)
                {
                    var wave = f.Waves[w];
                    if (wave == null || wave.Spawns.Count == 0) { r.Error(ctx, $"wave {w + 1} has no enemies"); continue; }
                    if (wave.Spawns.Count > 5) r.Error(ctx, $"wave {w + 1} has more than 5 enemies");
                    foreach (var s in wave.Spawns)
                    {
                        if (s == null || !db.TryGetEnemy(s.EnemyId, out _)) r.Error(ctx, $"wave {w + 1}: unknown enemy '{s?.EnemyId}'");
                        if (s != null && s.StatMultiplier <= 0) r.Error(ctx, $"wave {w + 1}: stat multiplier must be > 0");
                    }
                }
                if (string.IsNullOrEmpty(f.DropTableId)) r.Error(ctx, "missing drop table");
                else if (!db.TryGetDropTable(f.DropTableId, out _)) r.Error(ctx, $"unknown drop table '{f.DropTableId}'");
                if (!string.IsNullOrEmpty(f.FirstClearTableId) && !db.TryGetDropTable(f.FirstClearTableId, out _))
                    r.Error(ctx, $"unknown first-clear table '{f.FirstClearTableId}'");
                if (f.RecommendedPower < 0) r.Error(ctx, "recommended power must be >= 0");
                if (f.StarTurnLimit < 0) r.Error(ctx, "star turn limit must be >= 0");
                foreach (var b in f.PreBeatIds.Concat(f.PostBeatIds))
                    if (!db.TryGetStoryBeat(b, out _)) r.Error(ctx, $"unknown story beat '{b}'");
                ValidateConditions(f.UnlockConditions, ctx, db, r);
            }
            foreach (var b in db.StoryBeats.Where(b => b != null))
                if (string.IsNullOrEmpty(b.TextKey)) r.Error("storybeat:" + b.Id, "missing text key");
        }

        private static void ValidateConditions(List<UnlockConditionDef> conditions, string ctx, GameDatabase db, ValidationReport r)
        {
            foreach (var u in conditions)
            {
                if (u == null) { r.Error(ctx, "null unlock condition"); continue; }
                switch (u.Type)
                {
                    case UnlockConditionType.ClearFight:
                        if (!db.TryGetFight(u.RefId, out _)) r.Error(ctx, $"unlock condition references unknown fight '{u.RefId}'");
                        break;
                    case UnlockConditionType.HeroOwned:
                        if (!db.TryGetHero(u.RefId, out _)) r.Error(ctx, $"unlock condition references unknown hero '{u.RefId}'");
                        break;
                    case UnlockConditionType.MinPartyPower:
                        if (u.Value <= 0) r.Error(ctx, "MinPartyPower needs a positive value");
                        break;
                }
            }
        }

        private static IEnumerable<DropEntryDef> AllEntries(GameDatabase db) =>
            db.DropTables.Where(t => t != null).SelectMany(t => t.Guaranteed.Concat(t.Groups.Where(g => g != null).SelectMany(g => g.Entries))).Where(e => e != null);

        private static void ValidateOrphans(GameDatabase db, ValidationReport r)
        {
            var entries = AllEntries(db).ToList();
            var usedTables = new HashSet<string>(db.Fights.Where(f => f != null).SelectMany(f => new[] { f.DropTableId, f.FirstClearTableId })
                .Concat(db.Chapters.Where(c => c != null).Select(c => c.ClearRewardTableId))
                .Append(db.Rules.DefeatConsolationTableId).Append(db.Rules.DailyBonusTableId)
                .Where(s => !string.IsNullOrEmpty(s)));
            foreach (var h in db.Heroes.Where(h => h != null))
            {
                var obtainable = db.Rules.StartingHeroIds.Contains(h.Id)
                    || entries.Any(e => (e.Type == DropType.HeroShard && e.RefId == h.Id) || (e.Type == DropType.RandomHeroShardByRarity && DropRoller.ShardCandidates(e, db).Contains(h)));
                if (!obtainable) r.Warn("hero:" + h.Id, "hero is never obtainable (not a starting hero and no drop gives its shards)");
            }
            var usedEnemies = new HashSet<string>(db.Fights.Where(f => f != null).SelectMany(f => f.Waves.Where(w => w != null).SelectMany(w => w.Spawns.Where(s => s != null).Select(s => s.EnemyId))));
            foreach (var e in db.Enemies.Where(e => e != null && !usedEnemies.Contains(e.Id))) r.Warn("enemy:" + e.Id, "enemy is not used by any fight");
            foreach (var t in db.DropTables.Where(t => t != null && !usedTables.Contains(t.Id))) r.Warn("droptable:" + t.Id, "drop table is not used");
            foreach (var e in db.Equipment.Where(e => e != null))
            {
                var droppable = entries.Any(x => (x.Type == DropType.Equipment && x.RefId == e.Id) || (x.Type == DropType.RandomEquipment && (x.SlotFilter == null || x.SlotFilter == e.Slot)));
                if (!droppable) r.Warn("equipment:" + e.Id, "item is never dropped");
            }
            var usedSkills = new HashSet<string>(db.Heroes.Where(h => h != null).SelectMany(h => new[] { h.BasicSkillId, h.ActiveSkillId, h.PassiveSkillId })
                .Concat(db.Enemies.Where(e => e != null).SelectMany(e => new[] { e.BasicSkillId, e.ActiveSkillId, e.PassiveSkillId })).Where(s => s != null));
            foreach (var s in db.Skills.Where(s => s != null && !usedSkills.Contains(s.Id))) r.Warn("skill:" + s.Id, "skill is not used");
        }
    }
}
