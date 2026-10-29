using System;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Small red notification dot on top of any element.</summary>
    public sealed class RedDot : MonoBehaviour
    {
        private Image _dot;

        public static RedDot Attach(Transform parent, float size = 34)
        {
            var skinned = UIKit.Theme.redDotSprite != null;
            var dot = UIKit.Image(parent, "RedDot", skinned ? Color.white : UIKit.Theme.redDot, skinned ? UIKit.Theme.redDotSprite : UIKit.Theme.circle);
            dot.rectTransform.Anchor(new Vector2(1, 1), new Vector2(size, size), new Vector2(-6, -6), new Vector2(1, 1));
            var rd = dot.gameObject.AddComponent<RedDot>();
            rd._dot = dot;
            dot.gameObject.SetActive(false);
            return rd;
        }

        public void Show(bool on) => gameObject.SetActive(on);
    }

    /// <summary>
    /// Rarity border + rarity shape icon (readable without color). The owner's background image becomes the colored border;
    /// an inset inner panel (first child, under the content) restores the card body.
    /// </summary>
    public sealed class RarityFrame : MonoBehaviour
    {
        public Image Border;
        public Image Inner;
        public Image Gem;

        public static RarityFrame Create(Image owner, float gemSize = 40, float thickness = 8)
        {
            var inner = UIKit.Image(owner.transform, "Inner", UIKit.Theme.panel, UIKit.Theme.rounded);
            inner.rectTransform.Stretch(thickness, thickness, thickness, thickness);
            inner.transform.SetAsFirstSibling();
            var f = inner.gameObject.AddComponent<RarityFrame>();
            f.Border = owner;
            f.Inner = inner;
            f.Gem = UIKit.Image(inner.transform, "Gem", Color.white, UIKit.Theme.circle);
            f.Gem.rectTransform.Anchor(new Vector2(0, 1), new Vector2(gemSize, gemSize), new Vector2(10, -10), new Vector2(0, 1));
            return f;
        }

        public void Set(Rarity rarity, Color color)
        {
            Border.color = color;
            Inner.color = Color.Lerp(UIKit.Theme.panel, color, 0.12f);
            Gem.enabled = true;
            Gem.sprite = UIKit.Theme.RarityShape(rarity);
            Gem.color = UIKit.Theme.RarityGem(rarity) != null ? Color.white : Color.Lerp(color, Color.white, 0.35f);
        }

        public void SetNone()
        {
            Border.color = UIKit.Theme.panelAlt;
            Inner.color = UIKit.Theme.panel;
            Gem.enabled = false;
        }
    }

    /// <summary>Horizontal fill bar with "have/need" label.</summary>
    public sealed class ProgressBar : MonoBehaviour
    {
        public Image Fill;
        public TextMeshProUGUI Label;

        public static ProgressBar Create(Transform parent, float height = 40, Color? fill = null)
        {
            var bg = UIKit.Image(parent, "ProgressBar", UIKit.Theme.panelAlt, UIKit.Theme.barBackground != null ? UIKit.Theme.barBackground : UIKit.Theme.rounded);
            bg.Size(preferredHeight: height);
            var bar = bg.gameObject.AddComponent<ProgressBar>();
            bar.Fill = UIKit.Image(bg.transform, "Fill", fill ?? UIKit.Theme.positive, UIKit.Theme.barFill != null ? UIKit.Theme.barFill : UIKit.Theme.rounded);
            bar.Fill.rectTransform.anchorMin = Vector2.zero;
            bar.Fill.rectTransform.anchorMax = new Vector2(0, 1);
            bar.Fill.rectTransform.pivot = new Vector2(0, 0.5f);
            bar.Fill.rectTransform.offsetMin = bar.Fill.rectTransform.offsetMax = Vector2.zero;
            bar.Label = UIKit.Text(bg.transform, "Label", "", height * 0.7f, UIKit.Theme.text, TextAlignmentOptions.Center, bold: true);
            bar.Label.rectTransform.Stretch();
            bar.Label.textWrappingMode = TextWrappingModes.NoWrap;
            bar.Label.overflowMode = TextOverflowModes.Overflow;
            return bar;
        }

        public void Set(float ratio, string label = null, Color? color = null)
        {
            ratio = Mathf.Clamp01(float.IsNaN(ratio) ? 0 : ratio);
            Fill.rectTransform.anchorMax = new Vector2(ratio, 1);
            Fill.enabled = ratio > 0.001f;
            if (color.HasValue) Fill.color = color.Value;
            if (label != null) Label.text = label;
        }

        public void Set(int have, int need, Color? color = null) => Set(need <= 0 ? 1 : (float)have / need, $"{have}/{need}", color);
    }

    /// <summary>"Label ........ value (+delta)" row.</summary>
    public sealed class StatRow : MonoBehaviour
    {
        public TextMeshProUGUI Name, Value, Delta;

        public static StatRow Create(Transform parent, string label, string value = "", float size = 0)
        {
            var row = UIKit.Rect("StatRow", parent);
            UIKit.HStack(row.gameObject, 12, 0, TextAnchor.MiddleLeft, expandWidth: false);
            row.Size(preferredHeight: (size > 0 ? size : UIKit.Theme.fontBody) * 1.5f);
            var s = row.gameObject.AddComponent<StatRow>();
            s.Name = UIKit.Text(row, "Name", label, size, UIKit.Theme.textDim);
            s.Name.Size(flexibleWidth: 1);
            s.Value = UIKit.Text(row, "Value", value, size, UIKit.Theme.text, TextAlignmentOptions.Right, bold: true);
            s.Value.Size(preferredWidth: 260);
            s.Delta = UIKit.Text(row, "Delta", "", size, UIKit.Theme.positive, TextAlignmentOptions.Right);
            s.Delta.Size(preferredWidth: 160);
            return s;
        }

        public void Set(string value, float delta = 0, string deltaText = null)
        {
            Value.text = value;
            if (Mathf.Abs(delta) < 0.0001f) { Delta.text = ""; return; }
            Delta.text = deltaText ?? (delta > 0 ? "+" + delta.ToString("0.#") : delta.ToString("0.#"));
            Delta.color = delta > 0 ? UIKit.Theme.positive : UIKit.Theme.negative;
        }
    }

    /// <summary>Row of mutually exclusive tab buttons.</summary>
    public sealed class Tabs : MonoBehaviour
    {
        private Button[] _buttons;
        public int Selected { get; private set; }
        public event Action<int> Changed;

        public static Tabs Create(Transform parent, string[] labels, int selected = 0, float height = 110)
        {
            var row = UIKit.Rect("Tabs", parent);
            UIKit.HStack(row.gameObject, 12);
            row.Size(preferredHeight: height);
            var tabs = row.gameObject.AddComponent<Tabs>();
            tabs._buttons = new Button[labels.Length];
            for (var i = 0; i < labels.Length; i++)
            {
                var index = i;
                tabs._buttons[i] = UIKit.Button(row, "Tab" + i, labels[i], () => tabs.Select(index), UIKit.ButtonStyle.Ghost, UIKit.Theme.fontBody);
                var label = tabs._buttons[i].GetComponentInChildren<TextMeshProUGUI>();
                label.fontSizeMin = 16;
                ((RectTransform)label.transform).Stretch(6, 6, 6, 6);
            }
            tabs.Select(selected, notify: false);
            return tabs;
        }

        public void Select(int index, bool notify = true)
        {
            Selected = index;
            for (var i = 0; i < _buttons.Length; i++)
            {
                ((Image)_buttons[i].targetGraphic).color = i == index ? UIKit.Theme.secondary : UIKit.Theme.panelAlt;
            }
            if (notify) Changed?.Invoke(index);
        }
    }

    /// <summary>Row of star sprites (the UI font has no ★ glyph): filled = earned, dim = missing.</summary>
    public sealed class StarRow : MonoBehaviour
    {
        private Image[] _stars = new Image[0];
        private float _size;

        public static StarRow Create(Transform parent, float size = 40, int max = 5)
        {
            var row = UIKit.Rect("Stars", parent);
            UIKit.HStack(row.gameObject, 4, 0, TextAnchor.MiddleCenter, expandWidth: false);
            row.Size(preferredHeight: size);
            var sr = row.gameObject.AddComponent<StarRow>();
            sr._size = size;
            sr.EnsureCount(max);
            return sr;
        }

        private void EnsureCount(int max)
        {
            if (_stars.Length == max) return;
            foreach (var s in _stars) Destroy(s.gameObject);
            _stars = new Image[max];
            for (var i = 0; i < max; i++)
            {
                _stars[i] = UIKit.Image(transform, "Star" + i, Color.white, UIKit.Theme.starFull != null ? UIKit.Theme.starFull : UIKit.Theme.star);
                _stars[i].Size(_size, _size);
            }
        }

        public void Set(int filled, int max)
        {
            EnsureCount(Mathf.Max(0, max));
            var th = UIKit.Theme;
            var skinned = th.starFull != null && th.starEmpty != null;
            for (var i = 0; i < _stars.Length; i++)
            {
                if (skinned)
                {
                    _stars[i].sprite = i < filled ? th.starFull : th.starEmpty;
                    _stars[i].color = Color.white;
                }
                else _stars[i].color = i < filled ? th.warning : th.textDim.WithAlpha(0.3f);
            }
        }
    }

    /// <summary>
    /// Hero card. Skinned (Theme.cardFrames set): rarity card back, 3:4 hero art, ornate frame with name plate, rarity gem,
    /// class icon. Unskinned: rarity border + tinted round portrait with initial. Both: stars, shard bar, lock, red dot.
    /// </summary>
    public sealed class HeroCard : MonoBehaviour
    {
        public Image Background, Portrait, FrameArt, ClassIcon;
        public TextMeshProUGUI Initial, Name, Power;
        public StarRow Stars;
        public RarityFrame Frame;
        public ProgressBar Shards;
        public GameObject LockOverlay;
        public RedDot Dot;
        public Button Button;
        public bool Skinned { get; private set; }

        public const float Width = 300, Height = 400;

        public static HeroCard Create(Transform parent, Action onClick = null)
        {
            var th = UIKit.Theme;
            var bg = UIKit.Image(parent, "HeroCard", th.panel, th.rounded, raycast: true);
            bg.Size(Width, Height);
            var c = bg.gameObject.AddComponent<HeroCard>();
            c.Background = bg;
            c.Button = bg.gameObject.AddComponent<Button>();
            c.Button.targetGraphic = bg;
            if (onClick != null) c.Button.onClick.AddListener(() => onClick());
            bg.gameObject.AddComponent<ButtonFeedback>();
            c.Skinned = th.CardFrame(Rarity.Common) != null;
            c.Frame = RarityFrame.Create(bg);
            if (c.Skinned)
            {
                c.Frame.Inner.enabled = false;
                c.Portrait = UIKit.Image(bg.transform, "Portrait", Color.white, th.circle);
                c.Portrait.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(280, 373), new Vector2(0, -15), new Vector2(0.5f, 1));
                c.FrameArt = UIKit.Image(bg.transform, "FrameArt", Color.white, th.CardFrame(Rarity.Common));
                c.FrameArt.rectTransform.Stretch();
                c.FrameArt.type = Image.Type.Simple;
                c.FrameArt.preserveAspect = false;
                c.Frame.Gem.transform.SetParent(bg.transform, false);
                c.Frame.Gem.rectTransform.Anchor(new Vector2(0, 1), new Vector2(46, 46), new Vector2(22, -25), new Vector2(0, 1));
                c.ClassIcon = UIKit.Image(bg.transform, "Class", Color.white, null);
                c.ClassIcon.rectTransform.Anchor(new Vector2(1, 1), new Vector2(46, 46), new Vector2(-22, -25), new Vector2(1, 1));
                c.Initial = UIKit.Text(c.Portrait.transform, "Initial", "", 1, Color.clear);
                c.Initial.gameObject.SetActive(false);
                c.Stars = StarRow.Create(bg.transform, 32);
                ((RectTransform)c.Stars.transform).Anchor(new Vector2(0.5f, 1), new Vector2(280, 36), new Vector2(0, -266), new Vector2(0.5f, 1));
                c.Name = UIKit.Text(bg.transform, "Name", "", 34, new Color(1f, 0.97f, 0.88f), TextAlignmentOptions.Center, bold: true);
                c.Name.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(250, 44), new Vector2(0, -303), new Vector2(0.5f, 1));
                c.Name.outlineWidth = 0.22f;
                c.Name.outlineColor = new Color32(20, 12, 24, 255);
                c.Power = UIKit.Text(bg.transform, "Power", "", 24, th.text, TextAlignmentOptions.Center, bold: true);
                c.Power.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(150, 30), new Vector2(0, -34), new Vector2(0.5f, 1));
                c.Power.outlineWidth = 0.25f;
                c.Power.outlineColor = new Color32(0, 0, 0, 230);
                c.Shards = ProgressBar.Create(bg.transform, 26);
                ((RectTransform)c.Shards.transform).Anchor(new Vector2(0.5f, 0), new Vector2(250, 26), new Vector2(0, 22), new Vector2(0.5f, 0));
            }
            else
            {
                c.Portrait = UIKit.Image(bg.transform, "Portrait", Color.white, th.circle);
                c.Portrait.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(190, 190), new Vector2(0, -26), new Vector2(0.5f, 1));
                c.Initial = UIKit.Text(c.Portrait.transform, "Initial", "", 96, new Color(0, 0, 0, 0.55f), TextAlignmentOptions.Center, bold: true);
                c.Initial.rectTransform.Stretch();
                c.Stars = StarRow.Create(bg.transform, 34);
                ((RectTransform)c.Stars.transform).Anchor(new Vector2(0.5f, 1), new Vector2(280, 40), new Vector2(0, -222), new Vector2(0.5f, 1));
                c.Name = UIKit.Text(bg.transform, "Name", "", 36, th.text, TextAlignmentOptions.Center, bold: true);
                c.Name.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(280, 50), new Vector2(0, -262), new Vector2(0.5f, 1));
                c.Power = UIKit.Text(bg.transform, "Power", "", 28, th.textDim, TextAlignmentOptions.Center);
                c.Power.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(280, 36), new Vector2(0, -308), new Vector2(0.5f, 1));
                c.Shards = ProgressBar.Create(bg.transform, 36);
                ((RectTransform)c.Shards.transform).Anchor(new Vector2(0.5f, 0), new Vector2(260, 36), new Vector2(0, 18), new Vector2(0.5f, 0));
            }
            c.Name.textWrappingMode = TextWrappingModes.NoWrap;
            c.Name.enableAutoSizing = true;
            c.Name.fontSizeMin = 18;
            c.Name.fontSizeMax = c.Name.fontSize;
            var lockImg = UIKit.Image(bg.transform, "Lock", th.locked.WithAlpha(c.Skinned ? 0.35f : 0.55f), th.rounded);
            lockImg.rectTransform.Stretch(c.Skinned ? 16 : 0, c.Skinned ? 16 : 0, c.Skinned ? 16 : 0, c.Skinned ? 104 : 60);
            c.LockOverlay = lockImg.gameObject;
            if (c.Skinned && th.iconLock != null)
                UIKit.Image(lockImg.transform, "LockIcon", Color.white, th.iconLock).rectTransform.Anchor(new Vector2(0.5f, 0.62f), new Vector2(84, 84), new Vector2(0, 54));
            var lockText = UIKit.Text(lockImg.transform, "LockText", "", 34, th.text, TextAlignmentOptions.Center, bold: true);
            lockText.rectTransform.Anchor(new Vector2(0.5f, c.Skinned ? 0.4f : 0.62f), new Vector2(260, 70));
            lockText.textWrappingMode = TextWrappingModes.NoWrap;
            lockText.enableAutoSizing = true;
            lockText.fontSizeMin = 18;
            lockText.fontSizeMax = 34;
            lockText.outlineWidth = 0.2f;
            lockText.outlineColor = new Color32(0, 0, 0, 220);
            c.Dot = RedDot.Attach(bg.transform, 44);
            return c;
        }

        public void Set(string name, Rarity rarity, Color rarityColor, Color tint, Sprite portrait, bool unlocked, int star, int maxStar, int shards, int shardsNeeded, string powerText, bool redDot)
        {
            var th = UIKit.Theme;
            var art = portrait != null;
            if (Skinned)
            {
                Background.sprite = th.CardBack(rarity) != null ? th.CardBack(rarity) : th.rounded;
                Background.type = th.CardBack(rarity) != null ? Image.Type.Simple : Image.Type.Sliced;
                Background.color = unlocked ? Color.white : new Color(0.55f, 0.55f, 0.6f);
                FrameArt.sprite = th.CardFrame(rarity);
                Frame.Gem.sprite = th.RarityShape(rarity);
                Frame.Gem.color = th.RarityGem(rarity) != null ? Color.white : rarityColor;
            }
            else Frame.Set(rarity, rarityColor);
            Portrait.sprite = art ? portrait : th.circle;
            Portrait.preserveAspect = true;
            // Real art is drawn untinted (locked heroes become a dark silhouette); placeholders keep the tint.
            var baseColor = art ? Color.white : tint;
            Portrait.color = unlocked ? baseColor : art ? new Color(0.12f, 0.1f, 0.16f, 0.92f) : Color.Lerp(tint, Color.black, 0.65f);
            Initial.text = art || string.IsNullOrEmpty(name) ? "" : name.Substring(0, 1).ToUpperInvariant();
            Name.text = name;
            Stars.gameObject.SetActive(unlocked);
            if (unlocked) Stars.Set(star, maxStar);
            Power.text = unlocked ? powerText : "";
            LockOverlay.SetActive(!unlocked);
            Shards.gameObject.SetActive(shardsNeeded > 0);
            if (shardsNeeded > 0) Shards.Set(shards, shardsNeeded, shards >= shardsNeeded ? th.positive : th.secondary);
            Dot.Show(redDot);
        }

        /// <summary>Class badge (skinned cards only).</summary>
        public void SetClass(HeroClass heroClass)
        {
            if (ClassIcon == null) return;
            ClassIcon.sprite = UIKit.Theme.ClassIcon(heroClass);
            ClassIcon.enabled = ClassIcon.sprite != null;
        }
    }

    /// <summary>Equipment card: rarity-tinted slot icon, rarity frame, "equipped" badge, red dot.</summary>
    public sealed class ItemCard : MonoBehaviour
    {
        public Image Background, Icon;
        public RarityFrame Frame;
        public TextMeshProUGUI Badge, Caption;
        public RedDot Dot;
        public Button Button;

        public static ItemCard Create(Transform parent, float size = 200, Action onClick = null)
        {
            var th = UIKit.Theme;
            var bg = UIKit.Image(parent, "ItemCard", th.panel, th.rounded, raycast: true);
            bg.Size(size, size);
            var c = bg.gameObject.AddComponent<ItemCard>();
            c.Background = bg;
            c.Button = bg.gameObject.AddComponent<Button>();
            c.Button.targetGraphic = bg;
            if (onClick != null) c.Button.onClick.AddListener(() => onClick());
            bg.gameObject.AddComponent<ButtonFeedback>();
            c.Frame = RarityFrame.Create(bg, size * 0.16f, 6);
            c.Icon = UIKit.Image(bg.transform, "Icon", Color.white, th.iconWeapon);
            c.Icon.rectTransform.Stretch(size * 0.18f, size * 0.14f, size * 0.18f, size * 0.22f);
            c.Caption = UIKit.Text(bg.transform, "Caption", "", size * 0.13f, th.text, TextAlignmentOptions.Bottom);
            c.Caption.rectTransform.Stretch(6, 0, 6, 6);
            c.Caption.textWrappingMode = TextWrappingModes.NoWrap;
            c.Badge = UIKit.Text(bg.transform, "Badge", "", size * 0.16f, th.warning, TextAlignmentOptions.TopRight, bold: true);
            c.Badge.rectTransform.Stretch(8, 6, 12, 8);
            c.Dot = RedDot.Attach(bg.transform, size * 0.18f);
            return c;
        }

        /// <summary><paramref name="icon"/> = the template's own art (drawn untinted); null = rarity-tinted slot icon.</summary>
        public void Set(EquipmentSlot slot, Rarity rarity, Color rarityColor, string caption, string badge, bool redDot, Sprite icon = null)
        {
            Frame.Set(rarity, rarityColor);
            Icon.sprite = icon != null ? icon : UIKit.Theme.SlotIcon(slot);
            Icon.color = icon != null ? Color.white : Color.Lerp(rarityColor, Color.white, 0.25f);
            Caption.text = caption ?? "";
            Badge.text = badge ?? "";
            Dot.Show(redDot);
        }

        public void SetEmpty(EquipmentSlot slot, string caption)
        {
            Frame.SetNone();
            Icon.sprite = UIKit.Theme.SlotIcon(slot);
            Icon.color = UIKit.Theme.textDim.WithAlpha(0.35f);
            Caption.text = caption ?? "";
            Badge.text = "";
            Dot.Show(false);
        }
    }

    /// <summary>Currency chip: icon + amount.</summary>
    public sealed class CurrencyChip : MonoBehaviour
    {
        public TextMeshProUGUI Amount;
        public Image Icon;

        public static CurrencyChip Create(Transform parent, Color iconColor)
        {
            var bg = UIKit.Panel(parent, "CurrencyChip", UIKit.Theme.panelAlt);
            UIKit.HStack(bg.gameObject, 12, 14, TextAnchor.MiddleLeft, expandWidth: false);
            bg.Size(preferredWidth: 300, preferredHeight: 90);
            var chip = bg.gameObject.AddComponent<CurrencyChip>();
            var gold = UIKit.Theme.iconGold;
            chip.Icon = UIKit.Image(bg.transform, "Icon", gold != null ? Color.white : iconColor, gold != null ? gold : UIKit.Theme.circle);
            chip.Icon.Size(56, 56);
            chip.Amount = UIKit.Text(bg.transform, "Amount", "0", UIKit.Theme.fontLarge, UIKit.Theme.text, TextAlignmentOptions.Right, bold: true);
            chip.Amount.Size(preferredWidth: 200);
            chip.Amount.textWrappingMode = TextWrappingModes.NoWrap;
            return chip;
        }

        public void Set(long amount) => Amount.text = FormatAmount(amount);

        public static string FormatAmount(long v)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (v >= 1_000_000_000) return (v / 1_000_000_000d).ToString(v >= 10_000_000_000 ? "0" : "0.#", inv) + "B";
            if (v >= 1_000_000) return (v / 1_000_000d).ToString(v >= 10_000_000 ? "0" : "0.#", inv) + "M";
            if (v >= 100_000) return (v / 1_000) + "K";
            return v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
