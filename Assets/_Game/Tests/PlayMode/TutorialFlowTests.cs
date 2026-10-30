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

        /// <summary>Screen-space rect of a UI element (overlay canvases: world space is screen pixels).</summary>
        private static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        private static IEnumerator AssertHighlights(TutorialOverlay overlay, string anchorId, int step)
        {
            // First frame after the tap: the hole drawn last frame must already fit the freshly built target.
            if (overlay.Visible && TutorialAnchor.Active.TryGetValue(anchorId, out var early) && early.isActiveAndEnabled)
            {
                var earlyHole = ScreenRect((RectTransform)overlay.transform.Find("Root/Hole"));
                var earlyTarget = ScreenRect(early.Rect);
                Assert.AreEqual(earlyTarget.center.y, earlyHole.center.y, 2f, $"first-frame hole on {anchorId} hole={earlyHole} target={earlyTarget}");
            }
            // Let screens finish building, laying out and animating in.
            yield return ScreenTourTests.WaitFor(() => TutorialAnchor.Active.ContainsKey(anchorId), 5);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(anchorId, overlay.TargetAnchor);
            Assert.IsTrue(overlay.Visible, $"overlay visible for {anchorId}");
            var hole = ScreenRect((RectTransform)overlay.transform.Find("Root/Hole"));
            var target = ScreenRect(TutorialAnchor.Active[anchorId].Rect);
            // The overlay blocks scrolling, so the target must be on screen (and inside its scroll view's viewport).
            Assert.IsTrue(target.yMin >= 0 && target.yMax <= Screen.height, $"{anchorId} on screen: {target}");
            var scroll = TutorialAnchor.Active[anchorId].GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scroll != null)
            {
                var view = ScreenRect(scroll.viewport);
                Assert.IsTrue(target.yMin >= view.yMin - 1 && target.yMax <= view.yMax + 1, $"{anchorId} inside its scroll view: {target} view={view}");
            }
            UICapture.Capture(Object.FindAnyObjectByType<MainSceneRoot>().Manager.transform, 1080, 2340, $"TestResults/screens/tutorial_walk_{step:00}_{anchorId}.png");
            Assert.AreEqual(target.center.x, hole.center.x, 2f, $"hole centred on {anchorId} (x) hole={hole} target={target}");
            Assert.AreEqual(target.center.y, hole.center.y, 2f, $"hole centred on {anchorId} (y) hole={hole} target={target}");
            Assert.AreEqual(target.width, hole.width, 2f, $"hole fits {anchorId}");
        }

        [UnityTest]
        public IEnumerator FullScript_HighlightsEveryTarget()
        {
            Game.BattleView.BattleScreen.SpeedOverride = 60f;
            try
            {
                var session = AppContext.Services.Get<GameSession>();
                var step = 0;
                while (session.Tutorial.Current != null)
                {
                    var anchorId = session.Tutorial.Current.Anchor;
                    var overlay = Object.FindAnyObjectByType<TutorialOverlay>();
                    yield return AssertHighlights(overlay, anchorId, step++);
                    var battle = anchorId == "preview_start" && step < 3;
                    Tap(anchorId);
                    if (battle)
                    {
                        // Story beats play before the first fight.
                        var end = Time.realtimeSinceStartup + 10;
                        while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == SceneNames.Main && Time.realtimeSinceStartup < end)
                        {
                            if (TutorialAnchor.Active.TryGetValue("story_next", out var next) && next.isActiveAndEnabled) Tap("story_next");
                            yield return new WaitForSecondsRealtime(0.2f);
                        }
                        // Battle scene resolves the fight, then Main shows the result screen.
                        yield return ScreenTourTests.WaitFor(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == SceneNames.Main
                            && Object.FindAnyObjectByType<MainSceneRoot>()?.Manager?.Top is ResultScreen, 90);
                        yield return new WaitForSecondsRealtime(1.5f);
                    }
                    else yield return null;
                }
            }
            finally { Game.BattleView.BattleScreen.SpeedOverride = null; }
        }
    }
}
