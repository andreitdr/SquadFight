using System.Collections;
using System.IO;
using Game.App;
using Game.Services;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class TutorialFlowTests
    {
        private string _saveDir;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "squadfight-tut-" + System.Guid.NewGuid().ToString("N"));
            yield return ScreenTourTests.BootFresh(_saveDir);
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

        private static void Tap(string anchorId)
        {
            Assert.IsTrue(TutorialAnchor.Active.TryGetValue(anchorId, out var anchor), "anchor present: " + anchorId);
            ExecuteEvents.Execute(anchor.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        }

        [UnityTest]
        public IEnumerator FreshGame_ShowsOverlay_AndTapsAdvanceTheScript()
        {
            var session = AppContext.Services.Get<GameSession>();
            yield return new WaitForSecondsRealtime(0.3f);
            var overlay = Object.FindAnyObjectByType<TutorialOverlay>();
            Assert.IsNotNull(overlay, "new players get the tutorial overlay");
            Assert.IsTrue(overlay.Visible);
            Assert.AreEqual("home_continue", overlay.TargetAnchor);
            UICapture.Capture(Object.FindAnyObjectByType<MainSceneRoot>().Manager.transform, 1080, 2340, "TestResults/screens/tutorial_01.png");

            Tap("home_continue");
            yield return null;
            Assert.IsInstanceOf<FightPreviewScreen>(Object.FindAnyObjectByType<MainSceneRoot>().Manager.Top, "the real button ran too");
            Assert.AreEqual("preview_start", session.Tutorial.Current.Anchor);
            yield return new WaitForSecondsRealtime(0.3f);
            UICapture.Capture(Object.FindAnyObjectByType<MainSceneRoot>().Manager.transform, 1080, 2340, "TestResults/screens/tutorial_02.png");
            Assert.IsTrue(File.Exists(Path.Combine(_saveDir, "save.json")));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
