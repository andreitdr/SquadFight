using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.EditorTools
{
    /// <summary>
    /// Creates a dynamic TMP font asset from LiberationSans.ttf so every glyph in the font file (ellipsis, arrows,
    /// Romanian diacritics, …) renders on demand. The static essentials atlas only has ASCII.
    /// </summary>
    public static class FontSetup
    {
        public const string SourceFont = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        public const string FontAssetPath = "Assets/_Game/Art/Fonts/GameFont SDF.asset";

        public const string Prewarm = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?'\"()[]+-×/%…→·ăâîșțĂÂÎȘȚ";

        public static TMP_FontAsset EnsureDynamicFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                // Glyph rendering needs a GPU device: re-bake when previously created in a -nographics run.
                if (existing.characterTable.Count < Prewarm.Length - 1 && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    existing.TryAddCharacters(Prewarm, out var missing);
                    if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[FontSetup] glyphs not in font: " + missing);
                    EditorUtility.SetDirty(existing);
                    foreach (var t in existing.atlasTextures) if (t != null) EditorUtility.SetDirty(t);
                    AssetDatabase.SaveAssetIfDirty(existing);
                    Debug.Log("[FontSetup] baked " + existing.characterTable.Count + " glyphs");
                }
                return existing;
            }
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFont);
            if (font == null) { Debug.LogError("[FontSetup] missing " + SourceFont); return null; }
            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = "GameFont SDF";
            Directory.CreateDirectory(Path.GetDirectoryName(FontAssetPath));
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            asset.material.name = "GameFont SDF Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var tex in asset.atlasTextures)
            {
                tex.name = "GameFont SDF Atlas";
                AssetDatabase.AddObjectToAsset(tex, asset);
            }
            // Pre-warm common text so first frames do not stutter.
            asset.TryAddCharacters(Prewarm);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
