using System;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Services;

// Balance CLI: progression | walls | fights <policy> [seed]
var root = Directory.GetCurrentDirectory();
while (!Directory.Exists(Path.Combine(root, "Assets")) && Directory.GetParent(root) != null) root = Directory.GetParent(root).FullName;
var db = ContentCsvLoader.Load(Path.Combine(root, "Assets/_Game/Content/Source"));
var cmd = args.Length > 0 ? args[0] : "progression";
IPlayerPolicy Policy(string n) => n == "casual" ? new CasualPolicy() : n == "hardcore" ? new HardcorePolicy() : new GreedyPolicy();

switch (cmd)
{
    case "progression":
    {
        // All runs in parallel (independent sessions over the same read-only database), printed in a fixed order.
        var jobs = new[] { "greedy", "casual", "hardcore" }.SelectMany(p => (args.Length > 1 ? args[1] : "1,2,3").Split(',').Select(ulong.Parse).Select(seed => (p, seed))).ToList();
        var reports = new CampaignReport[jobs.Count];
        System.Threading.Tasks.Parallel.For(0, jobs.Count, k => reports[k] = CampaignSimulator.Run(db, Policy(jobs[k].p), jobs[k].seed));
        for (var k = 0; k < jobs.Count; k++)
            {
                var (p, seed) = jobs[k];
                var r = reports[k];
                var losses = r.Fights.Sum(f => f.Attempts - f.Wins);
                var heroes = r.HeroUnlockedAtFight.Count;
                Console.WriteLine($"{p,-8} s{seed} {(r.Completed ? "done" : "STUCK " + r.StuckAt),-14} fights {r.TotalFights,5} losses {losses,4} heroes {heroes,3}/{db.Heroes.Count} gold +{r.GoldEarned}/-{r.GoldSpent} level {r.PlayerLevel} ({r.PlayerXp} xp) walls {r.Walls.Count} [{string.Join(",", r.Walls.Take(8))}]");
            }
        break;
    }
    case "fights":
    {
        var policy = Policy(args.Length > 1 ? args[1] : "casual");
        var seed = args.Length > 2 ? ulong.Parse(args[2]) : 1UL;
        var snaps = FightBalance.SnapshotsAtFirstAttempt(db, policy, seed);
        Console.WriteLine(FightBalanceResult.MarkdownHeader);
        foreach (var f in db.Fights)
        {
            if (!snaps.TryGetValue(f.Id, out var data)) { Console.WriteLine($"| {f.Id} | (not reached) |"); continue; }
            var s = new GameSession(db, data, null, new SeededRandom(1), new ManualClock());
            Console.WriteLine(FightBalance.Run(s, f, 100, 1, policy.Name + "@first try").ToMarkdownRow());
        }
        break;
    }
    case "tune":
    {
        // Fits each fight's enemy multipliers so a casual player's FIRST attempt wins at a target rate (weakest of 4 simulated
        // players), chapter by chapter: snapshots are re-taken after each chapter is tuned, so later chapters see the real
        // progression. "tune tutorial" only re-tunes the two tutorial fights; "tune from <n>" starts at chapter n.
        var csvPath = Path.Combine(root, "Assets/_Game/Content/Source/fights.csv");
        var seeds = new ulong[] { 1, 2, 3, 4, 5, 6 };
        var tutorialOnly = args.Length > 1 && args[1] == "tutorial";
        var fromChapter = args.Length > 2 && args[1] == "from" ? int.Parse(args[2]) : 1;
        var toChapter = args.Length > 4 && args[3] == "to" ? int.Parse(args[4]) : db.Chapters.Count;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var c = fromChapter - 1; !tutorialOnly && c < toChapter; c++)
        {
            var chapter = db.Chapters[c];
            var next = c + 1 < db.Chapters.Count ? db.Chapters[c + 1].FightIds[0] : null;
            for (var pass = 0; pass < 2; pass++)
            {
                // The weakest of 6 casual players (replays new fights twice, plays side quests). A 400-fight campaign assumes
                // some farming; pure rushers (greedy) are reported by `progression` but not tuned for.
                var snaps = new System.Collections.Generic.Dictionary<string, Game.Save.SaveData>[seeds.Length];
                System.Threading.Tasks.Parallel.For(0, snaps.Length, k => snaps[k] = FightBalance.SnapshotsAtFirstAttempt(db, new CasualPolicy(), seeds[k], id => id == next));
                var ids = chapter.FightIds.Concat(chapter.SideQuestIds).ToList();
                System.Threading.Tasks.Parallel.ForEach(ids, id =>
                {
                    var f = db.GetFight(id);
                    if (TutorialFight(f) >= 0) return; // tuned for the tutorial party below
                    var states = snaps.Where(m => m.ContainsKey(f.Id)).Select(m => m[f.Id]).ToList();
                    if (states.Count == 0) return;
                    var i = chapter.FightIds.IndexOf(id);
                    var boss = f.Waves.Any(w => w.Spawns.Any(sp => db.GetEnemy(sp.EnemyId).IsBoss));
                    // Chapters 1-3 (onboarding) are easier so that players who never replay get through them too.
                    var onboarding = c < 3;
                    var target = f.Id == db.Fights[0].Id ? 0.99 : f.IsSideQuest ? 0.7 : boss ? (onboarding ? 0.92 : 0.8) : onboarding ? 0.97 : i == 0 ? 0.93 : 0.88;
                    var baseline = f.Waves.SelectMany(w => w.Spawns).Select(sp => sp.StatMultiplier).ToList();
                    double lo = 0.25, hi = 4;
                    for (var it = 0; it < 11; it++)
                    {
                        var mid = Math.Sqrt(lo * hi);
                        Apply(f, baseline, mid);
                        var wr = states.Min(d => FightBalance.Run(new GameSession(db, Game.Save.SaveSerializer.Clone(d), null, new SeededRandom(1), new ManualClock()), f, 24, 11, "tune").WinRate);
                        if (wr > target) lo = mid; else hi = mid;
                    }
                    // Safety margin: win rates swing hard with small power differences (loot luck), so ship enemies 12%
                    // weaker than the fitted value: the target then still holds for a noticeably weaker party.
                    Apply(f, baseline, Math.Sqrt(lo * hi) / 1.12);
                });
            }
            Console.WriteLine($"{chapter.Id} tuned ({sw.Elapsed.TotalSeconds:0}s)");
            WriteWaves(csvPath, db); // after every chapter, so a stopped run keeps its progress
        }
        // The two tutorial fights: the scripted party should win without losing a hero (onboarding, no deaths).
        foreach (var f in db.Fights.Where(fd => TutorialFight(fd) >= 0))
        {
            var party = FightBalance.TutorialParty(db, TutorialFight(f) == 1);
            var baseline = f.Waves.SelectMany(w => w.Spawns).Select(sp => sp.StatMultiplier).ToList();
            double lo = 0.2, hi = 8;
            for (var it = 0; it < 14; it++)
            {
                var mid = Math.Sqrt(lo * hi);
                Apply(f, baseline, mid);
                if (FightBalance.Run(party, f, 200, 11, "tutorial").FlawlessRate > 0.95) lo = mid; else hi = mid;
            }
            Apply(f, baseline, lo);
            Console.WriteLine($"{f.Id}: tutorial party flawless {FightBalance.Run(party, f, 200, 11, "tutorial").FlawlessRate:P0}");
        }
        WriteWaves(csvPath, db);
        Console.WriteLine("wrote " + csvPath);
        break;
    }
    case "ease":
    {
        // ease [chapters=3]: only LOWERS main-fight multipliers of the first chapters until the weakest of 6 players who never
        // replay (greedy) wins 85% of first attempts. Run after `tune`; never makes anything harder.
        var csvPath = Path.Combine(root, "Assets/_Game/Content/Source/fights.csv");
        var upTo = args.Length > 1 ? int.Parse(args[1]) : 3;
        var seeds = new ulong[] { 1, 2, 3, 4, 5, 6 };
        for (var c = 0; c < upTo; c++)
        {
            var chapter = db.Chapters[c];
            var next = db.Chapters[c + 1].FightIds[0];
            for (var pass = 0; pass < 2; pass++)
            {
                var snaps = new System.Collections.Generic.Dictionary<string, Game.Save.SaveData>[seeds.Length];
                System.Threading.Tasks.Parallel.For(0, seeds.Length, k => snaps[k] = FightBalance.SnapshotsAtFirstAttempt(db, new GreedyPolicy(), seeds[k], id => id == next));
                var eased = 0;
                System.Threading.Tasks.Parallel.ForEach(chapter.FightIds, id =>
                {
                    var f = db.GetFight(id);
                    if (TutorialFight(f) >= 0) return;
                    var states = snaps.Where(m => m.ContainsKey(id)).Select(m => m[id]).ToList();
                    if (states.Count == 0) return;
                    double Wr() => states.Min(d => FightBalance.Run(new GameSession(db, Game.Save.SaveSerializer.Clone(d), null, new SeededRandom(1), new ManualClock()), f, 24, 11, "ease").WinRate);
                    if (Wr() >= 0.85) return;
                    var baseline = f.Waves.SelectMany(w => w.Spawns).Select(sp => sp.StatMultiplier).ToList();
                    double lo = 0.3, hi = 1;
                    for (var it = 0; it < 9; it++)
                    {
                        var mid = Math.Sqrt(lo * hi);
                        Apply(f, baseline, mid);
                        if (Wr() >= 0.85) lo = mid; else hi = mid;
                    }
                    Apply(f, baseline, lo);
                    System.Threading.Interlocked.Increment(ref eased);
                });
                Console.WriteLine($"{chapter.Id} pass {pass}: eased {eased} fight(s)");
            }
        }
        WriteWaves(csvPath, db);
        break;
    }
    case "recommend":
    {
        // recommendedPower = median party power of casual players at their first attempt (rounded to 10).
        var csvPath = Path.Combine(root, "Assets/_Game/Content/Source/fights.csv");
        var powers = db.Fights.ToDictionary(f => f.Id, f => new System.Collections.Generic.List<int>());
        var runs = new CampaignReport[8];
        System.Threading.Tasks.Parallel.For(0, runs.Length, k => runs[k] = CampaignSimulator.Run(db, new CasualPolicy(), (ulong)(k + 1)));
        foreach (var r in runs)
            foreach (var fs in r.Fights) if (fs.Attempts > 0) powers[fs.FightId].Add(fs.PartyPowerAtFirstAttempt);
        RewriteColumn(csvPath, "recommendedPower", id =>
        {
            if (!powers.TryGetValue(id, out var list) || list.Count == 0) return null;
            list.Sort();
            return (Math.Round(list[list.Count / 2] / 10.0) * 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
        });
        Console.WriteLine("recommended power written");
        break;
    }
    case "tutorial":
        // How the scripted tutorial parties fare in the two tutorial fights.
        foreach (var f in db.Fights.Where(fd => TutorialFight(fd) >= 0))
        {
            var r = FightBalance.Run(FightBalance.TutorialParty(db, TutorialFight(f) == 1), f, 200, 11, "tutorial");
            Console.WriteLine($"{f.Id}: power {r.PartyPower} vs recommended {f.RecommendedPower}, win {r.WinRate:P0}, flawless {r.FlawlessRate:P0}");
        }
        break;
    case "trace":
    {
        // trace [policy] [maxFights]: progress of one run (where the time and the difficulty go).
        var policy = Policy(args.Length > 1 ? args[1] : "casual");
        var max = args.Length > 2 ? int.Parse(args[2]) : 400;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = CampaignSimulator.Run(db, policy, 1, max, (id, s) =>
        {
            if (db.GetFightOrder(id) % 10 == 0) Console.WriteLine($"{sw.ElapsedMilliseconds,7} ms  first try {id,-14} power {s.PartyPower,6} rec {db.GetFight(id).RecommendedPower,6} heroes {s.Roster.UnlockedHeroIds.Count()} items {s.Inventory.Query(includeEquipped: true).Count()}");
        });
        Console.WriteLine($"{sw.ElapsedMilliseconds} ms, {r.TotalFights} fights, {(r.Completed ? "done" : "stuck at " + r.StuckAt)}, walls: {string.Join(",", r.Walls.Take(15))}");
        break;
    }
    case "levels":
    {
        // Total XP of casual players when they first reach each chapter (sets LEVEL_ACT_FROM in gen_campaign.py).
        var firsts = db.Chapters.ToDictionary(c => c.FightIds[0], c => c.Id);
        var seen = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>();
        var runs = new CampaignReport[3];
        var gate = new object();
        System.Threading.Tasks.Parallel.For(0, runs.Length, k => runs[k] = CampaignSimulator.Run(db, new CasualPolicy(), (ulong)(k + 1), 5000, (id, s) =>
        {
            if (!firsts.TryGetValue(id, out var ch)) return;
            lock (gate) { if (!seen.TryGetValue(ch, out var l)) seen[ch] = l = new System.Collections.Generic.List<int>(); l.Add((int)s.Levels.TotalXp); }
        }));
        foreach (var c in db.Chapters) Console.WriteLine($"{c.Id,-12} xp {(seen.TryGetValue(c.Id, out var l) ? string.Join("/", l) : "-")}");
        foreach (var r in runs) Console.WriteLine($"casual s{r.Seed}: {r.TotalFights} fights, level {r.PlayerLevel}, {r.PlayerXp} xp");
        break;
    }
    case "validate":
        Console.WriteLine(ConfigValidation.Validate(db));
        break;
}

// 0 = the first tutorial fight, 1 = the second, -1 = not a tutorial fight.
int TutorialFight(FightDef f) => f.Id == TutorialService.FirstFight ? 0 : f.Id == TutorialService.SecondFight ? 1 : -1;

static void Apply(FightDef f, System.Collections.Generic.List<float> baseline, double scale)
{
    var i = 0;
    foreach (var w in f.Waves) foreach (var sp in w.Spawns) sp.StatMultiplier = (float)Math.Round(baseline[i++] * scale, 2);
}

static void WriteWaves(string csvPath, GameDatabase db) => RewriteColumn(csvPath, "waves", id => db.TryGetFight(id, out var fd)
    ? string.Join(" / ", fd.Waves.Select(w => string.Join("+", w.Spawns.Select(sp => $"{sp.EnemyId}@{sp.StatMultiplier.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}"))))
    : null);

/// <summary>Rewrites one column of a content CSV in place (comments, markers and other columns untouched; quoting kept valid).</summary>
static void RewriteColumn(string csvPath, string column, Func<string, string> valueForId)
{
    var lines = File.ReadAllLines(csvPath).ToList();
    var header = TableLocalizer.ParseCsv(lines.First(l => !l.StartsWith("#")))[0];
    var col = Array.IndexOf(header, column);
    for (var li = 0; li < lines.Count; li++)
    {
        if (lines[li].StartsWith("#") || lines[li].StartsWith("id,") || lines[li].Trim().Length == 0) continue;
        var cells = TableLocalizer.ParseCsv(lines[li])[0];
        var v = valueForId(cells[0]);
        if (v == null || col >= cells.Length) continue;
        cells[col] = v;
        lines[li] = string.Join(",", cells.Select(c => c.IndexOfAny(new[] { ',', '"' }) >= 0 ? "\"" + c.Replace("\"", "\"\"") + "\"" : c));
    }
    File.WriteAllLines(csvPath, lines);
}
