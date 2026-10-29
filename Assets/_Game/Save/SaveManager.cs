using System;
using Game.Core;

namespace Game.Save
{
    /// <summary>Anything that can request a (debounced) save after mutating player state.</summary>
    public interface ISaveScheduler
    {
        void MarkDirty();
        /// <summary>Saves immediately (used before showing rewards so a kill cannot duplicate or lose them).</summary>
        bool SaveNow();
    }

    /// <summary>
    /// Owns loading and saving. Mutations call <see cref="MarkDirty"/>; <see cref="Tick"/> (called every frame by the
    /// Unity lifecycle hook) writes at most <see cref="DebounceSeconds"/> after the first unsaved change, so a kill loses
    /// at most ~1 s. Pause/quit hooks call <see cref="Flush"/>. IO errors never throw to callers.
    /// </summary>
    public sealed class SaveManager : ISaveScheduler
    {
        private readonly SaveStore _store;
        private readonly IClock _clock;
        private readonly IGameLogger _log;
        private Func<SaveData> _export;
        private bool _loaded;
        private bool _dirty;
        private double _dirtySince;

        public double DebounceSeconds { get; set; } = 1.0;
        public bool IsDirty => _dirty;
        /// <summary>True when the disk holds a save this build must not overwrite (newer version, unreadable): writes are refused.</summary>
        public bool ReadOnly { get; private set; }
        public int SuccessfulSaves { get; private set; }
        public string LastError { get; private set; }
        public LoadResult LastLoad { get; private set; }

        public event Action Saved;
        public event Action<string> SaveFailed;

        public SaveManager(SaveStore store, IClock clock, IGameLogger log = null)
        {
            _store = store;
            _clock = clock;
            _log = log ?? NullLogger.Instance;
        }

        public SaveStore Store => _store;

        /// <summary>Loads (or creates) the save. <paramref name="newGame"/> builds a fresh save when none is usable.</summary>
        public LoadResult Load(Func<SaveData> newGame)
        {
            var result = _store.Load(newGame);
            LastLoad = result;
            _loaded = true;
            ReadOnly = result.ReadOnly;
            foreach (var p in result.Problems) _log.Warning("[Save] " + p);
            if (ReadOnly) _log.Error("[Save] The existing save cannot be read safely by this version; progress will not be saved this session.");
            // A recovered or brand-new save is written right away so main is healthy again.
            if (!ReadOnly && (result.Source != LoadSource.Main || result.Problems.Count > 0))
            {
                _dirty = true;
                _dirtySince = _clock.RealtimeSeconds - DebounceSeconds;
            }
            return result;
        }

        /// <summary>Supplies the current state to persist (normally the live PlayerState data).</summary>
        public void Bind(Func<SaveData> export) => _export = export;

        public void MarkDirty()
        {
            if (_dirty) return;
            _dirty = true;
            _dirtySince = _clock.RealtimeSeconds;
        }

        public void Tick()
        {
            if (_dirty && _clock.RealtimeSeconds - _dirtySince >= DebounceSeconds) SaveNow();
        }

        public void Flush()
        {
            if (IsDirty) SaveNow();
        }

        public bool SaveNow()
        {
            // Never write before Load decided what is on disk (would overwrite the real save with defaults).
            if (!_loaded || ReadOnly || _export == null) return false;
            try
            {
                var data = _export();
                _store.Write(data);
                _dirty = false;
                SuccessfulSaves++;
                LastError = null;
                Raise(Saved);
                return true;
            }
            catch (Exception e)
            {
                // Stay dirty; the next Tick retries after another debounce interval.
                _dirty = true;
                _dirtySince = _clock.RealtimeSeconds;
                LastError = e.Message;
                _log.Error("[Save] Write failed: " + e.Message);
                try { SaveFailed?.Invoke(e.Message); }
                catch (Exception handlerError) { _log.Error("[Save] SaveFailed handler threw: " + handlerError.Message); }
                return false;
            }
        }

        private void Raise(Action evt)
        {
            try { evt?.Invoke(); }
            catch (Exception e) { _log.Error("[Save] Saved handler threw: " + e.Message); }
        }
    }
}
