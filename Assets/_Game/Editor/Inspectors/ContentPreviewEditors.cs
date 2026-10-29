using System.Linq;
using Game.Core;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    internal static class PreviewUtil
    {
        public static GameConfig Config => ConfigValidatorMenu.LoadGameConfig();

        public static void Box(string text)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(text, MessageType.Info);
        }
    }

    /// <summary>Hero inspector with computed previews: unlock cost, star-up costs and stats per star.</summary>
    [CustomEditor(typeof(HeroDefinition))]
    public sealed class HeroDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var hero = (HeroDefinition)target;
            var cfg = PreviewUtil.Config;
            if (!cfg || !cfg.rarityConfig || !cfg.starConfig) return;
            var r = cfg.rarityConfig.Get(hero.rarity);
            if (r == null) return;
            var lines = $"Unlock: {r.shardsToUnlock} shards";
            var total = r.shardsToUnlock;
            var stats = hero.baseStats.ToStatBlock();
            foreach (var level in cfg.starConfig.levels.Where(l => l.star > 1).OrderBy(l => l.star))
            {
                var cost = Mathf.CeilToInt(level.shardsRequired * r.starCostMultiplier);
                total += cost;
                lines += $"\n★{level.star}: {cost} shards + {level.currencyCost} {cfg.primaryCurrencyId}  →  HP {stats[StatType.MaxHp] * level.statMultiplier:0}, ATK {stats[StatType.Attack] * level.statMultiplier:0}";
            }
            lines += $"\nTotal shards to max star: {total}";
            PreviewUtil.Box(lines);
        }
    }

    [CustomEditor(typeof(StarUpgradeConfig))]
    public sealed class StarUpgradeConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var cfg = (StarUpgradeConfig)target;
            var cumulative = 0;
            var text = "Cumulative shards (×1 rarity multiplier):";
            foreach (var l in cfg.levels.OrderBy(l => l.star))
            {
                cumulative += l.star > 1 ? l.shardsRequired : 0;
                text += $"\n★{l.star}: {cumulative} shards, ×{l.statMultiplier:0.00} stats";
            }
            PreviewUtil.Box(text);
        }
    }

    [CustomEditor(typeof(EquipmentTemplate))]
    public sealed class EquipmentTemplateEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var t = (EquipmentTemplate)target;
            var cfg = PreviewUtil.Config;
            if (!cfg || !cfg.rarityConfig) return;
            var text = "Stats by rarity:";
            foreach (var e in cfg.rarityConfig.entries)
                text += $"\n{e.rarity}: " + string.Join(", ", t.stats.Select(s => s.kind == ModifierKind.Percent
                    ? $"{s.stat} +{s.value * e.equipmentStatMultiplier * 100:0.#}%"
                    : $"{s.stat} +{s.value * e.equipmentStatMultiplier:0.##}"));
            PreviewUtil.Box(text);
        }
    }

    [CustomEditor(typeof(DropTable))]
    public sealed class DropTableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var t = (DropTable)target;
            var text = $"Guaranteed entries: {t.guaranteed.Count}";
            for (var g = 0; g < t.groups.Count; g++)
            {
                var group = t.groups[g];
                var total = group.entries.Sum(e => Mathf.Max(0, e.weight));
                text += $"\nGroup {g + 1} ({group.minRolls}-{group.maxRolls} rolls):";
                foreach (var e in group.entries)
                    text += $"\n   {e.type} {e.refId} {(e.type == DropType.RandomHeroShardByRarity || e.type == DropType.Equipment ? e.rarity.ToString() : "")}: {(total > 0 ? 100f * e.weight / total : 0):0.#}%";
            }
            PreviewUtil.Box(text);
        }
    }
}
