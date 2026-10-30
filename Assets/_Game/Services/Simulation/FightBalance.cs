using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Save;

namespace Game.Services
{
    public sealed class FightBalanceResult
    {
        public string FightId;
        public string Preset;
        public int PartyPower;
        public int Runs;
        public int Wins;
        /// <summary>Wins in which no hero fell.</summary>
        public int Flawless;
        public double AvgRounds;
        /// <summary>Average fraction of total party HP lost (wins and losses).</summary>
        public double AvgHpLost;
        public readonly int[] Stars = new int[4];
        public double WinRate => Runs == 0 ? 0 : (double)Wins / Runs;
        public double FlawlessRate => Runs == 0 ? 0 : (double)Flawless / Runs;

        public string ToMarkdownRow() =>
            $"| {FightId} | {Preset} | {PartyPower} | {Runs} | {WinRate:P0} | {AvgRounds:0.0} | {AvgHpLost:P0} | {string.Join("/", Stars)} |";

        public const string MarkdownHeader = "| Fight | Party | Power | Runs | Win rate | Avg rounds | Avg HP lost | Stars 0/1/2/3 |\n|---|---|---|---|---|---|---|---|";
    }

    /// <summary>Runs one fight many times against a given party using the real simulator.</summary>
    public static class FightBalance
    {
        public static FightBalanceResult Run(GameSession party, FightDef fight, int runs, ulong seed, string presetName)
        {
            var result = new FightBalanceResult { FightId = fight.Id, Preset = presetName, PartyPower = party.PartyPower, Runs = runs };
            double rounds = 0, hpLost = 0;
            for (var i = 0; i < runs; i++)
            {
                var setup = BattleSetupFactory.Create(party, fight);
                var sim = new BattleSimulator(setup, new SeededRandom(seed + (ulong)i * 7919UL));
                var r = sim.RunToEnd();
                if (r.Victory) result.Wins++;
                if (r.Victory && sim.Players.All(p => p.Alive)) result.Flawless++;
                rounds += r.Rounds;
                var max = sim.Players.Sum(p => (double)p.MaxHp);
                hpLost += max <= 0 ? 0 : sim.Players.Sum(p => (double)(p.MaxHp - p.Hp)) / max;
                result.Stars[ProgressionRules.EvaluateStars(r, fight, party.Db.Rules)]++;
            }
            result.AvgRounds = rounds / Math.Max(1, runs);
            result.AvgHpLost = hpLost / Math.Max(1, runs);
            return result;
        }

        /// <summary>
        /// The party a player has in the tutorial: the starting heroes for the first fight; for the second, plus the guided
        /// hero (unlocked, in the party) wearing the first-clear weapon in slot 0, exactly as the script makes them do.
        /// </summary>
        public static GameSession TutorialParty(GameDatabase db, bool afterFirstFight)
        {
            var clock = new ManualClock();
            var s = new GameSession(db, NewGameFactory.Create(db, clock), null, new SeededRandom(1), clock);
            if (!afterFirstFight) return s;
            s.State.GetOrCreateHero(TutorialService.GuidedHero).Unlocked = true;
            s.State.GetOrCreateHero(TutorialService.GuidedHero).Stars = 1;
            s.Party.Add(TutorialService.GuidedHero);
            var sword = EquipmentFactory.Create(db.Equipment.Where(e => e.Slot == db.Rules.SlotLayout[0]).OrderBy(e => e.Rarity).First(), Rarity.Common, s.Random, db);
            s.Inventory.AddItem(sword);
            s.Inventory.Equip(TutorialService.GuidedHero, sword.InstanceId, 0);
            return s;
        }

        /// <summary>A party preset: the given heroes unlocked at a star level, each wearing one item per slot of the given rarity.</summary>
        public static GameSession Preset(GameDatabase db, IEnumerable<string> heroIds, int star, Rarity? gear)
        {
            var clock = new ManualClock();
            var data = NewGameFactory.Create(db, clock);
            var ids = heroIds.ToList();
            foreach (var h in data.Heroes)
            {
                var on = ids.Contains(h.HeroId);
                h.Unlocked = on;
                h.Stars = on ? Math.Max(1, Math.Min(star, db.MaxStar)) : 0;
            }
            data.Party = Enumerable.Range(0, db.Rules.PartySize).Select(i => i < ids.Count ? ids[i] : null).ToList();
            var s = new GameSession(db, data, null, new SeededRandom(1), clock);
            if (gear.HasValue)
                foreach (var hero in s.Party.Members)
                    for (var slot = 0; slot < db.Rules.SlotLayout.Count; slot++)
                    {
                        var t = db.Equipment.Where(e => e.Slot == db.Rules.SlotLayout[slot]).OrderBy(e => e.Rarity).FirstOrDefault();
                        if (t == null) continue;
                        var item = EquipmentFactory.Create(t, gear.Value, s.Random, db);
                        s.Inventory.AddItem(item);
                        s.Inventory.Equip(hero, item.InstanceId, slot);
                    }
            return s;
        }

        /// <summary>The party of a simulated player right before attempting each fight for the first time (from the progression simulator).</summary>
        public static Dictionary<string, SaveData> SnapshotsAtFirstAttempt(GameDatabase db, IPlayerPolicy policy, ulong seed)
        {
            var map = new Dictionary<string, SaveData>();
            CampaignSimulator.Run(db, policy, seed, onBeforeFirstAttempt: (fightId, session) => map[fightId] = SaveSerializer.Clone(session.State.Data));
            return map;
        }

        public static string Table(IEnumerable<FightBalanceResult> results)
        {
            var sb = new StringBuilder(FightBalanceResult.MarkdownHeader).Append('\n');
            foreach (var r in results) sb.AppendLine(r.ToMarkdownRow());
            return sb.ToString();
        }
    }
}
