using System;
using Game.Core;
using Game.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Draws the current tutorial step: dims the screen except the target anchor (four blocker panels leave a hole, so only
    /// the target is tappable), a pulsing ring, a hint bubble and a Skip button. Advances when the anchor is tapped.
    /// </summary>
    public sealed class TutorialOverlay : MonoBehaviour, ICaptureRefresh
    {
        private UIContext _ctx;
        private TutorialService _tutorial;
        private RectTransform _root, _hole, _hint;
        private readonly Image[] _blockers = new Image[4];
        private Image _ring;
        private TextMeshProUGUI _text;
        private IDisposable _sub;
        private readonly Vector3[] _corners = new Vector3[4];

        public bool Visible => _root != null && _root.gameObject.activeSelf;
        public string TargetAnchor => _tutorial?.Current?.Anchor;

        public static TutorialOverlay Create(UIContext ctx)
        {
            var go = new GameObject("TutorialCanvas", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(ctx.Manager.transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UIManager.ReferenceWidth, UIManager.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var o = go.AddComponent<TutorialOverlay>();
            o._ctx = ctx;
            o._tutorial = ctx.Session.Tutorial;
            o.Build();
            TutorialAnchor.Tapped += o.OnTapped;
            o._sub = ctx.Session.Bus.Subscribe<TutorialChanged>(_ => o.Refresh());
            ctx.SceneDisposables.Add(o._sub);
            return o;
        }

        private void Build()
        {
            var th = _ctx.Theme;
            _root = UIKit.Rect("Root", transform).Stretch();
            for (var i = 0; i < 4; i++) _blockers[i] = UIKit.Image(_root, "Blocker" + i, th.overlay.WithAlpha(0.6f), null, raycast: true);
            _hole = UIKit.Rect("Hole", _root);
            _ring = UIKit.Image(_hole, "Ring", th.primary, th.ring);
            _ring.rectTransform.Stretch(-24, -24, -24, -24);
            _hint = UIKit.Panel(_root, "Hint", th.panelAlt).rectTransform;
            _hint.sizeDelta = new Vector2(860, 200);
            _text = UIKit.Text(_hint, "Text", "", th.fontBody, th.text, TextAlignmentOptions.Center);
            _text.rectTransform.Stretch(30, 20, 30, 20);
            var skip = UIKit.Button(_root, "Skip", _ctx.T("tut_skip"), () =>
                ConfirmPopup.Show(_ctx, _ctx.T("tut_skip_title"), _ctx.T("tut_skip_body"), () => _tutorial.Skip()), UIKit.ButtonStyle.Ghost, th.fontSmall);
            ((RectTransform)skip.transform).Anchor(new Vector2(1, 1), new Vector2(260, 90), new Vector2(-24, -(UIManager.TopBarHeight + 20)), new Vector2(1, 1));
            Refresh();
        }

        private void OnTapped(string anchorId)
        {
            if (_tutorial != null && _tutorial.OnAnchorTapped(anchorId)) _ctx.Haptics.Play(HapticKind.Light);
        }

        private void Refresh()
        {
            if (this == null) return;
            var step = _tutorial.Current;
            if (step == null)
            {
                _root.gameObject.SetActive(false);
                return;
            }
            _text.text = _ctx.T(step.TextKey);
        }

        public void RefreshForCapture() => LateUpdate();

        private void LateUpdate()
        {
            var step = _tutorial?.Current;
            // Popups other than the targeted one keep their own input (e.g. confirmations).
            var anchorVisible = step != null && TutorialAnchor.Active.TryGetValue(step.Anchor, out var anchor) && anchor != null && anchor.isActiveAndEnabled;
            if (!anchorVisible)
            {
                if (_root.gameObject.activeSelf) _root.gameObject.SetActive(false);
                return;
            }
            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);
            var a = TutorialAnchor.Active[step.Anchor];
            a.Rect.GetWorldCorners(_corners);
            var canvasRect = (RectTransform)transform;
            Vector2 min = canvasRect.InverseTransformPoint(_corners[0]);
            Vector2 max = canvasRect.InverseTransformPoint(_corners[2]);
            var size = canvasRect.rect.size;
            var half = size / 2f;
            min += half;
            max += half;
            Place(_blockers[0], new Vector2(0, max.y), new Vector2(size.x, size.y)); // top
            Place(_blockers[1], new Vector2(0, 0), new Vector2(size.x, min.y));     // bottom
            Place(_blockers[2], new Vector2(0, min.y), new Vector2(min.x, max.y));   // left
            Place(_blockers[3], new Vector2(max.x, min.y), new Vector2(size.x, max.y)); // right
            _hole.anchorMin = _hole.anchorMax = Vector2.zero;
            _hole.pivot = Vector2.zero;
            _hole.anchoredPosition = min;
            _hole.sizeDelta = max - min;
            var pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f);
            _ring.rectTransform.localScale = new Vector3(pulse, pulse, 1);
            // Hint above the target when it is in the lower half, else below.
            var below = (min.y + max.y) / 2f > size.y / 2f;
            _hint.anchorMin = _hint.anchorMax = Vector2.zero;
            _hint.pivot = new Vector2(0.5f, below ? 1f : 0f);
            _hint.anchoredPosition = new Vector2(Mathf.Clamp((min.x + max.x) / 2f, 450, Mathf.Max(450, size.x - 450)), below ? min.y - 60 : max.y + 60);
        }

        private static void Place(Image img, Vector2 from, Vector2 to)
        {
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(Mathf.Max(0, to.x - from.x), Mathf.Max(0, to.y - from.y));
        }

        private void OnDestroy() => TutorialAnchor.Tapped -= OnTapped;
    }

    /// <summary>One-time contextual tip (after the tutorial).</summary>
    public sealed class TipPopup : PopupBase
    {
        private string _tip;

        public static void ShowOnce(UIContext ctx, string tipId)
        {
            if (tipId == null || ctx.Session == null || !ctx.Session.Tutorial.ShouldShowTip(tipId)) return;
            ctx.Session.Tutorial.MarkTipSeen(tipId);
            ctx.Manager.ShowPopup<TipPopup>(p => p._tip = tipId);
        }

        protected override void Build()
        {
            UIKit.Text(Panel, "Title", Ctx.T(_tip + "_title"), Th.fontLarge, Th.primary, TextAlignmentOptions.Center, bold: true).Size(preferredHeight: 80);
            UIKit.Text(Panel, "Body", Ctx.T(_tip + "_body"), Th.fontBody, Th.text, TextAlignmentOptions.Center).Size(preferredHeight: 220);
            UIKit.Button(Panel, "Ok", Ctx.T("got_it"), Close);
        }
    }

    /// <summary>Daily login reward (shown on Home when enabled and available).</summary>
    public sealed class DailyBonusPopup : PopupBase
    {
        public static void ShowIfAvailable(UIContext ctx)
        {
            if (ctx.Session.Daily.CanClaim(DateTime.Now) && ctx.Session.Tutorial.IsComplete) ctx.Manager.ShowPopup<DailyBonusPopup>();
        }

        protected override void Build()
        {
            UIKit.Text(Panel, "Title", Ctx.T("daily_title"), Th.fontTitle, Th.primary, TextAlignmentOptions.Center, bold: true).Size(preferredHeight: 100);
            UIKit.Text(Panel, "Body", Ctx.F("daily_body", Ctx.Session.Daily.Streak + 1), Th.fontBody, Th.text, TextAlignmentOptions.Center).Size(preferredHeight: 120);
            UIKit.Button(Panel, "Claim", Ctx.T("claim"), () =>
            {
                var r = Ctx.Session.Daily.Claim(DateTime.Now);
                Close();
                if (r.Ok)
                {
                    Ctx.PlaySfx("reward");
                    Ctx.Manager.Toast(Ctx.F("daily_claimed", r.Value.Bundle.ToString()));
                }
            });
        }
    }
}
