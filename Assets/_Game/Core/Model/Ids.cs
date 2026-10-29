using System.Text.RegularExpressions;

namespace Game.Core
{
    /// <summary>Stable string IDs: lowercase snake_case, starting with a letter (e.g. <c>hero_knight_01</c>).</summary>
    public static class Ids
    {
        private static readonly Regex Pattern = new Regex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsValid(string id) => !string.IsNullOrEmpty(id) && id.Length <= 64 && Pattern.IsMatch(id);

        public static string ShardCurrency(string heroId) => "shard_" + heroId;
    }
}
