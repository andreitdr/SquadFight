using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Game.Save
{
    /// <summary>Upgrades a save payload (as JSON) from <see cref="From"/> to <see cref="To"/> = From + 1.</summary>
    public interface ISaveMigration
    {
        int From { get; }
        int To { get; }
        void Apply(JObject payload);
    }

    public sealed class MigrationRunner
    {
        private readonly Dictionary<int, ISaveMigration> _byFrom = new Dictionary<int, ISaveMigration>();

        public MigrationRunner(IEnumerable<ISaveMigration> migrations)
        {
            foreach (var m in migrations)
            {
                if (m.To != m.From + 1) throw new ArgumentException($"Migration {m.GetType().Name} must go from N to N+1.");
                if (_byFrom.ContainsKey(m.From)) throw new ArgumentException($"Two migrations start at version {m.From}.");
                _byFrom[m.From] = m;
            }
        }

        /// <summary>All shipped migrations, in order. Append new ones here.</summary>
        public static MigrationRunner Default() => new MigrationRunner(new ISaveMigration[]
        {
            new Migration_1_To_2(),
            new Migration_2_To_3(),
            new Migration_3_To_4(),
        });

        public bool CanMigrate(int from, int to)
        {
            for (var v = from; v < to; v++)
                if (!_byFrom.ContainsKey(v)) return false;
            return true;
        }

        public void Migrate(JObject payload, int from, int to)
        {
            for (var v = from; v < to; v++)
            {
                if (!_byFrom.TryGetValue(v, out var m)) throw new InvalidOperationException($"No save migration from version {v}.");
                m.Apply(payload);
                payload["version"] = m.To;
            }
        }
    }

    /// <summary>v1 → v2: hero field "star" renamed to "stars" (sample migration showing the pattern).</summary>
    public sealed class Migration_1_To_2 : ISaveMigration
    {
        public int From => 1;
        public int To => 2;

        public void Apply(JObject payload)
        {
            if (!(payload["heroes"] is JArray heroes)) return;
            foreach (var h in heroes.OfType<JObject>())
            {
                if (h["star"] == null) continue;
                if (h["stars"] == null) h["stars"] = h["star"];
                h.Remove("star");
            }
        }
    }
}

namespace Game.Save
{
    /// <summary>v2 → v3: adds pity counters (bad-luck protection) starting at zero.</summary>
    public sealed class Migration_2_To_3 : ISaveMigration
    {
        public int From => 2;
        public int To => 3;

        public void Apply(JObject payload)
        {
            if (!(payload["pity"] is JObject)) payload["pity"] = new JObject { ["fightsWithoutShard"] = 0 };
        }
    }
}

namespace Game.Save
{
    /// <summary>v3 → v4: tutorial and daily-bonus records. Existing players (any fight already played) skip the tutorial.</summary>
    public sealed class Migration_3_To_4 : ISaveMigration
    {
        public int From => 3;
        public int To => 4;

        public void Apply(JObject payload)
        {
            var played = payload["progress"]?["totalFights"]?.Type == JTokenType.Integer && (long)payload["progress"]["totalFights"] > 0;
            if (!(payload["tutorial"] is JObject))
                payload["tutorial"] = new JObject { ["step"] = played ? 999 : 0, ["skipped"] = played, ["seenTips"] = new JArray() };
            if (!(payload["daily"] is JObject))
                payload["daily"] = new JObject { ["lastClaimDate"] = "", ["streak"] = 0 };
        }
    }
}
