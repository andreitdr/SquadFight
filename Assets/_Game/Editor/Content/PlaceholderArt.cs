using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools.Content
{
    /// <summary>
    /// Generates white procedural placeholder sprites (tinted at runtime). Every file gets the "placeholder" asset label
    /// so the step-18 audit can list what still needs final art.
    /// </summary>
    public static class PlaceholderArt
    {
        public const string Folder = "Assets/_Game/Art/Placeholder";
        public const string Label = "placeholder";

        public static string PathOf(string name) => $"{Folder}/{name}.png";

        public static Sprite Get(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(PathOf(name));

        [MenuItem("Tools/Game/Content/Generate Placeholder Art")]
        public static void EnsureAll()
        {
            Directory.CreateDirectory(Folder);
            var made = 0;
            made += Make("circle", 256, (x, y) => Circle(x, y, 0.47f));
            made += Make("ring", 256, (x, y) => Math.Min(Circle(x, y, 0.47f), 1f - Circle(x, y, 0.38f)));
            made += Make("diamond", 256, (x, y) => Edge(0.47f - (Math.Abs(x) + Math.Abs(y)) * 0.75f));
            made += Make("square", 256, (x, y) => Edge(0.46f - Math.Max(Math.Abs(x), Math.Abs(y))));
            made += Make("rounded", 256, (x, y) => RoundedRect(x, y, 0.46f, 0.12f));
            made += Make("star", 256, Star);
            made += Make("shard", 256, (x, y) => Edge(0.45f - (Math.Abs(x) * 1.8f + Math.Abs(y) * 0.9f)));
            made += Make("triangle", 256, (x, y) => Edge(Math.Min(y + 0.4f, 0.42f - (Math.Abs(x) * 1.7f - y * 0.98f) * 0.5f)));
            made += Make("soft_dot", 128, (x, y) => (float)Math.Pow(Math.Max(0, 1 - Math.Sqrt(x * x + y * y) / 0.5f), 2));
            made += Make("pixel", 4, (x, y) => 1f);
            // Equipment slot icons
            made += Make("icon_weapon", 128, (x, y) => Math.Max(Edge(0.06f - Math.Abs(x + y)) * (Math.Abs(x - y) < 0.62f ? 1 : 0), Edge(0.08f - Math.Abs(x - y + 0.42f)) * (Math.Abs(x + y + 0.0f) < 0.25f ? 1 : 0)));
            made += Make("icon_helmet", 128, (x, y) => y > -0.05f ? Circle(x, y + 0.05f, 0.38f) : Edge(0.38f - Math.Abs(x)) * Edge(y + 0.3f));
            made += Make("icon_chest", 128, (x, y) => RoundedRect(x, y + 0.02f, 0.34f, 0.08f) * (Math.Abs(x) < 0.1f && y > 0.2f ? 0 : 1));
            made += Make("icon_gloves", 128, (x, y) => Math.Max(RoundedRect(x, y + 0.12f, 0.26f, 0.1f), Edge(0.07f - Math.Abs(x + 0.33f)) * Edge(0.2f - Math.Abs(y + 0.05f))));
            made += Make("icon_boots", 128, (x, y) => Math.Max(Edge(0.14f - Math.Abs(x + 0.05f)) * Edge(0.4f - Math.Abs(y - 0.05f)), RoundedRect((x - 0.1f) * 0.75f, (y + 0.32f) * 1.6f, 0.3f, 0.1f)));
            made += Make("icon_ring", 128, (x, y) => Math.Max(Math.Min(Circle(x, y + 0.08f, 0.32f), 1f - Circle(x, y + 0.08f, 0.22f)), Circle(x, y - 0.3f, 0.12f)));
            made += Make("background", 64, (x, y) => 0.55f + 0.45f * (y + 0.5f), alphaIsShape: false);
            if (made > 0) AssetDatabase.Refresh();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
                ConfigureImporter(AssetDatabase.GUIDToAssetPath(guid));
            AssetDatabase.SaveAssets();
        }

        private static int Make(string name, int size, Func<float, float, float> shape, bool alphaIsShape = true)
        {
            var path = PathOf(name);
            if (File.Exists(path)) return 0;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (var j = 0; j < size; j++)
                for (var i = 0; i < size; i++)
                {
                    var x = (i + 0.5f) / size - 0.5f;
                    var y = (j + 0.5f) / size - 0.5f;
                    var v = Mathf.Clamp01(shape(x, y));
                    px[j * size + i] = alphaIsShape ? new Color(1, 1, 1, v) : new Color(v, v, v, 1);
                }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            return 1;
        }

        private static void ConfigureImporter(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            var changed = imp.textureType != TextureImporterType.Sprite || imp.mipmapEnabled;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.spritePixelsPerUnit = 100;
            if (path.EndsWith("rounded.png") || path.EndsWith("pixel.png"))
            {
                var border = path.EndsWith("pixel.png") ? 1 : 40;
                if (imp.spriteBorder != new Vector4(border, border, border, border)) { imp.spriteBorder = new Vector4(border, border, border, border); changed = true; }
            }
            if (changed) imp.SaveAndReimport();
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj != null && Array.IndexOf(AssetDatabase.GetLabels(obj), Label) < 0) AssetDatabase.SetLabels(obj, new[] { Label });
        }

        private static float Edge(float d) => Mathf.Clamp01(d * 256f / 2f + 0.5f);
        private static float Circle(float x, float y, float r) => Edge(r - (float)Math.Sqrt(x * x + y * y));
        private static float RoundedRect(float x, float y, float half, float radius)
        {
            var qx = Math.Abs(x) - (half - radius);
            var qy = Math.Abs(y) - (half - radius);
            var outside = (float)Math.Sqrt(Math.Max(qx, 0) * Math.Max(qx, 0) + Math.Max(qy, 0) * Math.Max(qy, 0));
            var inside = Math.Min(Math.Max(qx, qy), 0);
            return Edge(radius - (outside + inside));
        }
        private static float Star(float x, float y)
        {
            var a = Math.Atan2(y, x);
            var r = Math.Sqrt(x * x + y * y);
            var k = 0.3 + 0.17 * Math.Cos(5 * (a - Math.PI / 2));
            return Edge((float)(k - r));
        }
    }
}
