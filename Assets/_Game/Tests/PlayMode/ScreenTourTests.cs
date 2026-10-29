using System.Collections;
using System.IO;
using System.Linq;
using Game.App;
using Game.Services;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>Boots the real game on a temporary save, plays the loop and visits every screen (screenshots in TestResults/screens).</summary>
    public class ScreenTourTests
    {
        private string _saveDir;

        public static IEnumerator BootFresh(string saveDir)
        {
            foreach (var go in Object.FindObjectsByType<GameBootstrap>()) Object.Destroy(go.gameObject);
            AppContext.Reset();
            GameBootstrap.SaveFolderOverride = saveDir;
            yield return null;
            SceneManager.LoadScene(SceneNames.Boot, LoadSceneMode.Single);
            yield return WaitFor(() => SceneManager.GetActiveScene().name != SceneNames.Main || AppContext.IsBooted, 10);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Main, 20);
            yield return WaitFor(() => Object.FindAnyObjectByType<MainSceneRoot>()?.Manager?.Top != null, 20);
        }

        public static IEnumerator WaitFor(System.Func<bool> condition, float seconds)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(condition(), "timed out waiting");
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "squadfight-tour-" + System.Guid.NewGuid().ToString("N"));
            yield return BootFresh(_saveDir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameBootstrap.SaveFolderOverride = null;
            foreach (var go in Object.FindObjectsByType<GameBootstrap>()) Object.Destroy(go.gameObject);
            AppContext.Reset();
            yield return null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        private static UIManager Manager => Object.FindAnyObjectByType<MainSceneRoot>().Manager;

        private static IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(0.7f);
            UICapture.Capture(Manager.transform, 1080, 2340, $"TestResults/screens/tour_{name}.png");
        }

        [UnityTest]
        public IEnumerator FullLoop_AndEveryScreen()
        {
            var m = Manager;
            var ctx = m.Context;
            var session = ctx.Session;
            Assert.IsInstanceOf<HomeScreen>(m.Top);
            yield return Shot("01_home");

            Navigation.GoTab(ctx, "campaign");
            Assert.IsInstanceOf<CampaignScreen>(m.Top);
            yield return Shot("02_campaign");

            var fight = session.Progress.GetNextRecommendedFight();
            m.Push<FightPreviewScreen>(fight);
            yield return Shot("03_preview");

            // Start the fight: Battle scene resolves it and Main shows the result.
            ctx.StartBattle(fight);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Battle, 10);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Main && Object.FindAnyObjectByType<MainSceneRoot>()?.Manager?.Top is ResultScreen, 20);
            m = Manager;
            ctx = m.Context;
            yield return new WaitForSecondsRealtime(1.5f);
            m.CloseAllPopups();
            yield return Shot("04_result");
            Assert.IsTrue(session.Progress.IsFightCleared(fight));
            Assert.IsTrue(File.Exists(Path.Combine(_saveDir, "save.json")), "result saved before the result screen");

            Navigation.GoTab(ctx, "heroes");
            yield return Shot("05_heroes");
            var unlockable = session.Db.Heroes.FirstOrDefault(h => !session.Roster.IsUnlocked(h.Id) && session.Roster.GetUnlockInfo(h.Id).CanUnlock);
            Assert.IsNotNull(unlockable, "first-clear rewards make a hero unlockable");
            m.Push<HeroDetailScreen>(unlockable.Id);
            yield return Shot("06_hero_locked");
            session.Roster.UnlockHero(unlockable.Id);
            HeroUnlockedPopup.Show(ctx, unlockable.Id);
            yield return Shot("07_unlocked_popup");
            m.CloseAllPopups();
            yield return Shot("08_hero_detail");

            var knight = session.Party.Members[0];
            m.Push<EquipmentPickerScreen>(new EquipmentPickerArgs(knight, 0));
            yield return Shot("09_picker");

            m.PopToRoot();
            m.Push<PartyScreen>();
            yield return Shot("10_party");

            Navigation.GoTab(ctx, "inventory");
            yield return Shot("11_inventory");
            Navigation.GoTab(ctx, "settings");
            yield return Shot("12_settings");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LanguageSwitch_UpdatesVisibleText()
        {
            var m = Manager;
            var ctx = m.Context;
            Navigation.GoTab(ctx, "settings");
            yield return null;
            var loc = (Game.Core.TableLocalizer)ctx.L;
            ctx.Session.Settings.Language = "ro";
            loc.SetLanguage("ro");
            UIScreensSetup.ShowHome(ctx);
            yield return null;
            var texts = m.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Select(t => t.text).ToList();
            CollectionAssert.Contains(texts, "Acasă", "tab bar rebuilt in Romanian");
            yield return Shot("13_home_ro");
            loc.SetLanguage("en");
        }
    }
}
