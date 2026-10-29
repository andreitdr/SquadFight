using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Hero/enemy dialogue lines (Resources/Localization/strings_dialogue.csv). Keys: line_&lt;speakerId&gt;_&lt;trigger&gt;_&lt;n&gt;
    /// with n = 1, 2, …; add or remove variants freely, the picker counts them.
    /// </summary>
    public static class HeroLines
    {
        public const string Unlock = "unlock", Select = "select", StarUp = "starup", BattleStart = "battle_start", Skill = "skill",
            Kill = "kill", LowHp = "low_hp", AllyDown = "ally_down", Victory = "victory", Defeat = "defeat";

        public static int Count(UIContext ctx, string speakerId, string trigger)
        {
            if (ctx?.L == null || string.IsNullOrEmpty(speakerId)) return 0;
            var n = 0;
            while (ctx.L.Has(Key(speakerId, trigger, n + 1))) n++;
            return n;
        }

        /// <summary>A random variant for the trigger, or null when the speaker has none (presentation-only randomness).</summary>
        public static string Pick(UIContext ctx, string speakerId, string trigger)
        {
            var n = Count(ctx, speakerId, trigger);
            return n == 0 ? null : ctx.T(Key(speakerId, trigger, Random.Range(1, n + 1)));
        }

        public static string Key(string speakerId, string trigger, int n) => $"line_{speakerId}_{trigger}_{n}";
    }

    /// <summary>Comic speech bubble with a tail; pops in, holds, fades out and destroys itself (or returns to a pool).</summary>
    public sealed class SpeechBubble : MonoBehaviour
    {
        public TextMeshProUGUI Text;
        public CanvasGroup Group;
        public Image Tail;
        private int _tween;

        public static SpeechBubble Create(Transform parent, float maxWidth = 560)
        {
            var th = UIKit.Theme;
            var root = UIKit.Rect("Bubble", parent);
            root.pivot = new Vector2(0.5f, 0);
            var b = root.gameObject.AddComponent<SpeechBubble>();
            b.Group = root.gameObject.AddComponent<CanvasGroup>();
            b.Group.blocksRaycasts = false;
            var body = UIKit.Image(root, "Body", th.bubble != null ? Color.white : th.text, th.bubble != null ? th.bubble : th.rounded);
            body.rectTransform.Stretch();
            b.Tail = UIKit.Image(root, "Tail", th.bubble != null ? Color.white : th.text, th.bubbleTail != null ? th.bubbleTail : th.triangle);
            b.Tail.rectTransform.Anchor(new Vector2(0.5f, 0), new Vector2(40, 40), new Vector2(-18, -32), new Vector2(0.5f, 0));
            b.Tail.transform.SetAsFirstSibling();
            b.Text = UIKit.Text(root, "Text", "", 30, new Color(0.13f, 0.1f, 0.17f), TextAlignmentOptions.Center, bold: true);
            b.Text.rectTransform.Stretch(26, 18, 26, 20);
            b.Text.overflowMode = TextOverflowModes.Overflow;
            root.sizeDelta = new Vector2(maxWidth, 100);
            root.gameObject.SetActive(false);
            return b;
        }

        /// <summary>Shows <paramref name="text"/> with the tail tip at <paramref name="tip"/> (parent space).</summary>
        public void Show(string text, Vector2 tip, float hold = 2.2f, float fontSize = 30, float maxWidth = 560)
        {
            var rt = (RectTransform)transform;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Text.fontSize = fontSize;
            Text.text = text;
            var pref = Text.GetPreferredValues(text, maxWidth - 52, 0);
            var w = Mathf.Clamp(pref.x + 56, 160, maxWidth);
            var h = Text.GetPreferredValues(text, w - 52, 0).y + 40;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = tip + new Vector2(0, 30);
            Tween.Kill(_tween);
            Group.alpha = 1;
            Tween.Scale(transform, 0.4f, 1f, 0.22f, Ease.OutBack);
            _tween = Tween.To(this, 1f, 0f, 0.3f, a => Group.alpha = a, Ease.Linear, () => { if (this != null) gameObject.SetActive(false); }, hold);
        }

        public bool Busy => gameObject.activeSelf;
    }
}
