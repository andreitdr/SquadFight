using UnityEngine;

namespace Game.UI
{
    /// <summary>Keeps a full-screen RectTransform inside Screen.safeArea (notches, rounded corners, home indicator).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _applied;
        private Vector2Int _screen;

        /// <summary>Overrides Screen.safeArea in tests / the gallery simulator (null = real device value).</summary>
        public static Rect? SimulatedSafeArea;

        private void OnEnable() => Apply(true);
        private void Update() => Apply(false);

        private void Apply(bool force)
        {
            var area = SimulatedSafeArea ?? Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && area == _applied && screen == _screen) return;
            _applied = area;
            _screen = screen;
            if (screen.x <= 0 || screen.y <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            rt.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    public enum AspectClass { Tablet, Standard, Tall }

    public static class AspectHelper
    {
        /// <summary>
        /// Portrait height/width: ≤1.65 tablet (4:3 ≈ 1.33, iPad mini ≈ 1.52, 16:10 Android ≈ 1.6), ≤1.9 standard
        /// (16:9 ≈ 1.78), else tall (19.5:9 ≈ 2.17).
        /// </summary>
        public static AspectClass Classify(float width, float height)
        {
            var r = height / Mathf.Max(1f, width);
            return r <= 1.65f ? AspectClass.Tablet : r <= 1.9f ? AspectClass.Standard : AspectClass.Tall;
        }

        public static AspectClass Current => Classify(Screen.width, Screen.height);
    }
}
