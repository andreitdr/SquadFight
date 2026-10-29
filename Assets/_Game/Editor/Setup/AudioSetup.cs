using System.IO;
using System.Linq;
using Game.Data;
using Game.EditorTools.Content;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Builds Resources/AudioLibrary from the clips in Audio/Placeholder (or Audio/Final when present) and sets import settings.</summary>
    public static class AudioSetup
    {
        public const string LibraryPath = "Assets/_Game/Resources/AudioLibrary.asset";
        public const string PlaceholderFolder = "Assets/_Game/Audio/Placeholder";
        public const string FinalFolder = "Assets/_Game/Audio/Final";

        [MenuItem("Tools/Game/Setup/Build Audio Library")]
        public static AudioLibrary Build()
        {
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.sfx.Clear();
            lib.music.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { PlaceholderFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var id = Path.GetFileNameWithoutExtension(path);
                // A final clip with the same id replaces the placeholder.
                var finalPath = Directory.Exists(FinalFolder) ? Directory.GetFiles(FinalFolder).FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == id && !f.EndsWith(".meta")) : null;
                if (finalPath != null) path = finalPath.Replace('\\', '/');
                var isMusic = id.StartsWith("music_");
                Configure(path, isMusic, placeholder: finalPath == null);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var entry = new AudioLibrary.Entry { id = id, clips = { clip }, volume = isMusic ? 0.7f : 0.9f, pitchVariance = id == "hit" || id == "crit" ? 0.08f : 0f, cooldown = isMusic ? 0 : 0.04f };
                (isMusic ? lib.music : lib.sfx).Add(entry);
            }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AudioSetup] {lib.sfx.Count} sfx, {lib.music.Count} music tracks");
            return lib;
        }

        private static void Configure(string path, bool music, bool placeholder)
        {
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) return;
            var s = imp.defaultSampleSettings;
            var target = music
                ? new AudioImporterSampleSettings { loadType = AudioClipLoadType.Streaming, compressionFormat = AudioCompressionFormat.Vorbis, quality = 0.5f, sampleRateSetting = s.sampleRateSetting }
                : new AudioImporterSampleSettings { loadType = AudioClipLoadType.DecompressOnLoad, compressionFormat = AudioCompressionFormat.ADPCM, quality = 1f, sampleRateSetting = s.sampleRateSetting };
            var changed = s.loadType != target.loadType || s.compressionFormat != target.compressionFormat || imp.forceToMono != true;
            imp.defaultSampleSettings = target;
            imp.forceToMono = true;
            if (changed) imp.SaveAndReimport();
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (placeholder && obj != null && !AssetDatabase.GetLabels(obj).Contains(PlaceholderArt.Label))
                AssetDatabase.SetLabels(obj, new[] { PlaceholderArt.Label });
        }
    }
}
