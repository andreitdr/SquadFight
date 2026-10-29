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
        foreach (var p in new[] { "greedy", "casual", "hardcore" })
            foreach (var seed in new ulong[] { 1, 2, 3 })
            {
                var r = CampaignSimulator.Run(db, Policy(p), seed);
                var losses = r.Fights.Sum(f => f.Attempts - f.Wins);
                var unlocked = string.Join(" ", r.HeroUnlockedAtFight.Where(k => k.Value > 0).OrderBy(k => k.Value).Select(k => $"{k.Key.Replace("hero_", "")}@{k.Value}"));
                Console.WriteLine($"{p,-8} s{seed} {(r.Completed ? "done" : "STUCK " + r.StuckAt),-14} fights {r.TotalFights,4} losses {losses,3} gold +{r.GoldEarned}/-{r.GoldSpent} walls [{string.Join(",", r.Walls)}] unlocks: {unlocked}");
            }
        break;
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
        // Sets each fight's enemy multipliers so a casual player's FIRST attempt wins at a target rate, then writes fights.csv.
        var csvPath = Path.Combine(root, "Assets/_Game/Content/Source/fights.csv");
        for (var pass = 0; pass < 3; pass++)
        {
            var snaps = FightBalance.SnapshotsAtFirstAttempt(db, new CasualPolicy(), 1);
            foreach (var chapter in db.Chapters)
                for (var i = 0; i < chapter.FightIds.Count; i++)
                {
                    var f = db.GetFight(chapter.FightIds[i]);
                    if (!snaps.TryGetValue(f.Id, out var data)) continue;
                    var boss = f.Waves.Any(w => w.Spawns.Any(sp => db.GetEnemy(sp.EnemyId).IsBoss));
                    var target = f.Id == db.Fights[0].Id ? 0.98 : boss ? 0.62 : i >= 5 ? 0.75 : i == 0 ? 0.9 : 0.85;
                    var baseline = f.Waves.SelectMany(w => w.Spawns).Select(sp => sp.StatMultiplier).ToList();
                    double lo = 0.2, hi = 8;
                    for (var it = 0; it < 14; it++)
                    {
                        var mid = Math.Sqrt(lo * hi);
                        Apply(f, baseline, mid);
                        var s = new GameSession(db, Game.Save.SaveSerializer.Clone(data), null, new SeededRandom(1), new ManualClock());
                        var wr = FightBalance.Run(s, f, 80, 11, "tune").WinRate;
                        if (wr > target) lo = mid; else hi = mid;
                    }
                    Apply(f, baseline, Math.Sqrt(lo * hi));
                }
            Console.WriteLine("pass " + pass + " done");
        }
        // Write multipliers back (only the waves column changes).
        var lines = File.ReadAllLines(csvPath).ToList();
        var header = lines.First(l => !l.StartsWith("#")).Split(',');
        var wavesCol = Array.IndexOf(header, "waves");
        for (var li = 0; li < lines.Count; li++)
        {
            if (lines[li].StartsWith("#") || lines[li].StartsWith("id,")) continue;
            var cells = lines[li].Split(',');
            if (!db.TryGetFight(cells[0], out var fd)) continue;
            cells[wavesCol] = string.Join(" / ", fd.Waves.Select(w => string.Join("+", w.Spawns.Select(sp => $"{sp.EnemyId}@{sp.StatMultiplier.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}"))));
            lines[li] = string.Join(",", cells);
        }
        File.WriteAllLines(csvPath, lines);
        Console.WriteLine("wrote " + csvPath);
        break;
    }
    case "validate":
        Console.WriteLine(ConfigValidation.Validate(db));
        break;
}

static void Apply(FightDef f, System.Collections.Generic.List<float> baseline, double scale)
{
    var i = 0;
    foreach (var w in f.Waves) foreach (var sp in w.Spawns) sp.StatMultiplier = (float)Math.Round(baseline[i++] * scale, 2);
}
