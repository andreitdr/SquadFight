using System.Collections.Generic;
using Game.Core;

namespace Game.Services
{
    /// <summary>What applying a reward bundle changed, with highlights for the UI.</summary>
    public sealed class RewardResult
    {
        public RewardBundle Bundle;
        public readonly List<string> NowUnlockable = new List<string>();
        public readonly List<string> StarUpAvailable = new List<string>();
        public readonly List<string> BetterItems = new List<string>();
        /// <summary>Shards that overflowed past max star and were converted, per hero.</summary>
        public readonly Dictionary<string, int> ConvertedShards = new Dictionary<string, int>();
        /// <summary>Items converted to currency because the inventory was full.</summary>
        public readonly List<string> AutoSold = new List<string>();
        public readonly Dictionary<string, (int before, int after)> ShardProgress = new Dictionary<string, (int, int)>();
        public bool IsEmpty => Bundle == null || Bundle.IsEmpty;
    }
}
