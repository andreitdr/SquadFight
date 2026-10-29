using System;
using System.IO;
using System.Text;
using Game.Core;

namespace Game.Save
{
    /// <summary>Durable file storage in one folder (e.g. Application.persistentDataPath). Pure System.IO.</summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string _root;

        public FileSaveStorage(string rootFolder)
        {
            _root = rootFolder ?? throw new ArgumentNullException(nameof(rootFolder));
            Directory.CreateDirectory(_root);
        }

        public string Location => _root;

        private string P(string key)
        {
            if (string.IsNullOrEmpty(key) || key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || key.Contains(".."))
                throw new ArgumentException($"Invalid storage key '{key}'.");
            return Path.Combine(_root, key);
        }

        public bool Exists(string key) => File.Exists(P(key));

        public string Read(string key)
        {
            var path = P(key);
            if (!File.Exists(path)) return null;
            return File.ReadAllText(path, Encoding.UTF8); // read errors propagate: they are not corruption
        }

        public System.Collections.Generic.IEnumerable<string> List(string prefix)
        {
            foreach (var f in Directory.GetFiles(_root, prefix + "*")) yield return Path.GetFileName(f);
        }

        public void Write(string key, string content)
        {
            var bytes = new UTF8Encoding(false).GetBytes(content);
            using (var fs = new FileStream(P(key), FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }
        }

        public void Replace(string sourceKey, string targetKey, string backupKey)
        {
            var src = P(sourceKey);
            var dst = P(targetKey);
            var bak = backupKey == null ? null : P(backupKey);
            if (!File.Exists(dst))
            {
                File.Move(src, dst);
                return;
            }
            try
            {
                // On Mono/Unix this is two renames (dst→bak, src→dst); a crash in between leaves tmp + bak, which the loader recovers.
                File.Replace(src, dst, bak, true);
            }
            catch (PlatformNotSupportedException) { ManualReplace(src, dst, bak); }
            catch (IOException) when (File.Exists(src) && File.Exists(dst)) { ManualReplace(src, dst, bak); }
        }

        /// <summary>Fallback when File.Replace is unavailable. A crash between steps leaves tmp + bak, which the loader recovers.</summary>
        private static void ManualReplace(string src, string dst, string bak)
        {
            if (bak != null)
            {
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(dst, bak);
            }
            else File.Delete(dst);
            File.Move(src, dst);
        }

        public void Move(string fromKey, string toKey)
        {
            var to = P(toKey);
            if (File.Exists(to)) File.Delete(to);
            File.Move(P(fromKey), to);
        }

        public void Delete(string key)
        {
            var p = P(key);
            if (File.Exists(p)) File.Delete(p);
        }
    }
}
