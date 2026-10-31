using System.Collections;
using System.Linq;
using Game.BattleView;
using Game.Core;
using Game.Data;
using Game.Services;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class BattlePresentationTests
    {
        private GameObject _root;
        private GameSession _session;
        private UIManager _manager;

        [SetUp]
        public void SetUp()
        {
            var config = ConfigDatabase.LoadFromResources();
            var clock = new ManualClock();
            _session = new GameSession(config.Database, NewGameFactory.Create(config.Database, clock), null, new SeededRandom(3), clock);
            var dbg = new DebugCommands(_session);
            foreach (var h in new[] { "hero_cleric", "hero_rogue" }) dbg.Execute($"unlock {h} force");
            _session.Party.AutoFill(id => _session.Roster.GetPower(id));
            dbg.Execute("clear chapter chapter_1");
            _root = new GameObject("BattleTestRoot");
            var ctx = new UIContext { Session = _session, Config = config, L = LocalizationLoader.Load("en"), Theme = Theme.Load() };
            _manager = UIManager.Create(ctx, _root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            BattleScreen.SpeedOverride = null;
            Object.Destroy(_root);
        }

        private static string ReferenceHash(ActiveBattle b) => BattleSimulator.Hash(new BattleSimulator(b.Setup, new SeededRandom(b.Seed)).RunToEndEvents());

        [UnityTest]
        public IEnumerator FullBossBattle_PlayedFast_MatchesSimulation_NoDesync()
        {
            BattleScreen.SpeedOverride = 60f;
            var battle = _session.Battles.Start("fight_1_8", 1234).Value;
            var reference = ReferenceHash(battle);
            bool? left = null;
            var screen = _manager.Push<BattleScreen>(new BattleScreenArgs { Battle = battle, Leave = r => left = r });
            var end = Time.realtimeSinceStartup + 120;
            var captured = false;
            while (left == null && Time.realtimeSinceStartup < end)
            {
                if (!captured && screen.EventsPlayed > 40)
                {
                    captured = true;
                    UICapture.Capture(_root.transform, 1080, 2340, "TestResults/screens/battle_mid.png");
                    UICapture.Capture(_root.transform, 1668, 2388, "TestResults/screens/battle_mid_ipad.png");
                }
                yield return null;
            }
            Assert.AreEqual(false, left, "left normally (not a retreat)");
            Assert.IsTrue(battle.Simulator.IsOver);
            Assert.AreEqual(reference, BattleSimulator.Hash(battle.Simulator.Events), "presentation must not change the simulation");
            Assert.AreEqual(battle.Simulator.Events.Count, screen.EventsPlayed);
            Assert.AreEqual(0, screen.DesyncErrors);
            Assert.Less(screen.Fx.CreatedTexts, 60, "floating numbers are pooled");
            Assert.Less(screen.Fx.CreatedSparks, 60, "sparks/projectiles are pooled");
            var completion = _session.Battles.Finish();
            Assert.IsTrue(completion.Ok);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Skip_GivesSameOutcomeAsFullSimulation()
        {
            var battle = _session.Battles.Start("fight_1_8", 777).Value;
            var reference = new BattleSimulator(battle.Setup, new SeededRandom(777)).RunToEnd().Outcome;
            bool? left = null;
            _manager.Push<BattleScreen>(new BattleScreenArgs { Battle = battle, Leave = r => left = r });
            yield return null;
            yield return null;
            _manager.Top.transform.Find("Hud/Row/Skip").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            var end = Time.realtimeSinceStartup + 10;
            while (left == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(false, left);
            var completion = _session.Battles.Finish().Value;
            Assert.AreEqual(reference, completion.Battle.Outcome);
        }

        [UnityTest]
        public IEnumerator Retreat_FromPauseMenu_RecordsDefeat()
        {
            BattleScreen.SpeedOverride = 1f;
            var battle = _session.Battles.Start("fight_1_8", 5).Value;
            bool? left = null;
            var screen = _manager.Push<BattleScreen>(new BattleScreenArgs { Battle = battle, Leave = r => left = r });
            yield return null;
            _manager.HandleBack(); // opens pause
            Assert.IsInstanceOf<BattlePausePopup>(_manager.Popups.Last());
            _manager.Popups.Last().transform.Find("Panel/Retreat").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            var confirm = (ConfirmPopup)_manager.Popups.Last();
            confirm.transform.Find("Panel/Buttons/Confirm").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(true, left);
            var completion = _session.Battles.Retreat().Value;
            Assert.IsFalse(completion.Victory);
        }
    }

    internal static class SimExt
    {
        public static System.Collections.Generic.IReadOnlyList<BattleEvent> RunToEndEvents(this BattleSimulator s)
        {
            s.RunToEnd();
            return s.Events;
        }
    }
}
