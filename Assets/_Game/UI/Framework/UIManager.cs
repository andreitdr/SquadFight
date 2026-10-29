using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Owns the UI canvases and navigation: a screen stack (push/pop/replace), a popup stack, toasts, a loading overlay
    /// and Android back handling. Screens/popups are created on demand and destroyed when removed (no leaks).
    /// Layers: Screens (with optional top bar / tab bar chrome) → Popups → Toasts → Overlay.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        public const float ReferenceWidth = 1080, ReferenceHeight = 1920;
        public const float TopBarHeight = 150, TabBarHeight = 190;

        private readonly List<ScreenBase> _screens = new List<ScreenBase>();
        private readonly List<PopupBase> _popups = new List<PopupBase>();
        private readonly Queue<string> _toasts = new Queue<string>();
        private UIContext _ctx;
        private RectTransform _screenHost, _topHost, _tabHost, _popupLayer, _toastLayer, _overlayLayer;
        private CanvasGroup _fade, _loading;
        private bool _toastShowing;
        private int _loadingCount;

        public IReadOnlyList<ScreenBase> Screens => _screens;
        public IReadOnlyList<PopupBase> Popups => _popups;
        public ScreenBase Top => _screens.Count > 0 ? _screens[_screens.Count - 1] : null;
        public RectTransform TopBarHost => _topHost;
        public RectTransform TabBarHost => _tabHost;
        public bool IsLoading => _loadingCount > 0;

        /// <summary>Raised after navigation changes the top screen (chrome refresh, tutorial).</summary>
        public event Action<ScreenBase> TopChanged;

        public static UIManager Create(UIContext ctx, Transform parent = null)
        {
            var go = new GameObject("UIManager");
            if (parent != null) go.transform.SetParent(parent, false);
            var m = go.AddComponent<UIManager>();
            m._ctx = ctx;
            ctx.Manager = m;
            UIKit.Theme = ctx.Theme;
            m.BuildLayers();
            return m;
        }

        private RectTransform Canvas(string name, int order, bool safeArea)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var root = UIKit.Rect("SafeArea", go.transform).Stretch();
            if (safeArea) root.gameObject.AddComponent<SafeAreaFitter>();
            return root;
        }

        private void BuildLayers()
        {
            // Full-bleed background behind the safe area.
            var bgCanvas = Canvas("BackgroundCanvas", -10, false);
            UIKit.Image(bgCanvas, "Background", _ctx.Theme.background).rectTransform.Stretch();

            var screens = Canvas("ScreensCanvas", 0, true);
            _screenHost = UIKit.Rect("Screens", screens).Stretch();
            _topHost = UIKit.Rect("TopBar", screens).TopBand(TopBarHeight);
            _tabHost = UIKit.Rect("TabBar", screens).BottomBand(TabBarHeight);
            _popupLayer = Canvas("PopupCanvas", 10, true);
            _toastLayer = Canvas("ToastCanvas", 20, true);
            _overlayLayer = Canvas("OverlayCanvas", 30, false);

            var loading = UIKit.Image(_overlayLayer, "Loading", _ctx.Theme.overlay, null, raycast: true);
            loading.rectTransform.Stretch();
            _loading = loading.gameObject.AddComponent<CanvasGroup>();
            var spinnerText = UIKit.Text(loading.transform, "Text", "…", _ctx.Theme.fontTitle, _ctx.Theme.text, TMPro.TextAlignmentOptions.Center);
            spinnerText.rectTransform.Stretch();
            loading.gameObject.SetActive(false);

            var fade = UIKit.Image(_overlayLayer, "Fade", Color.black, null, raycast: true);
            fade.rectTransform.Stretch();
            _fade = fade.gameObject.AddComponent<CanvasGroup>();
            _fade.alpha = 0;
            fade.gameObject.SetActive(false);
            ApplyChrome();
        }

        // ---------------- screens ----------------

        public T Push<T>(object args = null) where T : ScreenBase
        {
            var previous = Top;
            if (previous != null)
            {
                previous.OnHide();
                previous.gameObject.SetActive(false);
            }
            var screen = CreateScreen<T>(args);
            _screens.Add(screen);
            AnimateIn(screen);
            ApplyChrome();
            screen.OnShow();
            TopChanged?.Invoke(screen);
            return screen;
        }

        /// <summary>Replaces the top screen (or the whole stack when <paramref name="clearStack"/>).</summary>
        public T Replace<T>(object args = null, bool clearStack = false) where T : ScreenBase
        {
            if (clearStack) while (_screens.Count > 0) DestroyScreen(_screens[_screens.Count - 1]);
            else if (Top != null) DestroyScreen(Top);
            var screen = CreateScreen<T>(args);
            _screens.Add(screen);
            AnimateIn(screen);
            ApplyChrome();
            screen.OnShow();
            TopChanged?.Invoke(screen);
            return screen;
        }

        public void Pop() => Pop(Top);

        public void Pop(ScreenBase screen)
        {
            if (screen == null || !_screens.Contains(screen)) return;
            var wasTop = screen == Top;
            DestroyScreen(screen);
            if (!wasTop) return;
            var top = Top;
            if (top != null)
            {
                top.gameObject.SetActive(true);
                AnimateIn(top);
                top.OnShow();
            }
            ApplyChrome();
            TopChanged?.Invoke(top);
        }

        /// <summary>Pops everything above the root screen.</summary>
        public void PopToRoot()
        {
            while (_screens.Count > 1) DestroyScreen(Top);
            var top = Top;
            if (top != null)
            {
                top.gameObject.SetActive(true);
                top.OnShow();
            }
            ApplyChrome();
            TopChanged?.Invoke(top);
        }

        public T Find<T>() where T : ScreenBase
        {
            for (var i = _screens.Count - 1; i >= 0; i--) if (_screens[i] is T t) return t;
            return null;
        }

        private T CreateScreen<T>(object args) where T : ScreenBase
        {
            var rt = UIKit.Rect(typeof(T).Name, _screenHost).Stretch();
            var screen = rt.gameObject.AddComponent<T>();
            screen.Init(_ctx, args);
            return screen;
        }

        private void DestroyScreen(ScreenBase s)
        {
            _screens.Remove(s);
            s.OnHide();
            s.OnClosed();
            Destroy(s.gameObject);
        }

        private void AnimateIn(ScreenBase s)
        {
            s.Group.alpha = 0;
            Tween.Fade(s.Group, 1, _ctx.Theme.screenTransition);
        }

        private void ApplyChrome()
        {
            var top = Top;
            var showTop = top != null && top.ShowTopBar;
            var showTab = top != null && top.ShowTabBar;
            _topHost.gameObject.SetActive(showTop && _topHost.childCount > 0);
            _tabHost.gameObject.SetActive(showTab && _tabHost.childCount > 0);
            _screenHost.offsetMax = new Vector2(0, showTop && _topHost.childCount > 0 ? -TopBarHeight : 0);
            _screenHost.offsetMin = new Vector2(0, showTab && _tabHost.childCount > 0 ? TabBarHeight : 0);
        }

        /// <summary>Re-evaluates chrome visibility (call after adding top bar / tab bar content).</summary>
        public void RefreshChrome() => ApplyChrome();

        // ---------------- popups ----------------

        public T ShowPopup<T>(Action<T> configure = null) where T : PopupBase
        {
            var rt = UIKit.Rect(typeof(T).Name, _popupLayer);
            var popup = rt.gameObject.AddComponent<T>();
            configure?.Invoke(popup);
            popup.Init(_ctx);
            _popups.Add(popup);
            popup.Group.alpha = 0;
            Tween.Fade(popup.Group, 1, _ctx.Theme.popupTransition);
            var panel = popup.transform.Find("Panel");
            if (panel != null) Tween.Scale(panel, 0.9f, 1f, _ctx.Theme.popupTransition, Ease.OutBack);
            return popup;
        }

        public void ClosePopup(PopupBase popup)
        {
            if (popup == null || !_popups.Remove(popup)) return;
            popup.OnClosed();
            Destroy(popup.gameObject);
        }

        public void CloseAllPopups()
        {
            while (_popups.Count > 0) ClosePopup(_popups[_popups.Count - 1]);
        }

        // ---------------- toasts / loading / fade ----------------

        public void Toast(string message)
        {
            _toasts.Enqueue(message);
            if (!_toastShowing) StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            _toastShowing = true;
            while (_toasts.Count > 0)
            {
                var msg = _toasts.Dequeue();
                var panel = UIKit.Panel(_toastLayer, "Toast", _ctx.Theme.panelAlt);
                panel.rectTransform.Anchor(new Vector2(0.5f, 0), new Vector2(900, 130), new Vector2(0, 260), new Vector2(0.5f, 0));
                var text = UIKit.Text(panel.transform, "Text", msg, _ctx.Theme.fontBody, _ctx.Theme.text, TMPro.TextAlignmentOptions.Center);
                text.rectTransform.Stretch(24, 8, 24, 8);
                var g = panel.gameObject.AddComponent<CanvasGroup>();
                g.alpha = 0;
                Tween.Fade(g, 1, 0.15f);
                yield return new WaitForSecondsRealtime(1.8f);
                Tween.Fade(g, 0, 0.2f);
                yield return new WaitForSecondsRealtime(0.22f);
                Destroy(panel.gameObject);
            }
            _toastShowing = false;
        }

        public void ShowLoading(bool show)
        {
            _loadingCount = Mathf.Max(0, _loadingCount + (show ? 1 : -1));
            _loading.gameObject.SetActive(_loadingCount > 0);
        }

        public IEnumerator FadeOut(float duration = 0.25f)
        {
            _fade.gameObject.SetActive(true);
            var done = false;
            Tween.Fade(_fade, 1, duration, () => done = true);
            while (!done) yield return null;
        }

        public IEnumerator FadeIn(float duration = 0.25f)
        {
            var done = false;
            Tween.Fade(_fade, 0, duration, () => done = true);
            while (!done) yield return null;
            _fade.gameObject.SetActive(false);
        }

        // ---------------- back ----------------

        private void Update()
        {
            // Android back is delivered as Escape by the Input System.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) HandleBack();
        }

        /// <summary>Back: closes the top popup, else lets the screen handle it, else pops; at the root asks to quit.</summary>
        public void HandleBack()
        {
            if (IsLoading) return;
            if (_popups.Count > 0)
            {
                _popups[_popups.Count - 1].HandleBack();
                return;
            }
            var top = Top;
            if (top == null) return;
            if (top.HandleBack()) return;
            if (_screens.Count > 1) Pop(top);
            else _ctx.Quit?.Invoke();
        }

        private void OnDestroy()
        {
            foreach (var p in _popups) if (p != null) p.OnClosed();
            foreach (var s in _screens) if (s != null) s.OnClosed();
        }
    }
}
