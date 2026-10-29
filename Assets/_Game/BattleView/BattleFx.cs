using System.Collections.Generic;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.BattleView
{
    /// <summary>Pooled floating numbers, hit sparks and projectiles (no per-hit allocations once warm).</summary>
    public sealed class BattleFx : MonoBehaviour
    {
        private readonly Stack<TextMeshProUGUI> _texts = new Stack<TextMeshProUGUI>();
        private readonly Stack<Image> _sparks = new Stack<Image>();
        private Theme _th;
        public int CreatedTexts { get; private set; }
        public int CreatedSparks { get; private set; }

        public static BattleFx Create(Transform parent, Theme th)
        {
            var rt = UIKit.Rect("Fx", parent).Stretch();
            var fx = rt.gameObject.AddComponent<BattleFx>();
            fx._th = th;
            return fx;
        }

        public void FloatText(Vector2 at, string text, Color color, float size, float duration)
        {
            var t = _texts.Count > 0 ? _texts.Pop() : NewText();
            t.gameObject.SetActive(true);
            t.text = text;
            t.color = color;
            t.fontSize = size;
            var rt = t.rectTransform;
            rt.anchoredPosition = at;
            rt.localScale = Vector3.one;
            Tween.To(t, 0f, 1f, duration, k =>
            {
                rt.anchoredPosition = at + new Vector2(0, 110f * k);
                t.color = t.color.WithAlpha(k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
                var s = k < 0.15f ? Mathf.Lerp(0.6f, 1.25f, k / 0.15f) : Mathf.Lerp(1.25f, 1f, Mathf.Min(1, (k - 0.15f) / 0.2f));
                rt.localScale = new Vector3(s, s, 1);
            }, Ease.Linear, () => Release(t));
        }

        private TextMeshProUGUI NewText()
        {
            CreatedTexts++;
            var t = UIKit.Text(transform, "Float", "", 48, Color.white, TextAlignmentOptions.Center, bold: true);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(400, 100);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(0, 0, 0, 200);
            return t;
        }

        private void Release(TextMeshProUGUI t)
        {
            if (t == null) return;
            t.gameObject.SetActive(false);
            _texts.Push(t);
        }

        public void Spark(Vector2 at, Color color, float size, float duration)
        {
            var img = _sparks.Count > 0 ? _sparks.Pop() : NewSpark();
            img.gameObject.SetActive(true);
            img.color = color;
            var rt = img.rectTransform;
            rt.anchoredPosition = at;
            Tween.To(img, 0f, 1f, duration, k =>
            {
                var s = Mathf.Lerp(0.3f, 1.2f, k) * size;
                rt.sizeDelta = new Vector2(s, s);
                img.color = color.WithAlpha(1f - k);
            }, Ease.OutQuad, () => ReleaseSpark(img));
        }

        /// <summary>Projectile (soft dot) flying from a to b.</summary>
        public void Projectile(Vector2 from, Vector2 to, Color color, float duration)
        {
            var img = _sparks.Count > 0 ? _sparks.Pop() : NewSpark();
            img.gameObject.SetActive(true);
            img.color = color;
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(70, 70);
            Tween.To(img, 0f, 1f, duration, k => rt.anchoredPosition = Vector2.Lerp(from, to, k) + new Vector2(0, Mathf.Sin(k * Mathf.PI) * 60f), Ease.InQuad, () => ReleaseSpark(img));
        }

        private Image NewSpark()
        {
            CreatedSparks++;
            var img = UIKit.Image(transform, "Spark", Color.white, _th.softDot);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            return img;
        }

        private void ReleaseSpark(Image img)
        {
            if (img == null) return;
            img.gameObject.SetActive(false);
            _sparks.Push(img);
        }
    }
}
