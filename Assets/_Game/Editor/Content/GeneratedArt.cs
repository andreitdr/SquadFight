using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.UI;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools.Content
{
    /// <summary>
    /// Hooks the generated art (tools/art → Assets/_Game/Art/Generated) into the game:
    ///  • import settings for every texture in the folder (sprites, no mips, 9-slice borders from UI/slices.json);
    ///  • one <see cref="SpriteAnimSet"/> per character built from its anim.json;
    ///  • assigns portraits/icons/card art/animations, item icons, chapter backgrounds and the Theme skin.
    /// Only empty slots, generated art and "placeholder" assets are overwritten, so art you assign by hand is kept.
    /// To swap a picture keep the file name and overwrite the PNG; to replace it for good assign your own sprite.
    /// </summary>
    public static class GeneratedArt
    {
        public const string Root = "Assets/_Game/Art/Generated";
        public const string Label = "generated-art";
        private const string Chars = Root + "/Characters";
        private const string UI = Root + "/UI";
        private const string Icons = Root + "/Icons";
        private const string Items = Root + "/Items";
        private const string Bgs = Root + "/Backgrounds";

        [Serializable]
        private sealed class AnimManifest
        {
            public string id;
            public float heightRatio = 0.7f;
            public float feetFromBottom = 0.07f;
            public ClipEntry[] clipList = new ClipEntry[0];
        }

        [Serializable]
        private sealed class ClipEntry
        {
            public string name;
            public float fps;
            public bool loop;
            public string[] frames = new string[0];
        }

        /// <summary>On-screen character height relative to the battle unit's body size.</summary>
        public const float CharacterHeightInBody = 1.3f;

        [MenuItem("Tools/Game/Art/Apply Generated Art")]
        public static void Apply()
        {
            var report = Run();
            Debug.Log("[GeneratedArt] " + report);
            EditorUtility.DisplayDialog("Generated art", report, "OK");
        }

        /// <summary>Batch mode: -executeMethod Game.EditorTools.Content.GeneratedArt.ApplyBatch</summary>
        public static void ApplyBatch()
        {
            try
            {
                Debug.Log("[GeneratedArt] " + Run());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static string Run()
        {
            AssetDatabase.Refresh();
            ConfigureAllImporters();
            var anims = BuildAnimSets();
            var n = 0;
            foreach (var hero in Find<HeroDefinition>())
            {
                var dir = $"{Chars}/{hero.id}";
                n += Assign(hero, ref hero.portrait, LoadSprite($"{dir}/portrait.png"));
                n += Assign(hero, ref hero.icon, LoadSprite($"{dir}/icon.png"));
                n += Assign(hero, ref hero.cardArt, LoadSprite($"{dir}/card_bust.png"));
                if (anims.TryGetValue(hero.id, out var set)) n += Assign(hero, ref hero.animations, set);
            }
            foreach (var enemy in Find<EnemyDefinition>())
            {
                var dir = $"{Chars}/{enemy.id}";
                n += Assign(enemy, ref enemy.portrait, LoadSprite($"{dir}/portrait.png"));
                n += Assign(enemy, ref enemy.icon, LoadSprite($"{dir}/icon.png"));
                n += Assign(enemy, ref enemy.sprite, LoadSprite($"{dir}/portrait.png"));
                if (anims.TryGetValue(enemy.id, out var set)) n += Assign(enemy, ref enemy.animations, set);
            }
            foreach (var beat in Find<StoryBeatDefinition>())
            {
                // Speaker art: hero/enemy icon by id (speakerKey = "<id>_name"), the story icon for narrators.
                var speaker = beat.speakerKey != null && beat.speakerKey.EndsWith("_name") ? beat.speakerKey.Substring(0, beat.speakerKey.Length - 5) : null;
                var icon = speaker != null ? LoadSprite($"{Chars}/{speaker}/icon.png") : null;
                n += Assign(beat, ref beat.portrait, icon != null ? icon : LoadSprite($"{Icons}/ui_story.png"));
            }
            foreach (var item in Find<EquipmentTemplate>())
                n += Assign(item, ref item.icon, LoadSprite($"{Items}/{item.id}.png"));
            foreach (var chapter in Find<ChapterDefinition>())
            {
                n += Assign(chapter, ref chapter.background, LoadSprite($"{Bgs}/{chapter.id}.jpg"));
                n += Assign(chapter, ref chapter.banner, LoadSprite($"{Bgs}/{chapter.id}_banner.jpg"));
            }
            n += SkinTheme();
            AssetDatabase.SaveAssets();
            return $"{anims.Count} animation sets, {n} references assigned.";
        }

        // ------------------------------------------------------------------ importers

        public static bool IsGeneratedPath(string path) => path.Replace('\\', '/').StartsWith(Root + "/", StringComparison.Ordinal);

        private static Dictionary<string, Vector4> _slices;

        /// <summary>9-slice borders (left, bottom, right, top) written by tools/art/gen_ui.py.</summary>
        public static Vector4 BorderFor(string path)
        {
            if (_slices == null)
            {
                _slices = new Dictionary<string, Vector4>();
                var json = UI + "/slices.json";
                if (File.Exists(json))
                {
                    // Tiny parser for {"name": [l, b, r, t], ...} (JsonUtility cannot read dictionaries).
                    var text = File.ReadAllText(json);
                    foreach (var part in text.Split(new[] { "]," , "]" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var colon = part.IndexOf(':');
                        if (colon < 0) continue;
                        var name = part.Substring(0, colon).Trim(' ', '\n', '\r', '\t', '{', '"', ',');
                        var nums = part.Substring(colon + 1).Trim(' ', '\n', '\r', '\t', '[').Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        if (nums.Length == 4) _slices[name] = new Vector4(nums[0], nums[1], nums[2], nums[3]);
                    }
                }
            }
            return _slices.TryGetValue(Path.GetFileNameWithoutExtension(path), out var b) ? b : Vector4.zero;
        }

        public static void Configure(TextureImporter imp, string path)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.spritePixelsPerUnit = 100;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.compressionQuality = 60;
            imp.maxTextureSize = path.Contains("/Backgrounds/") ? 2048 : 1024;
            imp.spriteBorder = BorderFor(path);
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // uGUI Images; avoids tight-mesh edge artifacts
            imp.SetTextureSettings(settings);
        }

        private static void ConfigureAllImporters()
        {
            _slices = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;
                var before = EditorJsonUtility.ToJson(imp);
                Configure(imp, path);
                if (EditorJsonUtility.ToJson(imp) != before) imp.SaveAndReimport();
                var obj = AssetDatabase.LoadMainAssetAtPath(path);
                if (obj != null && !AssetDatabase.GetLabels(obj).Contains(Label)) AssetDatabase.SetLabels(obj, new[] { Label });
            }
        }

        // ------------------------------------------------------------------ animation sets

        private static Dictionary<string, SpriteAnimSet> BuildAnimSets()
        {
            var result = new Dictionary<string, SpriteAnimSet>();
            if (!AssetDatabase.IsValidFolder(Chars)) return result;
            foreach (var dir in Directory.GetDirectories(Chars))
            {
                var json = Path.Combine(dir, "anim.json");
                if (!File.Exists(json)) continue;
                var m = JsonUtility.FromJson<AnimManifest>(File.ReadAllText(json));
                var id = Path.GetFileName(dir);
                var folder = $"{Chars}/{id}";
                var path = $"{folder}/{id}_anim.asset";
                var set = AssetDatabase.LoadAssetAtPath<SpriteAnimSet>(path);
                if (set == null)
                {
                    set = ScriptableObject.CreateInstance<SpriteAnimSet>();
                    AssetDatabase.CreateAsset(set, path);
                }
                set.displayScale = CharacterHeightInBody / Mathf.Max(0.3f, m.heightRatio);
                set.feetY = m.feetFromBottom;
                set.clips = m.clipList.Select(c => new SpriteAnimSet.Clip
                {
                    name = c.name, fps = c.fps, loop = c.loop,
                    frames = c.frames.Select(f => LoadSprite($"{folder}/anim/{f}")).Where(s => s != null).ToArray(),
                }).ToList();
                EditorUtility.SetDirty(set);
                AssetDatabase.SetLabels(set, new[] { Label });
                result[id] = set;
            }
            return result;
        }

        // ------------------------------------------------------------------ theme

        private static int SkinTheme()
        {
            var theme = Resources.Load<Theme>(Theme.ResourcePath);
            if (theme == null) return 0;
            var n = 0;
            n += Assign(theme, ref theme.panelSprite, LoadSprite($"{UI}/panel.png"));
            n += Assign(theme, ref theme.buttonSprite, LoadSprite($"{UI}/button.png"));
            n += Assign(theme, ref theme.barBackground, LoadSprite($"{UI}/bar_bg.png"));
            n += Assign(theme, ref theme.barFill, LoadSprite($"{UI}/bar_fill.png"));
            n += Assign(theme, ref theme.bubble, LoadSprite($"{UI}/bubble.png"));
            n += Assign(theme, ref theme.bubbleTail, LoadSprite($"{UI}/bubble_tail.png"));
            n += Assign(theme, ref theme.glow, LoadSprite($"{UI}/glow.png"));
            n += Assign(theme, ref theme.vignette, LoadSprite($"{UI}/vignette.png"));
            n += Assign(theme, ref theme.ribbon, LoadSprite($"{UI}/ribbon.png"));
            n += Assign(theme, ref theme.starFull, LoadSprite($"{UI}/star_full.png"));
            n += Assign(theme, ref theme.starEmpty, LoadSprite($"{UI}/star_empty.png"));
            n += Assign(theme, ref theme.redDotSprite, LoadSprite($"{UI}/red_dot.png"));
            n += Assign(theme, ref theme.logo, LoadSprite($"{UI}/logo.png"));
            n += Assign(theme, ref theme.appBackground, LoadSprite($"{Bgs}/home.jpg"));
            n += Assign(theme, ref theme.battleBackground, LoadSprite($"{Bgs}/chapter_1.jpg"));
            n += Assign(theme, ref theme.iconGold, LoadSprite($"{Icons}/currency_gold.png"));
            n += Assign(theme, ref theme.iconShard, LoadSprite($"{Icons}/shard.png"));
            n += Assign(theme, ref theme.iconPower, LoadSprite($"{Icons}/power.png"));
            n += Assign(theme, ref theme.iconLock, LoadSprite($"{Icons}/ui_lock.png"));
            n += Assign(theme, ref theme.iconPause, LoadSprite($"{Icons}/ui_pause.png"));
            n += Assign(theme, ref theme.iconSpeed, LoadSprite($"{Icons}/ui_speed.png"));
            n += Assign(theme, ref theme.iconAuto, LoadSprite($"{Icons}/ui_auto.png"));
            n += Assign(theme, ref theme.iconSkip, LoadSprite($"{Icons}/ui_skip.png"));
            n += Assign(theme, ref theme.iconParty, LoadSprite($"{Icons}/ui_party.png"));
            n += Assign(theme, ref theme.iconBoss, LoadSprite($"{Icons}/ui_boss.png"));
            n += Assign(theme, ref theme.iconStory, LoadSprite($"{Icons}/ui_story.png"));
            n += Assign(theme, ref theme.tabHome, LoadSprite($"{Icons}/tab_home.png"));
            n += Assign(theme, ref theme.tabCampaign, LoadSprite($"{Icons}/tab_campaign.png"));
            n += Assign(theme, ref theme.tabHeroes, LoadSprite($"{Icons}/tab_heroes.png"));
            n += Assign(theme, ref theme.tabInventory, LoadSprite($"{Icons}/tab_inventory.png"));
            n += Assign(theme, ref theme.tabSettings, LoadSprite($"{Icons}/tab_settings.png"));
            n += Assign(theme, ref theme.iconWeapon, LoadSprite($"{Icons}/slot_weapon.png"));
            n += Assign(theme, ref theme.iconHelmet, LoadSprite($"{Icons}/slot_helmet.png"));
            n += Assign(theme, ref theme.iconChest, LoadSprite($"{Icons}/slot_chest.png"));
            n += Assign(theme, ref theme.iconGloves, LoadSprite($"{Icons}/slot_gloves.png"));
            n += Assign(theme, ref theme.iconBoots, LoadSprite($"{Icons}/slot_boots.png"));
            n += Assign(theme, ref theme.iconRing, LoadSprite($"{Icons}/slot_ring.png"));
            n += Assign(theme, ref theme.statusStun, LoadSprite($"{Icons}/status_stun.png"));
            n += Assign(theme, ref theme.statusShield, LoadSprite($"{Icons}/status_shield.png"));
            n += Assign(theme, ref theme.statusHeal, LoadSprite($"{Icons}/status_heal.png"));
            n += Assign(theme, ref theme.statusAttackUp, LoadSprite($"{Icons}/status_attack_up.png"));
            n += Assign(theme, ref theme.statusAttackDown, LoadSprite($"{Icons}/status_attack_down.png"));
            n += Assign(theme, ref theme.statusDefenseUp, LoadSprite($"{Icons}/status_defense_up.png"));
            n += Assign(theme, ref theme.statusDefenseDown, LoadSprite($"{Icons}/status_defense_down.png"));
            n += Assign(theme, ref theme.statusSpeedUp, LoadSprite($"{Icons}/status_speed_up.png"));
            n += Assign(theme, ref theme.statusSpeedDown, LoadSprite($"{Icons}/status_speed_down.png"));
            n += Assign(theme, ref theme.statusCritUp, LoadSprite($"{Icons}/status_crit_up.png"));
            n += Assign(theme, ref theme.statusCritDown, LoadSprite($"{Icons}/status_crit_down.png"));
            var rarities = Enum.GetNames(typeof(Rarity)).Select(r => r.ToLowerInvariant()).ToArray();
            n += AssignArray(theme, ref theme.rarityGems, rarities.Select(r => LoadSprite($"{UI}/gem_{r}.png")).ToArray());
            n += AssignArray(theme, ref theme.cardFrames, rarities.Select(r => LoadSprite($"{UI}/card_frame_{r}.png")).ToArray());
            n += AssignArray(theme, ref theme.cardBacks, rarities.Select(r => LoadSprite($"{UI}/card_back_{r}.png")).ToArray());
            var classes = Enum.GetNames(typeof(HeroClass)).Select(c => c.ToLowerInvariant()).ToArray();
            n += AssignArray(theme, ref theme.classIcons, classes.Select(c => LoadSprite($"{Icons}/class_{c}.png")).ToArray());
            return n;
        }

        // ------------------------------------------------------------------ helpers

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static IEnumerable<T> Find<T>() where T : ScriptableObject =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).Where(a => a != null);

        private static bool IsGenerated(UnityEngine.Object o)
        {
            if (o == null) return false;
            var path = AssetDatabase.GetAssetPath(o);
            return IsGeneratedPath(path) || AssetDatabase.GetLabels(o).Contains(PlaceholderArt.Label);
        }

        /// <summary>Sets <paramref name="slot"/> when it is empty or still holds generated/placeholder art.</summary>
        private static int Assign<T>(UnityEngine.Object owner, ref T slot, T value) where T : UnityEngine.Object
        {
            if (value == null || slot == value) return 0;
            if (slot != null && !IsGenerated(slot)) return 0;
            slot = value;
            EditorUtility.SetDirty(owner);
            return 1;
        }

        private static int AssignArray(UnityEngine.Object owner, ref Sprite[] slot, Sprite[] values)
        {
            if (values.Any(v => v == null)) return 0;
            if (slot != null && slot.Length == values.Length && slot.SequenceEqual(values)) return 0;
            if (slot != null && slot.Any(s => s != null && !IsGenerated(s))) return 0;
            slot = values;
            EditorUtility.SetDirty(owner);
            return 1;
        }
    }

    /// <summary>Any texture dropped into the generated folder (including swapped files) imports as a ready-to-use sprite.</summary>
    public sealed class GeneratedArtPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!GeneratedArt.IsGeneratedPath(assetPath)) return;
            var imp = (TextureImporter)assetImporter;
            if (!imp.importSettingsMissing) return; // keep hand-tuned settings on re-import
            GeneratedArt.Configure(imp, assetPath);
        }
    }
}
