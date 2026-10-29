using System.IO;
using Game.Save;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Editor helpers for the save file in Application.persistentDataPath.</summary>
    public static class SaveDebugMenu
    {
        private static string MainPath => Path.Combine(Application.persistentDataPath, SaveStore.MainKey);

        [MenuItem("Tools/Game/Save/Open Save Folder")]
        public static void OpenFolder()
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            EditorUtility.RevealInFinder(Application.persistentDataPath + "/");
        }

        [MenuItem("Tools/Game/Save/Delete Save")]
        public static void DeleteSave()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Delete save", "Stop Play mode first (the running game would write the save again).", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Delete save", "Delete save.json, save.bak and save.tmp?", "Delete", "Cancel")) return;
            foreach (var key in new[] { SaveStore.MainKey, SaveStore.BackupKey, SaveStore.TempKey })
            {
                var p = Path.Combine(Application.persistentDataPath, key);
                if (File.Exists(p)) File.Delete(p);
            }
            Debug.Log("[Save] Deleted save files in " + Application.persistentDataPath);
        }

        [MenuItem("Tools/Game/Save/Corrupt Save (test)")]
        public static void CorruptSave()
        {
            if (!File.Exists(MainPath))
            {
                EditorUtility.DisplayDialog("Corrupt save", "No save.json yet.", "OK");
                return;
            }
            var text = File.ReadAllText(MainPath);
            File.WriteAllText(MainPath, text.Substring(0, text.Length / 2));
            Debug.Log("[Save] save.json truncated; next load should recover from save.bak.");
        }
    }
}
