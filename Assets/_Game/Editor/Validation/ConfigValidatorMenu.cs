using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Validates the GameConfig graph: the pure <see cref="ConfigValidation"/> rules plus asset-level checks
    /// (null list entries, missing sprites, orphan assets). Runs from the menu and before every build.
    /// </summary>
    public static class ConfigValidatorMenu
    {
        public const string GameConfigPath = "Assets/_Game/Resources/GameConfig.asset";

        public static GameConfig LoadGameConfig() => AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);

        public static ValidationReport Validate(GameConfig config)
        {
            if (config == null)
            {
                var missing = new ValidationReport();
                missing.Error("GameConfig", $"no GameConfig at {GameConfigPath}");
                return missing;
            }
            var cdb = new ConfigDatabase(config);
            var report = ConfigValidation.Validate(cdb.Database);
            foreach (var issue in cdb.GatherIssues) report.Error("GameConfig", issue);
            if (!config.rarityConfig) report.Error("GameConfig", "rarity config not assigned");
            if (!config.starConfig) report.Error("GameConfig", "star config not assigned");
            if (!config.combatConfig) report.Error("GameConfig", "combat config not assigned");

            foreach (var h in cdb.HeroAssets)
            {
                if (!h.portrait) report.Error("hero:" + h.id, "missing portrait sprite");
                if (!h.basicSkill) report.Error("hero:" + h.id, "basic skill not assigned");
            }
            foreach (var e in cdb.EnemyAssets)
                if (!e.sprite) report.Error("enemy:" + e.id, "missing sprite");
            foreach (var e in cdb.EquipmentAssets)
                if (!e.icon) report.Error("equipment:" + e.id, "missing icon");
            foreach (var f in cdb.FightAssets)
            {
                if (!f.dropTable) report.Error("fight:" + f.id, "drop table not assigned");
                if (!f.background) report.Warn("fight:" + f.id, "no background sprite");
            }
            CheckAssetIdsMatchNames(report, cdb);
            var loc = LocalizationTools.LoadTables();
            foreach (var key in LocalizationTools.ContentKeys(cdb).Distinct())
                if (!string.IsNullOrEmpty(key) && !loc.HasIn(TableLocalizer.Fallback, key)) report.Error("localization", $"key '{key}' has no English text (players would see the raw key)");
            CheckOrphanAssets(report, cdb);
            return report;
        }

        private static void CheckAssetIdsMatchNames(ValidationReport report, ConfigDatabase cdb)
        {
            IEnumerable<ContentDefinition> all = cdb.HeroAssets.Cast<ContentDefinition>().Concat(cdb.EnemyAssets).Concat(cdb.SkillAssets)
                .Concat(cdb.EquipmentAssets).Concat(cdb.DropTableAssets).Concat(cdb.ChapterAssets).Concat(cdb.FightAssets).Concat(cdb.StoryBeatAssets);
            foreach (var a in all)
                if (a.name != a.id) report.Warn($"{a.GetType().Name}:{a.id}", $"asset file name '{a.name}' differs from id");
        }

        private static void CheckOrphanAssets(ValidationReport report, ConfigDatabase cdb)
        {
            void Check<T>(ICollection<T> reachable) where T : ContentDefinition
            {
                foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
                {
                    var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                    if (a && !reachable.Contains(a)) report.Warn($"{typeof(T).Name}:{a.id}", $"asset at {AssetDatabase.GUIDToAssetPath(guid)} is not referenced by GameConfig");
                }
            }
            Check(cdb.HeroAssets);
            Check(cdb.EnemyAssets);
            Check(cdb.SkillAssets);
            Check(cdb.EquipmentAssets);
            Check(cdb.DropTableAssets);
            Check(cdb.ChapterAssets);
            Check(cdb.FightAssets);
            Check(cdb.StoryBeatAssets);
        }

        [MenuItem("Tools/Game/Validate Config")]
        public static void ValidateMenu()
        {
            var report = Validate(LoadGameConfig());
            Log(report);
            EditorUtility.DisplayDialog("Config validation",
                report.HasErrors ? $"{report.Errors.Count()} error(s), {report.Warnings.Count()} warning(s). See Console." : $"OK ({report.Warnings.Count()} warning(s)).", "OK");
        }

        /// <summary>Batch entry: exits with code 1 on errors.</summary>
        public static void ValidateBatch()
        {
            var report = Validate(LoadGameConfig());
            Log(report);
            if (report.HasErrors) EditorApplication.Exit(1);
        }

        public static void Log(ValidationReport report)
        {
            var text = report.ToString();
            if (report.HasErrors) Debug.LogError(text);
            else if (report.Warnings.Any()) Debug.LogWarning(text);
            else Debug.Log(text);
        }
    }

    /// <summary>Blocks player builds when content is invalid.</summary>
    public sealed class ConfigValidationBuildHook : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Check(ConfigValidatorMenu.LoadGameConfig());

        public static void Check(GameConfig config)
        {
            var result = ConfigValidatorMenu.Validate(config);
            ConfigValidatorMenu.Log(result);
            if (result.HasErrors)
                throw new BuildFailedException("Config validation failed:\n" + string.Join("\n", result.Errors.Select(e => e.ToString())));
        }
    }
}
