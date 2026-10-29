using System.Collections;
using System.Diagnostics;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class UIFrameworkTests
    {
        private GameObject _root;
        private UIGalleryRoot _gallery;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("GalleryTestRoot");
            _gallery = _root.AddComponent<UIGalleryRoot>();
            _gallery.Build();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SafeAreaFitter.SimulatedSafeArea = null;
            Object.Destroy(_root);
            yield return null;
        }

        private sealed class DummyScreen : ScreenBase
        {
            public bool Closed;
            protected override void Build() => UIKit.Text(Root, "T", "dummy");
            public override void OnClosed() => Closed = true;
        }

        [UnityTest]
        public IEnumerator Gallery_BuildsWithoutErrors()
        {
            yield return null;
            Assert.IsInstanceOf<UIGalleryScreen>(_gallery.Manager.Top);
            Assert.Greater(_gallery.Manager.Top.GetComponentsInChildren<HeroCard>().Length, 0);
            Assert.Greater(_gallery.Manager.Top.GetComponentsInChildren<ItemCard>().Length, 0);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Navigation_PushPopBack_DestroysInstances()
        {
            var m = _gallery.Manager;
            var a = m.Push<DummyScreen>();
            var b = m.Push<DummyScreen>();
            Assert.AreEqual(3, m.Screens.Count);
            Assert.IsFalse(a.gameObject.activeSelf, "covered screens are deactivated");
            var popup = ConfirmPopup.Show(_gallery.Context, "t", "m", null);
            m.HandleBack();
            Assert.AreEqual(0, m.Popups.Count, "back closes the popup first");
            Assert.AreEqual(3, m.Screens.Count);
            m.HandleBack();
            yield return null;
            Assert.IsTrue(b == null, "popped screen destroyed");
            Assert.AreEqual(2, m.Screens.Count);
            Assert.IsTrue(a.gameObject.activeSelf);
            m.HandleBack();
            yield return null;
            Assert.AreEqual(1, m.Screens.Count);
            var quit = false;
            _gallery.Context.Quit = () => quit = true;
            m.HandleBack();
            Assert.IsTrue(quit, "back on the root screen asks to quit");
            Assert.IsTrue(popup == null);
        }

        [UnityTest]
        public IEnumerator Replace_ClearStack_LeavesOneScreen()
        {
            var m = _gallery.Manager;
            m.Push<DummyScreen>();
            m.Push<DummyScreen>();
            m.Replace<DummyScreen>(clearStack: true);
            yield return null;
            Assert.AreEqual(1, m.Screens.Count);
            Assert.AreEqual(1, Object.FindObjectsByType<DummyScreen>().Length);
        }

        [UnityTest]
        public IEnumerator RecycleList_1000Items_StaysBoundedAndFast()
        {
            var screen = _gallery.Manager.Push<GalleryListScreen>();
            yield return null;
            yield return null;
            var list = screen.List;
            Assert.AreEqual(1000, list.Count);
            Assert.Less(list.ActiveItems, 30);
            var sw = Stopwatch.StartNew();
            var content = list.Scroll.content;
            for (var y = 0f; y < content.rect.height; y += 400f)
            {
                content.anchoredPosition = new Vector2(0, y);
                list.SendMessage("LateUpdate");
            }
            sw.Stop();
            Assert.Less(list.ActiveItems, 30);
            Assert.Less(list.CreatedItems, 40, "items are recycled, not created per row");
            Assert.Less(sw.Elapsed.TotalMilliseconds / (content.rect.height / 400f), 2.0, "scroll layout cost per step (ms)");
        }

        [UnityTest]
        public IEnumerator SafeArea_SimulatedNotch_InsetsContent()
        {
            SafeAreaFitter.SimulatedSafeArea = new Rect(0, 0, Screen.width, Screen.height * 0.9f);
            yield return null;
            var fitter = _root.GetComponentInChildren<SafeAreaFitter>();
            var rt = (RectTransform)fitter.transform;
            Assert.AreEqual(0.9f, rt.anchorMax.y, 0.01f);
        }

        [UnityTest]
        public IEnumerator Capture_GalleryAtThreeAspectRatios()
        {
            yield return new WaitForSecondsRealtime(0.6f); // let the screen fade-in finish
            var scroll = _gallery.Manager.Top.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            foreach (var (name, w, h) in new[] { ("16x9", 1080, 1920), ("19_5x9", 1080, 2340), ("4x3", 1536, 2048) })
            {
                scroll.verticalNormalizedPosition = 1;
                Assert.IsTrue(UICapture.Capture(_root.transform, w, h, $"TestResults/screens/gallery_{name}.png"));
                scroll.verticalNormalizedPosition = 0.45f;
                Assert.IsTrue(UICapture.Capture(_root.transform, w, h, $"TestResults/screens/gallery_{name}_mid.png"));
            }
            Assert.AreEqual("5.6M", CurrencyChip.FormatAmount(5_600_000));
            Assert.AreEqual("12M", CurrencyChip.FormatAmount(12_000_000));
            Assert.AreEqual("1,234", CurrencyChip.FormatAmount(1234));
        }
    }
}
