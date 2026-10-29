using System;
using System.Collections.Generic;
using Game.Core;
using Game.Save;

namespace Game.Services
{
    /// <summary>
    /// Shared dependencies of all services plus the mutation protocol:
    /// validate → apply → <see cref="Commit"/> (marks the save dirty) → events.
    /// Inside <see cref="BeginTransaction"/> events and the dirty mark are deferred until commit, and the state is
    /// restored from a snapshot if the transaction is disposed without committing.
    /// </summary>
    public sealed class ServiceContext
    {
        public GameDatabase Db { get; }
        public PlayerState State { get; }
        public EventBus Bus { get; }
        public ISaveScheduler Saver { get; }
        public IRandom Random { get; }
        public IClock Clock { get; }
        public IGameLogger Log { get; }

        private Transaction _tx;

        public ServiceContext(GameDatabase db, PlayerState state, EventBus bus, ISaveScheduler saver, IRandom random, IClock clock, IGameLogger log)
        {
            Db = db;
            State = state;
            Bus = bus;
            Saver = saver ?? NullSaveScheduler.Instance;
            Random = random;
            Clock = clock;
            Log = log ?? NullLogger.Instance;
        }

        public bool InTransaction => _tx != null;

        /// <summary>Publishes now, or at commit when inside a transaction.</summary>
        public void Emit<T>(T evt)
        {
            if (_tx != null) _tx.Pending.Add(() => Bus.Publish(evt));
            else Bus.Publish(evt);
        }

        /// <summary>Marks the save dirty (deferred inside a transaction).</summary>
        public void Commit()
        {
            if (_tx != null) _tx.Dirty = true;
            else Saver.MarkDirty();
        }

        public Transaction BeginTransaction()
        {
            if (_tx != null) throw new InvalidOperationException("Nested transactions are not supported.");
            _tx = new Transaction(this, SaveSerializer.Clone(State.Data));
            return _tx;
        }

        public sealed class Transaction : IDisposable
        {
            private readonly ServiceContext _ctx;
            private readonly SaveData _snapshot;
            private bool _done;
            internal readonly List<Action> Pending = new List<Action>();
            internal bool Dirty;

            internal Transaction(ServiceContext ctx, SaveData snapshot)
            {
                _ctx = ctx;
                _snapshot = snapshot;
            }

            /// <summary>Keeps the changes: marks dirty, optionally saves immediately, then publishes queued events.</summary>
            public void Complete(bool saveNow = false)
            {
                if (_done) return;
                _done = true;
                _ctx._tx = null;
                if (Dirty)
                {
                    _ctx.Saver.MarkDirty();
                    if (saveNow) _ctx.Saver.SaveNow();
                }
                foreach (var a in Pending) a();
            }

            /// <summary>Rolls back unless <see cref="Complete"/> was called.</summary>
            public void Dispose()
            {
                if (_done) return;
                _done = true;
                _ctx._tx = null;
                _ctx.State.Replace(_snapshot);
                _ctx.Log.Warning("[Services] Transaction rolled back.");
            }
        }
    }

    public sealed class NullSaveScheduler : ISaveScheduler
    {
        public static readonly NullSaveScheduler Instance = new NullSaveScheduler();
        public int DirtyMarks { get; private set; }
        public void MarkDirty() => DirtyMarks++;
        public bool SaveNow() => true;
    }
}
