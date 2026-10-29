#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace Game.EditorTools
{
    /// <summary>
    /// Offline guarantee: removes INTERNET / ACCESS_NETWORK_STATE from the final manifest even if a dependency requests
    /// them (tools:node="remove" in the launcher manifest wins during the Gradle merge).
    /// </summary>
    public sealed class AndroidManifestPostProcessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;
        public static readonly string[] Removed = { "android.permission.INTERNET", "android.permission.ACCESS_NETWORK_STATE" };

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var launcher = Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml");
            var manifest = File.Exists(launcher) ? launcher : Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest)) return;
            var doc = new XmlDocument();
            doc.Load(manifest);
            const string android = "http://schemas.android.com/apk/res/android";
            const string tools = "http://schemas.android.com/tools";
            var root = doc.DocumentElement;
            if (root.GetAttribute("xmlns:tools") == "") root.SetAttribute("xmlns:tools", tools);
            foreach (var perm in Removed)
            {
                var el = doc.CreateElement("uses-permission");
                el.SetAttribute("name", android, perm);
                el.SetAttribute("node", tools, "remove");
                root.AppendChild(el);
            }
            doc.Save(manifest);
        }
    }
}
#endif
