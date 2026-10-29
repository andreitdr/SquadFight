namespace Game.Core
{
    /// <summary>Key/value text storage used by the save system. Keys are plain file names (e.g. "save.json").</summary>
    public interface ISaveStorage
    {
        bool Exists(string key);

        /// <summary>Returns null when the key does not exist or cannot be read.</summary>
        string Read(string key);

        /// <summary>Writes the full content durably (flushed to disk) under the key, replacing any previous value.</summary>
        void Write(string key, string content);

        /// <summary>Atomically replaces <paramref name="targetKey"/> with <paramref name="sourceKey"/>; the previous target becomes <paramref name="backupKey"/> (if not null).</summary>
        void Replace(string sourceKey, string targetKey, string backupKey);

        /// <summary>Renames a key (overwrites the destination).</summary>
        void Move(string fromKey, string toKey);

        void Delete(string key);

        /// <summary>Human-readable location for debugging (a folder path or "memory").</summary>
        string Location { get; }
    }
}
