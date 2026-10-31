using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Caps a stretched RectTransform at a maximum width and/or height, centred in its parent. On phones the cap is never
    /// reached; on tablets content keeps phone-like line lengths while backgrounds stay full-bleed.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class MaxSizeFitter : MonoBehaviour, ICaptureRefresh
    {
        /// <summary>≤ 0 = no cap on that axis.</summary>
        public float MaxWidth, MaxHeight;
        private Vector2 _baseMin, _baseMax, _parentSize;
        private bool _ready;

        /// <summary>Keeps the rect's current offsets as its margins and adds a centred inset beyond the cap.</summary>
        public static MaxSizeFitter Attach(RectTransform rt, float maxWidth, float maxHeight = 0)
        {
            var f = rt.gameObject.AddComponent<MaxSizeFitter>();
            f.MaxWidth = maxWidth;
            f.MaxHeight = maxHeight;
            f._baseMin = rt.offsetMin;
            f._baseMax = rt.offsetMax;
            f._ready = true;
            f.Apply(true);
            return f;
        }

        private void OnEnable() => Apply(true);
        private void Update() => Apply(false);
        private void OnRectTransformDimensionsChange() => Apply(false);
        public void RefreshForCapture() => Apply(false);

        private void Apply(bool force)
        {
            if (!_ready || !(transform.parent is RectTransform parent)) return;
            var size = parent.rect.size;
            if (!force && size == _parentSize) return;
            _parentSize = size;
            var w = size.x - _baseMin.x + _baseMax.x;
            var h = size.y - _baseMin.y + _baseMax.y;
            var inset = new Vector2(MaxWidth > 0 ? Mathf.Max(0, (w - MaxWidth) * 0.5f) : 0, MaxHeight > 0 ? Mathf.Max(0, (h - MaxHeight) * 0.5f) : 0);
            var rt = (RectTransform)transform;
            rt.offsetMin = _baseMin + inset;
            rt.offsetMax = _baseMax - inset;
        }
    }
}
