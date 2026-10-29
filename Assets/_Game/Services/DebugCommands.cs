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
            Register("unlock", a => a.Length < 1 ? "Usage: unlock <heroId> [force]" : Unlock(a));
            Register("starup", a => a.Length < 1 ? "Usage: starup <heroId>" : Report(_s.Roster.UpgradeStar(a[0]), $"{a[0]} is now ★{_s.Roster.GetStar(a[0])}"));
            Register("fight", a => a.Length < 1 ? "Usage: fight <fightId> [seed]" : Fight(a));
            Register("clear", Clear);
            Register("soak", a => Soak(a.Length > 0 ? Int(a[0]) : 100));
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
                case "shards":
                    if (a.Length < 3) return "Usage: give shards <heroId> <n>";
                    return Report(_s.Roster.AddShards(a[1], Int(a[2])), $"+{a[2]} shards {a[1]} (now {_s.Roster.GetShards(a[1])})");
                case "item":
                    var template = _s.Db.GetEquipment(a[1]);
                    var rarity = a.Length > 2 ? (Rarity)Enum.Parse(typeof(Rarity), a[2], true) : template.Rarity;
                    var item = EquipmentFactory.Create(template, rarity, _s.Random, _s.Db);
                    return Report(_s.Inventory.AddItem(item), $"+{item}");
                default:
                    return Report(_s.Currency.Add(a[0], long.Parse(a[1], CultureInfo.InvariantCulture)), $"{a[0]} = {_s.Currency.Get(a[0])}");
            }
        }

        /// <summary>"unlock hero" uses normal rules; "unlock hero force" grants the missing shards first.</summary>
        private string Unlock(string[] a)
        {
            var id = a[0];
            if (a.Length > 1 && a[1] == "force" && !_s.Roster.IsUnlocked(id))
            {
                var info = _s.Roster.GetUnlockInfo(id);
                if (info.Have < info.Need) _s.Roster.AddShards(id, info.Need - info.Have);
            }
            return Report(_s.Roster.UnlockHero(id), $"{id} unlocked");
        }

        private string Fight(string[] a)
        {
            var r = _s.Battles.RunInstant(a[0], a.Length > 1 ? (ulong?)ulong.Parse(a[1], CultureInfo.InvariantCulture) : null);
            if (!r.Ok) return "Failed: " + r;
            var c = r.Value;
            return $"{c.FightId}: {(c.Victory ? "Victory ★" + c.Stars : "Defeat")} in {c.Battle.Rounds} rounds. Rewards: {(c.Rewards?.Bundle?.ToString() ?? "none")}";
        }

        /// <summary>"clear fight id" | "clear chapter id" | "clear all": records 3-star victories (with normal rewards) in campaign order.</summary>
        private string Clear(string[] a)
        {
            if (a.Length < 1) return "Usage: clear fight <id> | clear chapter <id> | clear all";
            var ids = new List<string>();
            switch (a[0].ToLowerInvariant())
            {
                case "fight": if (a.Length < 2) return "Usage: clear fight <id>"; ids.Add(a[1]); break;
                case "chapter": if (a.Length < 2) return "Usage: clear chapter <id>"; ids.AddRange(_s.Db.GetChapter(a[1]).FightIds); break;
                case "all": foreach (var c in _s.Db.Chapters) ids.AddRange(c.FightIds); break;
                default: return "Usage: clear fight <id> | clear chapter <id> | clear all";
            }
            var cleared = 0;
            foreach (var id in ids)
            {
                var win = new BattleResult { Outcome = BattleOutcome.Victory, Rounds = 1 };
                var r = _s.Progress.CompleteFight(id, win);
                if (!r.Ok) return $"Cleared {cleared}; stopped at {id}: {r}";
                cleared++;
            }
            return $"Cleared {cleared} fight(s).";
        }

        /// <summary>Plays N instant fights (farming the last cleared fight after a loss) and reports errors — QA soak test.</summary>
        private string Soak(int n)
        {
            var policy = new CasualPolicy();
            string lastCleared = null;
            var farm = false;
            int wins = 0, losses = 0, errors = 0;
            for (var i = 0; i < n; i++)
            {
                var next = _s.Progress.GetNextRecommendedFight();
                var target = farm && lastCleared != null ? lastCleared : next;
                var r = _s.Battles.RunInstant(target);
                if (!r.Ok) { errors++; continue; }
                if (r.Value.Victory) { wins++; if (target == next) lastCleared = next; farm = false; }
                else { losses++; farm = true; }
                policy.Manage(_s);
            }
            return $"Soak {n}: {wins} wins, {losses} losses, {errors} errors, {_s.Progress.TotalClearedFights}/{_s.Db.Fights.Count} cleared, power {_s.PartyPower}";
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
