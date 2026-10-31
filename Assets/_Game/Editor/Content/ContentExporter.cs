using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools.Content
{
    /// <summary>
    /// Exports the most-tuned tables back to CSV (heroes, enemies, equipment, rarities, stars) so balance changes made in the
    /// Inspector can return to the spreadsheet. Text columns are taken from the localization table.
    /// </summary>
    public static class ContentExporter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static string N(float v) => v.ToString("0.###", Inv);

        [MenuItem("Tools/Game/Content/Export Tables to CSV")]
        public static void ExportMenu()
        {
            Export(ContentPaths.Source);
            AssetDatabase.Refresh();
            Debug.Log("[Export] heroes/enemies/equipment/rarities/stars/ranks written to " + ContentPaths.Source);
        }

        public static void Export(string folder)
        {
            var cdb = new ConfigDatabase(ConfigValidatorMenu.LoadGameConfig());
            var text = LocalizationTools.LoadTables();
            string T(string key) => text.HasIn("en", key) ? text.Get(key) : "";
            var statCols = StatBlock.All.Select(s => char.ToLowerInvariant(s.ToString()[0]) + s.ToString().Substring(1)).ToArray();

            File.WriteAllText(Path.Combine(folder, "heroes.csv"), Csv.Write(new[] { "id", "name", "rarity", "class", "origin" }.Concat(statCols).Concat(new[] { "basicSkill", "activeSkill", "passive", "tint", "lore" }),
                cdb.Source.heroes.Select(h => new[] { h.id, T(h.nameKey), h.rarity.ToString(), h.heroClass.ToString(), h.origin ?? "" }
                    .Concat(StatBlock.All.Select(s => N(h.baseStats[s])))
                    .Concat(new[] { h.basicSkill ? h.basicSkill.id : "", h.activeSkill ? h.activeSkill.id : "", h.passiveSkill ? h.passiveSkill.id : "", "#" + ColorUtility.ToHtmlStringRGB(h.tint), T(h.loreKey) }))));

            string Ids(IEnumerable<SkillDefinition> list) => string.Join(";", list.Where(x => x).Select(x => x.id));
            File.WriteAllText(Path.Combine(folder, "enemies.csv"), Csv.Write(new[] { "id", "name", "rank" }.Concat(statCols).Concat(new[] { "basicSkill", "activeSkill", "passive", "extraSkills", "specials", "tint", "art" }),
                OrderLike(folder, "enemies.csv", cdb.EnemyAssets).Select(e => new[] { e.id, T(e.nameKey), e.rank.ToString() }
                    .Concat(StatBlock.All.Select(s => N(e.baseStats[s])))
                    .Concat(new[] { e.basicSkill ? e.basicSkill.id : "", e.activeSkill ? e.activeSkill.id : "", e.passiveSkill ? e.passiveSkill.id : "", Ids(e.extraSkills), Ids(e.specials), "#" + ColorUtility.ToHtmlStringRGB(e.tint), e.artFrom ?? "" }))));

            File.WriteAllText(Path.Combine(folder, "equipment.csv"), Csv.Write(new[] { "id", "name", "slot", "rarity", "tier", "stats", "setId" },
                cdb.Source.equipment.Select(e => new[] { e.id, T(e.nameKey), e.slot.ToString(), e.rarity.ToString(), e.tier.ToString(Inv),
                    string.Join(";", e.stats.Select(m => m.kind == ModifierKind.Percent ? $"{m.stat}={N(m.value * 100)}%" : $"{m.stat}={N(m.value)}")), e.setId ?? "" })));

            var rc = cdb.Source.rarityConfig;
            File.WriteAllText(Path.Combine(folder, "rarities.csv"), Csv.Write(new[] { "rarity", "color", "equipmentStatMultiplier", "shardsToUnlock", "starCostMultiplier", "dropWeightModifier", "itemSellValue", "overflowCurrencyPerShard" },
                rc.entries.Select(e => new[] { e.rarity.ToString(), "#" + ColorUtility.ToHtmlStringRGB(e.color), N(e.equipmentStatMultiplier), e.shardsToUnlock.ToString(Inv), N(e.starCostMultiplier), N(e.dropWeightModifier), e.itemSellValue.ToString(Inv), e.overflowCurrencyPerShard.ToString(Inv) })));

            File.WriteAllText(Path.Combine(folder, "stars.csv"), Csv.Write(new[] { "star", "shardsRequired", "currencyCost", "statMultiplier" },
                cdb.Source.starConfig.levels.Select(l => new[] { l.star.ToString(Inv), l.shardsRequired.ToString(Inv), l.currencyCost.ToString(Inv), N(l.statMultiplier) })));

            if (cdb.Source.rankConfig)
                File.WriteAllText(Path.Combine(folder, "ranks.csv"), Csv.Write(new[] { "rank", "hpMultiplier", "xp", "minSkills" },
                    cdb.Source.rankConfig.entries.Select(e => new[] { e.rank.ToString(), N(e.hpMultiplier), e.xp.ToString(Inv), e.minSkills.ToString(Inv) })));
        }

        /// <summary>Keeps the existing row order of a CSV when exporting assets gathered in another order.</summary>
        private static IEnumerable<T> OrderLike<T>(string folder, string file, IEnumerable<T> assets) where T : ContentDefinition
        {
            var path = Path.Combine(folder, file);
            if (!File.Exists(path)) return assets;
            var order = Csv.Read(path).Rows.Select(r => r.Str("id")).ToList();
            return assets.OrderBy(a => { var i = order.IndexOf(a.id); return i < 0 ? int.MaxValue : i; });
        }
    }
}
