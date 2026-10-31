using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Phones keep the 1080×1920 reference (match 0.5). Tablets (height/width ≤ 1.65: iPads, 16:10 Android tablets) get a canvas sized from the
    /// physical screen width instead, so buttons, text and cards keep about their phone size in inches rather than growing
    /// with the screen. Screens spend the extra room on more grid columns and centred content columns (<see cref="MaxSizeFitter"/>).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class AdaptiveCanvasScaler : CanvasScaler
    {
        /// <summary>Canvas units per inch on tablets. Phones run at ~370; 300 draws tablet UI ~1.2× its phone size, like iPadOS points.</summary>
        public const float TabletUnitsPerInch = 300f;
        public const float MinTabletWidth = 1300f, MaxTabletWidth = 2400f;
        /// <summary>Used when the device reports no plausible DPI (Editor game view, some Android builds): iPad Retina.</summary>
        public const float FallbackDpi = 264f;

        /// <summary>Overrides the device DPI (tests).</summary>
        public static float? DpiOverride;

        private Canvas _canvas;

        public static AdaptiveCanvasScaler Add(GameObject go)
        {
            var s = go.AddComponent<AdaptiveCanvasScaler>();
            s.uiScaleMode = ScaleMode.ScaleWithScreenSize;
            s.screenMatchMode = ScreenMatchMode.MatchWidthOrHeight;
            s.referenceResolution = new Vector2(UIManager.ReferenceWidth, UIManager.ReferenceHeight);
            s.matchWidthOrHeight = 0.5f;
            return s;
        }

        /// <summary>
        /// Canvas width in reference units for a portrait tablet screen, or 0 for a phone (phone reference applies). The game is
        /// portrait-only; landscape windows (Editor, batch-mode tests) keep the phone rule.
        /// </summary>
        public static float TabletCanvasWidth(float width, float height, float dpi)
        {
            if (width <= 0 || height < width || AspectHelper.Classify(width, height) != AspectClass.Tablet) return 0;
            if (dpi < 120 || dpi > 700) dpi = FallbackDpi;
            return Mathf.Clamp(width / dpi * TabletUnitsPerInch, MinTabletWidth, MaxTabletWidth);
        }

        protected override void HandleScaleWithScreenSize()
        {
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            var size = _canvas.renderingDisplaySize;
            var tabletWidth = TabletCanvasWidth(size.x, size.y, DpiOverride ?? UnityEngine.Device.Screen.dpi);
            if (tabletWidth > 0)
            {
                referenceResolution = new Vector2(tabletWidth, tabletWidth * size.y / size.x);
                matchWidthOrHeight = 0;
            }
            else
            {
                referenceResolution = new Vector2(UIManager.ReferenceWidth, UIManager.ReferenceHeight);
                matchWidthOrHeight = 0.5f;
            }
            base.HandleScaleWithScreenSize();
        }
    }
}
