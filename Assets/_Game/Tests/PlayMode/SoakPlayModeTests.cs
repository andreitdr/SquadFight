using System.Collections;
using System.IO;
using Game.App;
using Game.BattleView;
using Game.Services;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>Repeated real scene round trips: no leaked UI objects, bus handlers or managed memory growth.</summary>
    public class SoakPlayModeTests
    {
        private string _saveDir;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BattleScreen.SpeedOverride = 400f;
            _saveDir = Path.Combine(Path.GetTempPath(), "squadfight-soak-" + System.Guid.NewGuid().ToString("N"));
            yield return ScreenTourTests.BootFresh(_saveDir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleScreen.SpeedOverride = null;
            GameBootstrap.SaveFolderOverride = null;
            foreach (var go in Object.FindObjectsByType<GameBootstrap>()) Object.Destroy(go.gameObject);
            AppContext.Reset();
            yield return null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        [UnityTest]
        public IEnumerator TwentyBattleRoundTrips_NoLeaks()
        {
            var session = AppContext.Services.Get<GameSession>();
            long baseline = 0;
            int handlersAfterFirst = 0;
            for (var i = 0; i < 20; i++)
            {
                var ctx = Object.FindAnyObjectByType<MainSceneRoot>().Manager.Context;
                var fight = session.Progress.GetNextRecommendedFight();
                if (!session.Progress.IsFightUnlocked(fight)) fight = session.Db.Fights[0].Id;
                ctx.StartBattle(fight);
                yield return ScreenTourTests.WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Battle, 10);
                yield return ScreenTourTests.WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Main && Object.FindAnyObjectByType<MainSceneRoot>()?.Manager?.Top is ResultScreen, 60);
                Object.FindAnyObjectByType<MainSceneRoot>().Manager.CloseAllPopups();
                Assert.AreEqual(1, Object.FindObjectsByType<UIManager>().Length, "exactly one UI root after each round trip");
                Assert.AreEqual(0, Object.FindObjectsByType<BattleScreen>().Length, "battle UI destroyed with its scene");
                if (i == 1)
                {
                    System.GC.Collect();
                    System.GC.WaitForPendingFinalizers();
                    baseline = System.GC.GetTotalMemory(true);
                    handlersAfterFirst = session.Bus.HandlerCount<CurrencyChanged>() + session.Bus.HandlerCount<PartyChanged>();
                }
            }
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            var growthMb = (System.GC.GetTotalMemory(true) - baseline) / (1024.0 * 1024.0);
            TestContext.WriteLine($"managed memory growth over 18 round trips: {growthMb:0.00} MB");
            Assert.Less(growthMb, 8.0, "managed memory must not grow with battles");
            Assert.AreEqual(handlersAfterFirst, session.Bus.HandlerCount<CurrencyChanged>() + session.Bus.HandlerCount<PartyChanged>(), "bus subscriptions released on scene change");
            Assert.AreEqual(20, session.State.Data.Progress.TotalFights);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
