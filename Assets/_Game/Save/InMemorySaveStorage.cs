using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Save
{
    /// <summary>Storage for tests, with fault injection to simulate crashes and disk errors.</summary>
    public sealed class InMemorySaveStorage : ISaveStorage
    {
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>();

        /// <summary>When set, the next Write stores only this many characters and then throws (simulates a crash mid-write).</summary>
        public int? TruncateNextWriteAt;
        /// <summary>When true, Replace throws before doing anything (simulates a crash after the temp file was written).</summary>
        public bool FailNextReplace;
        /// <summary>When true, every Write throws (disk full / read-only).</summary>
        public bool FailAllWrites;

        public int WriteCount { get; private set; }
        public string Location => "memory";

        /// <summary>Keys whose Read throws (simulates locked files / iOS data protection before first unlock).</summary>
        public readonly HashSet<string> UnreadableKeys = new HashSet<string>();

        public bool Exists(string key) => Files.ContainsKey(key);

        public string Read(string key)
        {
            if (UnreadableKeys.Contains(key) && Files.ContainsKey(key)) throw new System.IO.IOException("simulated read failure");
            return Files.TryGetValue(key, out var v) ? v : null;
        }

        public IEnumerable<string> List(string prefix)
        {
            foreach (var k in new List<string>(Files.Keys)) if (k.StartsWith(prefix, StringComparison.Ordinal)) yield return k;
        }

        public void Write(string key, string content)
        {
            if (FailAllWrites) throw new System.IO.IOException("simulated write failure");
            WriteCount++;
            if (TruncateNextWriteAt.HasValue)
            {
                var n = Math.Min(TruncateNextWriteAt.Value, content.Length);
                TruncateNextWriteAt = null;
                Files[key] = content.Substring(0, n);
                throw new System.IO.IOException("simulated crash during write");
            }
            Files[key] = content;
        }

        public void Replace(string sourceKey, string targetKey, string backupKey)
        {
            if (FailNextReplace)
            {
                FailNextReplace = false;
                throw new System.IO.IOException("simulated crash before replace");
            }
            if (!Files.TryGetValue(sourceKey, out var src)) throw new System.IO.FileNotFoundException(sourceKey);
            if (Files.TryGetValue(targetKey, out var old) && backupKey != null) Files[backupKey] = old;
            Files[targetKey] = src;
            Files.Remove(sourceKey);
        }

        public void Move(string fromKey, string toKey)
        {
            if (!Files.TryGetValue(fromKey, out var v)) throw new System.IO.FileNotFoundException(fromKey);
            Files[toKey] = v;
            Files.Remove(fromKey);
        }

        public void Delete(string key) => Files.Remove(key);
    }
}
