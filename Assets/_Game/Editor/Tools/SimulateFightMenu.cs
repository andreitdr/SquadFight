using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.EditorTools.Content;
using Game.Services;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Tools/Game/Debug/Simulate Fight: runs the selected FightDefinition (or the first fight) with a new-game party and logs the battle.</summary>
    public static class SimulateFightMenu
    {
        [MenuItem("Tools/Game/Debug/Simulate Fight (selected or first)")]
        public static void Simulate()
        {
            var config = ConfigValidatorMenu.LoadGameConfig();
            if (!config) { Debug.LogError("No GameConfig."); return; }
            var cdb = new ConfigDatabase(config);
            var db = cdb.Database;
            var fight = Selection.activeObject is FightDefinition fd ? db.GetFight(fd.id) : db.Fights.FirstOrDefault();
            if (fight == null) { Debug.LogError("No fight."); return; }
            Debug.Log(Run(db, fight, seed: 1, out _));
        }

        public static string Run(GameDatabase db, FightDef fight, ulong seed, out BattleResult result)
        {
            var clock = new ManualClock();
            var session = new GameSession(db, NewGameFactory.Create(db, clock), null, new SeededRandom(seed), clock);
            var sim = new BattleSimulator(BattleSetupFactory.Create(session, fight), new SeededRandom(seed));
            result = sim.RunToEnd();
            var names = LoadEnglishNames();
            return $"Simulated {fight.Id} (seed {seed}, party power {session.PartyPower}, recommended {fight.RecommendedPower})\n" +
                   BattleLog.Format(sim, id => names.TryGetValue(id + "_name", out var n) ? n : id);
        }

        private static Dictionary<string, string> LoadEnglishNames()
        {
            var map = new Dictionary<string, string>();
            if (!File.Exists(ContentPaths.ContentStrings)) return map;
            foreach (var row in Csv.Read(ContentPaths.ContentStrings).Rows) map[row.Str("key")] = row.Str("en");
            return map;
        }
    }
}
