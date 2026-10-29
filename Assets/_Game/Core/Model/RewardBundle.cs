using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>Concrete rewards (already rolled) to be applied atomically.</summary>
    public sealed class RewardBundle
    {
        public readonly Dictionary<string, long> Currencies = new Dictionary<string, long>();
        public readonly Dictionary<string, int> Shards = new Dictionary<string, int>();
        public readonly List<EquipmentInstance> Items = new List<EquipmentInstance>();

        public bool IsEmpty => Currencies.Values.All(v => v == 0) && Shards.Values.All(v => v == 0) && Items.Count == 0;

        public void AddCurrency(string id, long amount)
        {
            if (amount == 0) return;
            Currencies.TryGetValue(id, out var v);
            Currencies[id] = v + amount;
        }

        public void AddShards(string heroId, int amount)
        {
            if (amount == 0) return;
            Shards.TryGetValue(heroId, out var v);
            Shards[heroId] = v + amount;
        }

        public void Merge(RewardBundle other)
        {
            if (other == null) return;
            foreach (var kv in other.Currencies) AddCurrency(kv.Key, kv.Value);
            foreach (var kv in other.Shards) AddShards(kv.Key, kv.Value);
            Items.AddRange(other.Items);
        }

        public override string ToString() =>
            string.Join(", ", Currencies.Select(c => $"{c.Value} {c.Key}")
                .Concat(Shards.Select(s => $"{s.Value} shard(s) {s.Key}"))
                .Concat(Items.Select(i => i.ToString())));
    }
}
