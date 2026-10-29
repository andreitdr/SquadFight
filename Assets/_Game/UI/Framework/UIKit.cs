using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Code-first uGUI construction helpers (sizes in 1080×1920 reference pixels).</summary>
    public static class UIKit
    {
        public static Theme Theme { get; set; }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Anchor(this RectTransform rt, Vector2 anchor, Vector2 size, Vector2 position = default, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        /// <summary>Full-width band anchored to the top (height h) or bottom.</summary>
        public static RectTransform TopBand(this RectTransform rt, float height, float y = 0)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = new Vector2(0, -y);
            return rt;
        }

        public static RectTransform BottomBand(this RectTransform rt, float height, float y = 0)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = new Vector2(0, y);
            return rt;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null, bool raycast = false)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            img.preserveAspect = sprite != null && sprite.border == Vector4.zero;
            return img;
        }

        public static Image Panel(Transform parent, string name, Color? color = null, bool raycast = false) =>
            Image(parent, name, color ?? Theme.panel, Theme.rounded, raycast);

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size = 0, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (Theme.font != null) t.font = Theme.font;
            t.text = text;
            t.fontSize = size > 0 ? size : Theme.fontBody;
            t.color = color ?? Theme.text;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate; // TMP ellipsis lookup fails on the dynamic font (see DECISIONS step 12)
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        public enum ButtonStyle { Primary, Secondary, Ghost, Danger }

        public static Button Button(Transform parent, string name, string label, Action onClick, ButtonStyle style = ButtonStyle.Primary, float fontSize = 0)
        {
            var bg = Image(parent, name, ButtonColor(style), Theme.rounded, raycast: true);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            bg.gameObject.AddComponent<ButtonFeedback>();
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.minHeight = Theme.minTouchTarget;
            le.preferredHeight = Theme.buttonHeight;
            if (label != null)
            {
                var t = Text(bg.transform, "Label", label, fontSize > 0 ? fontSize : Theme.fontLarge, style == ButtonStyle.Primary ? Theme.primaryText : Theme.text, TextAlignmentOptions.Center, bold: true);
                t.rectTransform.Stretch(16, 8, 16, 8);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.enableAutoSizing = true;
                t.fontSizeMin = 24;
                t.fontSizeMax = t.fontSize;
            }
            return button;
        }

        public static Color ButtonColor(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Secondary: return Theme.secondary;
                case ButtonStyle.Ghost: return Theme.panelAlt;
                case ButtonStyle.Danger: return Theme.negative;
                default: return Theme.primary;
            }
        }

        public static void SetLabel(this Button b, string text)
        {
            var t = b.GetComponentInChildren<TextMeshProUGUI>();
            if (t != null) t.text = text;
        }

        public static VerticalLayoutGroup VStack(GameObject go, float spacing = -1, float padding = 0, TextAnchor align = TextAnchor.UpperCenter, bool expandWidth = true)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing < 0 ? Theme.spacing : spacing;
            v.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = expandWidth;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(GameObject go, float spacing = -1, float padding = 0, TextAnchor align = TextAnchor.MiddleCenter, bool expandWidth = true)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing < 0 ? Theme.spacing : spacing;
            h.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = expandWidth;
            h.childForceExpandHeight = true;
            return h;
        }

        public static LayoutElement Size(this Component c, float preferredWidth = -1, float preferredHeight = -1, float flexibleWidth = -1, float flexibleHeight = -1, float minHeight = -1)
        {
            var le = c.TryGetComponent<LayoutElement>(out var existing) ? existing : c.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth >= 0) { le.preferredWidth = preferredWidth; le.minWidth = Mathf.Min(le.minWidth > 0 ? le.minWidth : preferredWidth, preferredWidth); }
            if (preferredHeight >= 0) { le.preferredHeight = preferredHeight; le.minHeight = Mathf.Max(le.minHeight, minHeight >= 0 ? minHeight : preferredHeight); }
            // A fixed preferred size means "do not stretch" unless a flexible size is given (overrides layout-group flex).
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            else if (preferredWidth >= 0) le.flexibleWidth = 0;
            if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
            else if (preferredHeight >= 0) le.flexibleHeight = 0;
            return le;
        }

        /// <summary>Vertical scroll view; children go into the returned content (vertical layout + content size fitter).</summary>
        public static ScrollRect ScrollView(Transform parent, string name, out RectTransform content, float spacing = -1, float padding = 0)
        {
            var root = Rect(name, parent);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40;
            var viewport = Rect("Viewport", root).Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            VStack(content.gameObject, spacing, padding);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

        public static void Clear(Transform t)
        {
            for (var i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Invisible full-rect click catcher (backdrops).</summary>
        public static Button ClickArea(Transform parent, string name, Action onClick, Color? color = null)
        {
            var img = Image(parent, name, color ?? new Color(0, 0, 0, 0), null, raycast: true);
            img.rectTransform.Stretch();
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => onClick?.Invoke());
            return b;
        }
    }

    /// <summary>Press feedback: scale down on press, haptic + click sound hooks.</summary>
    public sealed class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public static Action OnPressed;
        private Selectable _sel;
        private int _tween;

        private void Awake() => _sel = GetComponent<Selectable>();

        public void OnPointerDown(PointerEventData e)
        {
            if (_sel != null && !_sel.IsInteractable()) return;
            Tween.Kill(_tween);
            _tween = Tween.Scale(transform, transform.localScale.x, 0.94f, 0.06f, Ease.OutQuad);
            OnPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) => Release();

        private void Release()
        {
            Tween.Kill(_tween);
            _tween = Tween.Scale(transform, transform.localScale.x, 1f, 0.12f, Ease.OutBack);
        }
    }

    /// <summary>Reports a long press (default 0.5 s) without blocking normal clicks.</summary>
    public sealed class LongPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float Seconds = 0.5f;
        public UnityEvent Triggered = new UnityEvent();
        private float _downAt = -1;

        public void OnPointerDown(PointerEventData e) => _downAt = Time.unscaledTime;
        public void OnPointerUp(PointerEventData e) => _downAt = -1;
        public void OnPointerExit(PointerEventData e) => _downAt = -1;

        private void Update()
        {
            if (_downAt < 0 || Time.unscaledTime - _downAt < Seconds) return;
            _downAt = -1;
            Triggered.Invoke();
        }
    }
}
