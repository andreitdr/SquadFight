using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Save;

namespace Game.Services
{
    /// <summary>Decides what a simulated player does between fights.</summary>
    public interface IPlayerPolicy
    {
        string Name { get; }
        /// <summary>Spend resources: unlock, star-up, equip, rebuild the party.</summary>
        void Manage(GameSession session);
        /// <summary>Extra clears of a fight right after first beating it (farming habit).</summary>
        int ReplaysAfterFirstClear { get; }
    }

    /// <summary>Unlocks/stars up whenever possible, equips the best items, fields the strongest party.</summary>
    public class GreedyPolicy : IPlayerPolicy
    {
        public virtual string Name => "greedy";
        public virtual int ReplaysAfterFirstClear => 0;

        public virtual void Manage(GameSession s)
        {
            foreach (var h in s.Db.Heroes)
                if (!s.Roster.IsUnlocked(h.Id) && s.Roster.GetUnlockInfo(h.Id).CanUnlock) s.Roster.UnlockHero(h.Id);

            // Star-ups: strongest-value first (party members first), repeat while affordable.
            bool upgraded;
            do
            {
                upgraded = false;
                foreach (var id in s.Roster.UnlockedHeroIds.OrderByDescending(id => s.Party.Contains(id)).ThenByDescending(s.Roster.GetPower).ToList())
                    if (s.Roster.GetStarUpInfo(id).CanUpgrade && s.Roster.UpgradeStar(id).Ok) upgraded = true;
            } while (upgraded);

            // Party = 5 strongest heroes.
            var best = s.Roster.UnlockedHeroIds.OrderByDescending(s.Roster.GetPower).ThenBy(id => id, StringComparer.Ordinal).Take(s.Party.Size).ToList();
            for (var i = 0; i < s.Party.Size; i++) s.Party.Assign(i, i < best.Count ? best[i] : null);

            // Equip: for each party hero and slot, take the bag item with the largest power gain.
            var layout = s.Db.Rules.SlotLayout;
            foreach (var hero in s.Party.Members)
                for (var slot = 0; slot < layout.Count; slot++)
                {
                    var bestItem = s.Inventory.Query(layout[slot], includeEquipped: false)
                        .Select(i => (i.InstanceId, delta: s.Inventory.PowerDelta(hero, i.InstanceId, slot)))
                        .Where(x => x.delta > 0).OrderByDescending(x => x.delta).FirstOrDefault();
                    if (bestItem.InstanceId != null) s.Inventory.Equip(hero, bestItem.InstanceId, slot);
                }

            // Sell surplus low-rarity items that are not upgrades for anyone.
            if (s.Db.Rules.SellEnabled)
                foreach (var item in s.Inventory.Query(includeEquipped: false).Where(i => i.Rarity <= Rarity.Uncommon).ToList())
                    if (!s.Inventory.IsUpgradeForParty(item.InstanceId, s.Roster.UnlockedHeroIds)) s.Inventory.SellItem(item.InstanceId);
        }
    }

    public sealed class CasualPolicy : GreedyPolicy
    {
        public override string Name => "casual";
        public override int ReplaysAfterFirstClear => 2;
    }

    public sealed class HardcorePolicy : GreedyPolicy
    {
        public override string Name => "hardcore";
        public override int ReplaysAfterFirstClear => 10;
    }

    public sealed class FightStats
    {
        public string FightId;
        public int Attempts;
        public int Wins;
        public int FirstClearAtFight;
        public int PartyPowerAtFirstClear;
        public int PartyPowerAtFirstAttempt;
        public int RecommendedPower;
        public double WinRate => Attempts == 0 ? 0 : (double)Wins / Attempts;
    }

    public sealed class CampaignReport
    {
        public string Policy;
        public ulong Seed;
        public bool Completed;
        public int TotalFights;
        public readonly List<FightStats> Fights = new List<FightStats>();
        public readonly Dictionary<string, int> FightsPerChapter = new Dictionary<string, int>();
        /// <summary>Fight count at which each hero was unlocked (starting heroes: 0).</summary>
        public readonly Dictionary<string, int> HeroUnlockedAtFight = new Dictionary<string, int>();
        public readonly Dictionary<string, int> ShardsEarned = new Dictionary<string, int>();
        public readonly Dictionary<Rarity, int> ItemsEarned = new Dictionary<Rarity, int>();
        public long GoldEarned;
        public long GoldSpent;
        public readonly List<string> Walls = new List<string>();
        public string StuckAt;

        public string ToMarkdown(Func<string, string> name = null)
        {
            name ??= id => id;
            var sb = new StringBuilder();
            sb.AppendLine($"## Campaign simulation — policy `{Policy}`, seed {Seed}");
            sb.AppendLine();
            sb.AppendLine(Completed ? $"Completed the campaign in **{TotalFights}** fights." : $"**Did not finish** (stuck at `{StuckAt}` after {TotalFights} fights).");
            sb.AppendLine();
            sb.AppendLine("| Chapter | Fights played |");
            sb.AppendLine("|---|---|");
            foreach (var kv in FightsPerChapter) sb.AppendLine($"| {kv.Key} | {kv.Value} |");
            sb.AppendLine();
            sb.AppendLine("| Fight | Attempts | Wins | Win rate | Party power (1st try → clear) | Recommended | First clear at fight # |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var f in Fights)
                sb.AppendLine($"| {f.FightId} | {f.Attempts} | {f.Wins} | {f.WinRate:P0} | {f.PartyPowerAtFirstAttempt} → {f.PartyPowerAtFirstClear} | {f.RecommendedPower} | {f.FirstClearAtFight} |");
            sb.AppendLine();
            sb.AppendLine("| Hero | Unlocked at fight # | Shards earned |");
            sb.AppendLine("|---|---|---|");
            foreach (var kv in HeroUnlockedAtFight.OrderBy(k => k.Value))
                sb.AppendLine($"| {name(kv.Key)} | {kv.Value} | {(ShardsEarned.TryGetValue(kv.Key, out var n) ? n : 0)} |");
            foreach (var kv in ShardsEarned.Where(k => !HeroUnlockedAtFight.ContainsKey(k.Key)))
                sb.AppendLine($"| {name(kv.Key)} | locked | {kv.Value} |");
            sb.AppendLine();
            sb.AppendLine($"Gold earned {GoldEarned}, spent {GoldSpent}. Items earned: {string.Join(", ", ItemsEarned.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}"))}.");
            sb.AppendLine();
            sb.AppendLine(Walls.Count == 0 ? "No difficulty walls (every first attempt series had win rate ≥ 50%)." : "**Walls (win rate < 50%):** " + string.Join(", ", Walls));
            return sb.ToString();
        }
    }

    /// <summary>
    /// Plays the whole campaign headlessly with the real services and battle simulator, following a policy.
    /// Strategy: try the next uncleared fight; on a loss, farm the newest cleared fight once and retry.
    /// </summary>
    public static class CampaignSimulator
    {
        public static CampaignReport Run(GameDatabase db, IPlayerPolicy policy, ulong seed, int maxFights = 5000)
        {
            var clock = new ManualClock();
            var session = new GameSession(db, NewGameFactory.Create(db, clock), null, new SeededRandom(seed), clock);
            var report = new CampaignReport { Policy = policy.Name, Seed = seed };
            var stats = db.Fights.ToDictionary(f => f.Id, f => new FightStats { FightId = f.Id, RecommendedPower = f.RecommendedPower });
            foreach (var h in db.Heroes) if (session.Roster.IsUnlocked(h.Id)) report.HeroUnlockedAtFight[h.Id] = 0;
            foreach (var c in db.Chapters) report.FightsPerChapter[c.Id] = 0;
            session.Bus.Subscribe<ShardsChanged>(e => { if (e.New > e.Old) { report.ShardsEarned.TryGetValue(e.HeroId, out var v); report.ShardsEarned[e.HeroId] = v + (e.New - e.Old); } });
            session.Bus.Subscribe<HeroUnlocked>(e => report.HeroUnlockedAtFight[e.HeroId] = report.TotalFights);
            session.Bus.Subscribe<CurrencyChanged>(e => { if (e.New > e.Old) report.GoldEarned += e.New - e.Old; else report.GoldSpent += e.Old - e.New; });
            session.Bus.Subscribe<ItemAdded>(e => { var r = session.Inventory.Get(e.InstanceId)?.Rarity ?? Rarity.Common; report.ItemsEarned.TryGetValue(r, out var v); report.ItemsEarned[r] = v + 1; });

            string lastCleared = null;
            var farmNext = false;
            policy.Manage(session);
            while (report.TotalFights < maxFights)
            {
                var next = db.Fights.Select(f => f.Id).OrderBy(db.GetFightOrder).FirstOrDefault(id => !session.Progress.IsFightCleared(id));
                if (next == null) { report.Completed = true; break; }
                // After a loss, farm the newest cleared fight once, then retry.
                var target = farmNext && lastCleared != null ? lastCleared : next;
                farmNext = false;
                var fs = stats[target];
                var power = session.PartyPower;
                if (fs.Attempts == 0) fs.PartyPowerAtFirstAttempt = power;
                var r = session.Battles.RunInstant(target);
                if (!r.Ok) { report.StuckAt = target + ": " + r; break; }
                report.TotalFights++;
                report.FightsPerChapter[db.GetFight(target).ChapterId]++;
                fs.Attempts++;
                if (r.Value.Victory)
                {
                    fs.Wins++;
                    if (target == next)
                    {
                        fs.FirstClearAtFight = report.TotalFights;
                        fs.PartyPowerAtFirstClear = power;
                        lastCleared = target;
                        policy.Manage(session);
                        for (var i = 0; i < policy.ReplaysAfterFirstClear && report.TotalFights < maxFights; i++)
                        {
                            var rr = session.Battles.RunInstant(target);
                            report.TotalFights++;
                            report.FightsPerChapter[db.GetFight(target).ChapterId]++;
                            fs.Attempts++;
                            if (rr.Ok && rr.Value.Victory) fs.Wins++;
                            policy.Manage(session);
                        }
                    }
                    else policy.Manage(session);
                }
                else
                {
                    farmNext = target == next;
                    policy.Manage(session);
                }
            }
            if (!report.Completed && report.StuckAt == null)
                report.StuckAt = db.Fights.Select(f => f.Id).OrderBy(db.GetFightOrder).FirstOrDefault(id => !session.Progress.IsFightCleared(id));
            foreach (var f in db.Fights.OrderBy(f => db.GetFightOrder(f.Id)))
            {
                var fs = stats[f.Id];
                report.Fights.Add(fs);
                // A wall = the fight needed more than two tries for the first clear (win rate of the attempts before clearing < 50%).
                var attemptsToClear = fs.Attempts - fs.Wins + (fs.Wins > 0 ? 1 : 0);
                if (fs.Wins == 0 || attemptsToClear > 2) report.Walls.Add($"{f.Id} ({attemptsToClear} tries)");
            }
            return report;
        }
    }
}
