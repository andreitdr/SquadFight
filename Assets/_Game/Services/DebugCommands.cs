using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Game.Core;

namespace Game.Services
{
    /// <summary>Text commands for the dev console (and tests). Pure: parses a line and calls services.</summary>
    public sealed class DebugCommands
    {
        private readonly GameSession _s;
        private readonly Dictionary<string, Func<string[], string>> _commands = new Dictionary<string, Func<string[], string>>(StringComparer.OrdinalIgnoreCase);

        public DebugCommands(GameSession session)
        {
            _s = session;
            Register("help", _ => "Commands: " + string.Join(", ", _commands.Keys.OrderBy(k => k)));
            Register("give", Give);
            Register("list", List);
            Register("party", _ => string.Join(" | ", _s.Party.GetSlots().Select(h => h ?? "-")));
            Register("reset", _ => { _s.ResetToNewGame(); return "Progress reset."; });
        }

        public void Register(string name, Func<string[], string> handler) => _commands[name] = handler;

        public IEnumerable<string> CommandNames => _commands.Keys;

        public string Execute(string line)
        {
            var parts = (line ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "";
            if (!_commands.TryGetValue(parts[0], out var cmd)) return $"Unknown command '{parts[0]}'. Try 'help'.";
            try { return cmd(parts.Skip(1).ToArray()); }
            catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException || e is ArgumentException || e is KeyNotFoundException)
            {
                return "Error: " + e.Message;
            }
        }

        public static int Int(string s) => int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);

        private string Give(string[] a)
        {
            if (a.Length < 2) return "Usage: give <currency> <amount> | give shards <heroId> <n> | give item <templateId> [rarity]";
            switch (a[0].ToLowerInvariant())
            {
                default:
                    return Report(_s.Currency.Add(a[0], long.Parse(a[1], CultureInfo.InvariantCulture)), $"{a[0]} = {_s.Currency.Get(a[0])}");
            }
        }

        private string List(string[] a)
        {
            var what = a.Length > 0 ? a[0].ToLowerInvariant() : "heroes";
            var sb = new StringBuilder();
            switch (what)
            {
                case "heroes":
                    foreach (var h in _s.Roster.AllHeroes)
                    {
                        var i = _s.Roster.GetInfo(h.Id);
                        sb.AppendLine($"{h.Id} [{h.Rarity}] {(i.Unlocked ? "★" + i.Star : "locked")} shards {i.Shards}");
                    }
                    break;
                case "items":
                    foreach (var i in _s.Inventory.All()) sb.AppendLine($"{i.InstanceId.Substring(0, 8)} {i.Template.Id} [{i.Rarity}] {(i.IsEquipped ? "on " + i.EquippedBy : "")}");
                    break;
                case "currencies":
                    foreach (var c in _s.Db.Rules.Currencies) sb.AppendLine($"{c.Id} {_s.Currency.Get(c.Id)}");
                    break;
                default: return "Usage: list heroes|items|currencies";
            }
            return sb.ToString().TrimEnd();
        }

        public static string Report(Result r, string ok) => r.Ok ? ok : "Failed: " + r;
    }
}
