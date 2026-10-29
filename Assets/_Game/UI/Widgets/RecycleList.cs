using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Virtualized, pooled vertical list/grid: only the rows in view (+1 buffer row) exist, recycled while scrolling.
    /// No layout groups (cheap to scroll 1,000+ items at 60 fps).
    /// </summary>
    public sealed class RecycleList : MonoBehaviour
    {
        private ScrollRect _scroll;
        private RectTransform _content;
        private Func<Transform, RectTransform> _create;
        private Action<int, RectTransform> _bind;
        private readonly Dictionary<int, RectTransform> _visible = new Dictionary<int, RectTransform>();
        private readonly Stack<RectTransform> _pool = new Stack<RectTransform>();
        private readonly List<int> _toRelease = new List<int>();
        private int _count;
        private Vector2 _lastPos = new Vector2(float.NaN, float.NaN);
        private float _lastWidth = -1;

        public int Columns { get; private set; } = 1;
        public float ItemHeight { get; private set; }
        public float Spacing { get; private set; }
        public float Padding { get; private set; }
        public int Count => _count;
        public int ActiveItems => _visible.Count;
        public int CreatedItems { get; private set; }
        public ScrollRect Scroll => _scroll;

        public static RecycleList Create(Transform parent, float itemHeight, Func<Transform, RectTransform> create, Action<int, RectTransform> bind, int columns = 1, float spacing = 20, float padding = 24)
        {
            var root = UIKit.Rect("RecycleList", parent);
            var list = root.gameObject.AddComponent<RecycleList>();
            list._scroll = root.gameObject.AddComponent<ScrollRect>();
            list._scroll.horizontal = false;
            list._scroll.scrollSensitivity = 40;
            var viewport = UIKit.Rect("Viewport", root).Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            list._content = UIKit.Rect("Content", viewport);
            list._content.anchorMin = new Vector2(0, 1);
            list._content.anchorMax = new Vector2(1, 1);
            list._content.pivot = new Vector2(0.5f, 1);
            list._scroll.viewport = viewport;
            list._scroll.content = list._content;
            list._create = create;
            list._bind = bind;
            list.ItemHeight = itemHeight;
            list.Columns = Mathf.Max(1, columns);
            list.Spacing = spacing;
            list.Padding = padding;
            list._scroll.onValueChanged.AddListener(_ => list.Layout());
            return list;
        }

        public void SetCount(int count, bool resetScroll = false)
        {
            _count = Mathf.Max(0, count);
            var rows = Mathf.CeilToInt(_count / (float)Columns);
            _content.sizeDelta = new Vector2(0, rows * (ItemHeight + Spacing) - (rows > 0 ? Spacing : 0) + Padding * 2);
            if (resetScroll) _content.anchoredPosition = Vector2.zero;
            RefreshAll();
        }

        /// <summary>Re-binds every visible item (data changed).</summary>
        public void RefreshAll()
        {
            foreach (var kv in _visible) Release(kv.Value);
            _visible.Clear();
            _lastPos = new Vector2(float.NaN, float.NaN);
            Layout();
        }

        private void LateUpdate() => Layout();

        /// <summary>Immediate re-layout (e.g. after the canvas resolution changed).</summary>
        public void ForceLayout() => Layout();

        private void Layout()
        {
            if (_content == null) return;
            var width = _content.rect.width;
            if (!Mathf.Approximately(width, _lastWidth))
            {
                // Viewport got its final size (or rotated): cell widths change, rebind everything.
                _lastWidth = width;
                foreach (var kv in _visible) Release(kv.Value);
                _visible.Clear();
                _lastPos = new Vector2(float.NaN, float.NaN);
            }
            var pos = _content.anchoredPosition;
            var viewHeight = ((RectTransform)_scroll.viewport).rect.height;
            if (pos == _lastPos && _visible.Count > 0) return;
            _lastPos = pos;
            var rowH = ItemHeight + Spacing;
            var firstRow = Mathf.Max(0, Mathf.FloorToInt((pos.y - Padding) / rowH));
            var lastRow = Mathf.FloorToInt((pos.y + viewHeight - Padding) / rowH) + 1;
            var first = firstRow * Columns;
            var last = Mathf.Min(_count - 1, (lastRow + 1) * Columns - 1);

            _toRelease.Clear();
            foreach (var kv in _visible) if (kv.Key < first || kv.Key > last) _toRelease.Add(kv.Key);
            foreach (var i in _toRelease)
            {
                Release(_visible[i]);
                _visible.Remove(i);
            }

            var cellW = (width - Padding * 2 - Spacing * (Columns - 1)) / Columns;
            for (var i = first; i <= last; i++)
            {
                if (_visible.ContainsKey(i)) continue;
                var item = _pool.Count > 0 ? _pool.Pop() : CreateItem();
                item.gameObject.SetActive(true);
                var row = i / Columns;
                var col = i % Columns;
                item.anchorMin = item.anchorMax = new Vector2(0, 1);
                item.pivot = new Vector2(0, 1);
                item.sizeDelta = new Vector2(cellW, ItemHeight);
                item.anchoredPosition = new Vector2(Padding + col * (cellW + Spacing), -(Padding + row * rowH));
                _bind(i, item);
                _visible[i] = item;
            }
        }

        private RectTransform CreateItem()
        {
            CreatedItems++;
            var item = _create(_content);
            var le = item.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;
            return item;
        }

        private void Release(RectTransform item)
        {
            item.gameObject.SetActive(false);
            _pool.Push(item);
        }
    }
}
