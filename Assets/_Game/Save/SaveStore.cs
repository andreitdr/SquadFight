using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Save
{
    public enum LoadSource { NewGame, Main, Temp, Backup }

    public sealed class LoadResult
    {
        public SaveData Data;
        public LoadSource Source;
        /// <summary>
        /// A save exists that this build cannot read safely (newer version, unreadable format, IO error):
        /// saving is disabled so it is never overwritten.
        /// </summary>
        public bool ReadOnly;
        public bool FutureVersion;
        public readonly List<string> Problems = new List<string>();
        public bool Recovered => Source == LoadSource.Backup || Source == LoadSource.Temp || Problems.Count > 0;
    }

    /// <summary>
    /// File protocol on top of <see cref="ISaveStorage"/>:
    /// write <c>save.tmp</c> (flushed) → replace <c>save.json</c>, keeping the previous one as <c>save.bak</c>.
    /// Load picks the intact file with the highest save counter among main/tmp, falling back to bak.
    /// Only damaged files (bad JSON/checksum) are moved aside as <c>save.corrupt.*</c>; intact files this build cannot
    /// read, and files that cannot be read at all, make the session read-only instead.
    /// </summary>
    public sealed class SaveStore
    {
        public const string MainKey = "save.json";
        public const string TempKey = "save.tmp";
        public const string BackupKey = "save.bak";
        public const string CorruptPrefix = "save.corrupt.";
        public const int MaxCorruptCopies = 5;

        private readonly ISaveStorage _storage;
        private readonly SaveSerializer _serializer;
        private readonly IClock _clock;

        public long Counter { get; private set; }
        public ISaveStorage Storage => _storage;

        public SaveStore(ISaveStorage storage, IClock clock, SaveSerializer serializer = null)
        {
            _storage = storage;
            _clock = clock;
            _serializer = serializer ?? new SaveSerializer();
        }

        public void Write(SaveData data)
        {
            var next = Counter + 1;
            var now = _clock.UtcNow;
            data.LastSavedUtc = now;
            var text = _serializer.Encode(data, next, now);
            _storage.Write(TempKey, text);
            _storage.Replace(TempKey, MainKey, BackupKey);
            Counter = next;
        }

        private struct Candidate
        {
            public LoadSource Source;
            public string Key;
            public DecodeResult Decoded;
            public string ReadError;
            public bool Ok => ReadError == null && Decoded.Status == DecodeStatus.Ok;
            public bool Blocking => ReadError != null || Decoded.Status == DecodeStatus.FutureVersion || Decoded.Status == DecodeStatus.Incompatible;
        }

        public LoadResult Load(Func<SaveData> newGame)
        {
            var result = new LoadResult();
            var candidates = new List<Candidate>();
            foreach (var (source, key) in new[] { (LoadSource.Main, MainKey), (LoadSource.Temp, TempKey), (LoadSource.Backup, BackupKey) })
            {
                bool exists;
                try { exists = _storage.Exists(key); }
                catch (Exception e) { candidates.Add(new Candidate { Source = source, Key = key, ReadError = e.Message }); continue; }
                if (!exists) continue;
                try
                {
                    var text = _storage.Read(key);
                    if (text == null) continue; // vanished between Exists and Read
                    candidates.Add(new Candidate { Source = source, Key = key, Decoded = _serializer.Decode(text) });
                }
                catch (Exception e)
                {
                    candidates.Add(new Candidate { Source = source, Key = key, ReadError = e.Message });
                }
            }

            // Newest intact main/temp wins (tmp is newer only if the app died between writing it and the replace).
            var ok = candidates.Where(c => c.Ok).ToList();
            var primary = ok.Where(c => c.Source != LoadSource.Backup).OrderByDescending(c => c.Decoded.Counter).ThenBy(c => c.Source).ToList();
            Candidate? chosen = primary.Count > 0 ? primary[0] : ok.Count > 0 ? ok[0] : (Candidate?)null;

            foreach (var c in candidates.Where(c => c.Blocking))
            {
                // A read error has an unknown counter: assume it may be the newest. Otherwise block only if newer than what we load.
                var blocks = c.ReadError != null || chosen == null || c.Decoded.Counter >= chosen.Value.Decoded.Counter;
                result.Problems.Add($"{c.Key}: {c.ReadError ?? c.Decoded.Error}");
                if (!blocks) continue;
                result.ReadOnly = true;
                if (c.Decoded.Status == DecodeStatus.FutureVersion) result.FutureVersion = true;
            }

            // Only genuinely damaged files are moved aside, and never while another file blocks writing.
            foreach (var c in candidates.Where(c => c.ReadError == null && c.Decoded.Status == DecodeStatus.Corrupt))
            {
                result.Problems.Add($"{c.Key}: {c.Decoded.Error}");
                if (result.ReadOnly) continue;
                if (c.Source == LoadSource.Temp) TryDelete(c.Key); // partial write, expected after a crash
                else Quarantine(c.Key);
            }

            Counter = candidates.Where(c => c.ReadError == null && c.Decoded.Status != DecodeStatus.Corrupt).Select(c => c.Decoded.Counter).DefaultIfEmpty(0).Max();

            if (chosen != null)
            {
                var c = chosen.Value;
                result.Data = c.Decoded.Data;
                result.Source = c.Source;
                if (c.Source != LoadSource.Main) result.Problems.Add($"recovered from {c.Key}");
                // tmp may be the only copy of the newest save: promote it before the next write truncates it.
                if (c.Source == LoadSource.Temp && !result.ReadOnly)
                {
                    try { _storage.Replace(TempKey, MainKey, BackupKey); }
                    catch (Exception e) { result.Problems.Add("could not promote save.tmp: " + e.Message); }
                }
                return result;
            }

            result.Data = newGame();
            result.Source = LoadSource.NewGame;
            return result;
        }

        private void Quarantine(string key)
        {
            try
            {
                var name = $"{CorruptPrefix}{_clock.UtcNow:yyyyMMddHHmmss}.{Guid.NewGuid().ToString("N").Substring(0, 8)}.{key}";
                _storage.Move(key, name);
                var copies = _storage.List(CorruptPrefix).OrderBy(k => k, StringComparer.Ordinal).ToList();
                for (var i = 0; i < copies.Count - MaxCorruptCopies; i++) TryDelete(copies[i]);
            }
            catch (Exception) { /* leaving the corrupt file in place is acceptable; it is never loaded */ }
        }

        private void TryDelete(string key)
        {
            try { _storage.Delete(key); }
            catch (Exception) { }
        }

        public void DeleteAll()
        {
            TryDelete(MainKey);
            TryDelete(TempKey);
            TryDelete(BackupKey);
            Counter = 0;
        }
    }
}
