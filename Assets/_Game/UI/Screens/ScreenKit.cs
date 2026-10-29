using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Marks a UI element the tutorial can point at (step 18). Registered by id while active; reports taps.</summary>
    public sealed class TutorialAnchor : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        public static readonly Dictionary<string, TutorialAnchor> Active = new Dictionary<string, TutorialAnchor>();
        /// <summary>Raised when any anchor is tapped (before or after its Button handler – both receive the click).</summary>
        public static event Action<string> Tapped;
        public string Id;

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
        {
            var sel = GetComponent<Selectable>();
            if (sel != null && !sel.IsInteractable()) return;
            Tapped?.Invoke(Id);
        }

        public static TutorialAnchor Mark(Component c, string id)
        {
            var a = c.gameObject.AddComponent<TutorialAnchor>();
            a.Id = id;
            if (a.isActiveAndEnabled) Active[id] = a;
            return a;
        }

        private void OnEnable() { if (!string.IsNullOrEmpty(Id)) Active[Id] = this; }
        private void OnDisable() { if (!string.IsNullOrEmpty(Id) && Active.TryGetValue(Id, out var a) && a == this) Active.Remove(Id); }
        public RectTransform Rect => (RectTransform)transform;
    }

    /// <summary>Shared lookups and building blocks for screens.</summary>
    public static class ScreenKit
    {
        public const float HeaderHeight = 130;

        /// <summary>Header band (back button when the stack has more than one screen) and a content rect below it.</summary>
        public static RectTransform Header(UIContext ctx, RectTransform root, string title, Action onBack = null, bool showBack = true)
        {
            var th = ctx.Theme;
            var header = UIKit.Rect("Header", root).TopBand(HeaderHeight);
            // Built before the screen joins the stack: any existing screen means there is somewhere to go back to.
            if (showBack && ctx.Manager.Screens.Count > 0)
            {
                var back = UIKit.Button(header, "Back", "<", onBack ?? ctx.Manager.Pop, UIKit.ButtonStyle.Ghost, th.fontTitle);
                ((RectTransform)back.transform).Anchor(new Vector2(0, 0.5f), new Vector2(130, 110), new Vector2(th.padding, 0), new Vector2(0, 0.5f));
                TutorialAnchor.Mark(back, "back");
            }
            var t = UIKit.Text(header, "Title", title, th.fontTitle, th.text, TextAlignmentOptions.Center, bold: true);
            t.rectTransform.Stretch(180, 0, 180, 0);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMin = 32;
            t.fontSizeMax = th.fontTitle;
            return UIKit.Rect("Body", root).Stretch(0, HeaderHeight, 0, 0);
        }

        public static string HeroName(UIContext ctx, string heroId) => ctx.Session.Db.TryGetHero(heroId, out var h) ? ctx.T(h.NameKey) : heroId;
        public static string EnemyName(UIContext ctx, string id) => ctx.Session.Db.TryGetEnemy(id, out var e) ? ctx.T(e.NameKey) : id;
        public static string RarityName(UIContext ctx, Rarity r) => ctx.T("rarity_" + r.ToString().ToLowerInvariant());
        public static string SlotName(UIContext ctx, EquipmentSlot s) => ctx.T("slot_" + s.ToString().ToLowerInvariant());
        public static string StatName(UIContext ctx, StatType s) => ctx.T("stat_" + s.ToString().ToLowerInvariant());
        public static string ClassName(UIContext ctx, HeroClass c) => ctx.T("class_" + c.ToString().ToLowerInvariant());
        public static Color RarityColor(UIContext ctx, Rarity r) => ctx.Config.GetRarityColor(r);

        public static string ItemName(UIContext ctx, ItemInfo item) => ctx.T(item.Template.NameKey);

        public static string FormatStat(StatType s, float v) =>
            s == StatType.CritChance ? (v * 100).ToString("0.#") + "%" : s == StatType.CritDamage ? "×" + v.ToString("0.00") : Mathf.RoundToInt(v).ToString();

        public static string FormatModifier(UIContext ctx, StatModifier m) =>
            m.Kind == ModifierKind.Percent ? $"{StatName(ctx, m.Stat)} +{m.Value * 100:0.#}%" :
            m.Stat == StatType.CritChance ? $"{StatName(ctx, m.Stat)} +{m.Value * 100:0.#}%" :
            m.Stat == StatType.CritDamage ? $"{StatName(ctx, m.Stat)} +{m.Value:0.00}" : $"{StatName(ctx, m.Stat)} +{m.Value:0.#}";

        public static void SetHeroCard(UIContext ctx, HeroCard card, HeroEntry e)
        {
            var asset = ctx.Config.GetHeroAsset(e.Def.Id);
            card.Set(ctx.T(e.Def.NameKey), e.Def.Rarity, RarityColor(ctx, e.Def.Rarity), asset != null ? asset.tint : Color.white, asset != null ? asset.portrait : null,
                e.Unlocked, e.Star, e.MaxStar, e.Shards, e.ShardsNeeded, ctx.F("power_value", e.Power), e.Badge);
            card.LockOverlay.GetComponentInChildren<TextMeshProUGUI>().text = e.ShardsNeeded > 0 && e.Shards >= e.ShardsNeeded ? ctx.T("unlock_ready") : ctx.T("locked");
        }

        /// <summary>Small round hero portrait with initial (party slots, story).</summary>
        public static Image Portrait(UIContext ctx, Transform parent, string heroId, float size)
        {
            var asset = heroId != null ? ctx.Config.GetHeroAsset(heroId) : null;
            var img = UIKit.Image(parent, "Portrait", asset != null ? asset.tint : ctx.Theme.panelAlt, asset != null && asset.portrait != null ? asset.portrait : ctx.Theme.circle);
            img.Size(size, size);
            var name = heroId != null ? HeroName(ctx, heroId) : "";
            var t = UIKit.Text(img.transform, "Initial", name.Length > 0 ? name.Substring(0, 1) : "+", size * 0.5f, new Color(0, 0, 0, 0.55f), TextAlignmentOptions.Center, bold: true);
            t.rectTransform.Stretch();
            return img;
        }

        /// <summary>Shows a failed command as a toast with its friendly message.</summary>
        public static bool Report(UIContext ctx, CommandResult r)
        {
            if (!r.Ok)
            {
                ctx.Manager.Toast(ctx.T(r.MessageKey));
                ctx.Haptics.Play(HapticKind.Warning);
            }
            return r.Ok;
        }

        public static Slider Slider(UIContext ctx, Transform parent, string label, float value, Action<float> onChange)
        {
            var th = ctx.Theme;
            var row = UIKit.Rect("SliderRow", parent);
            UIKit.HStack(row.gameObject, th.spacing, 0, TextAnchor.MiddleLeft, expandWidth: false);
            row.Size(preferredHeight: th.minTouchTarget);
            UIKit.Text(row, "Label", label, th.fontBody, th.text).Size(preferredWidth: 360);
            var bg = UIKit.Image(row, "Slider", th.panelAlt, th.rounded, raycast: true);
            bg.Size(flexibleWidth: 1, preferredHeight: 40);
            var slider = bg.gameObject.AddComponent<Slider>();
            var fillArea = UIKit.Rect("FillArea", bg.transform).Stretch();
            var fill = UIKit.Image(fillArea, "Fill", th.secondary, th.rounded);
            fill.rectTransform.Stretch();
            var handleArea = UIKit.Rect("HandleArea", bg.transform).Stretch(30, 0, 30, 0);
            var handle = UIKit.Image(handleArea, "Handle", th.text, th.circle, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(70, 70);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v => onChange(v));
            return slider;
        }

        /// <summary>Label + on/off button.</summary>
        public static Button Toggle(UIContext ctx, Transform parent, string label, Func<bool> get, Action<bool> set)
        {
            var th = ctx.Theme;
            var row = UIKit.Rect("ToggleRow", parent);
            UIKit.HStack(row.gameObject, th.spacing, 0, TextAnchor.MiddleLeft, expandWidth: false);
            row.Size(preferredHeight: th.minTouchTarget);
            UIKit.Text(row, "Label", label, th.fontBody, th.text).Size(flexibleWidth: 1);
            Button b = null;
            void Refresh()
            {
                b.SetLabel(get() ? ctx.T("on") : ctx.T("off"));
                ((Image)b.targetGraphic).color = get() ? th.positive : th.panelAlt;
            }
            b = UIKit.Button(row, "Toggle", "", () => { set(!get()); Refresh(); }, UIKit.ButtonStyle.Ghost, th.fontBody);
            b.Size(preferredWidth: 220, preferredHeight: 110);
            Refresh();
            return b;
        }
    }
}
