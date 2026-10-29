using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>All items: slot filter tabs, pooled 4-column grid, item popup, bulk sell.</summary>
    public sealed class InventoryScreen : ScreenBase
    {
        private InventoryViewModel _vm;
        private RecycleList _list;
        private TextMeshProUGUI _count;
        private IReadOnlyList<ItemInfo> _items = new List<ItemInfo>();
        private static readonly EquipmentSlot?[] Filters = { null, EquipmentSlot.Weapon, EquipmentSlot.Helmet, EquipmentSlot.Chest, EquipmentSlot.Gloves, EquipmentSlot.Boots, EquipmentSlot.Ring };

        public override string TabId => "inventory";

        protected override void Build()
        {
            _vm = new InventoryViewModel(Ctx.Session, UIScreensSetup.Dots);
            _vm.Changed += _ => Refresh();
            var body = ScreenKit.Header(Ctx, Root, Ctx.T("tab_inventory"));
            var tabs = Tabs.Create(body, Filters.Select(f => f.HasValue ? ScreenKit.SlotName(Ctx, f.Value) : Ctx.T("all")).ToArray(), 0, 100);
            var trt = (RectTransform)tabs.transform;
            trt.TopBand(100, 0);
            trt.offsetMin = new Vector2(16, trt.offsetMin.y);
            trt.offsetMax = new Vector2(-16, trt.offsetMax.y);
            foreach (var t in tabs.GetComponentsInChildren<TextMeshProUGUI>()) { t.fontSizeMax = 28; t.fontSize = 28; t.fontSizeMin = 14; }
            tabs.Changed += i => _vm.SetSlotFilter(Filters[i]);
            _count = UIKit.Text(body, "Count", "", Th.fontSmall, Th.textDim);
            ((RectTransform)_count.transform).Anchor(new Vector2(0, 1), new Vector2(500, 70), new Vector2(Th.padding, -110), new Vector2(0, 1));
            if (_vm.SellEnabled)
            {
                var bulk = UIKit.Button(body, "Bulk", Ctx.T("bulk_sell"), BulkSell, UIKit.ButtonStyle.Ghost, Th.fontSmall);
                ((RectTransform)bulk.transform).Anchor(new Vector2(1, 1), new Vector2(380, 80), new Vector2(-Th.padding, -105), new Vector2(1, 1));
            }
            _list = RecycleList.Create(body, 230, parent => (RectTransform)ItemCard.Create(parent, 230).transform, Bind, columns: 4, spacing: 18, padding: Th.padding);
            ((RectTransform)_list.transform).Stretch(0, 190, 0, 0);
            Refresh();
        }

        private void Bind(int index, RectTransform rt)
        {
            var item = _items[index];
            var card = rt.GetComponent<ItemCard>();
            card.Set(item.Slot, item.Rarity, ScreenKit.RarityColor(Ctx, item.Rarity), ScreenKit.ItemName(Ctx, item), item.IsEquipped ? "E" : "", _vm.Badge(item.InstanceId));
            card.Button.onClick.RemoveAllListeners();
            card.Button.onClick.AddListener(() => ItemPopup.Show(Ctx, _vm, item.InstanceId));
        }

        public override void OnShow() => Refresh();

        private void Refresh()
        {
            if (this == null) return;
            _items = _vm.Items;
            _count.text = _vm.Limit > 0 ? $"{_vm.Count}/{_vm.Limit}" : Ctx.F("item_count", _vm.Count);
            _list.SetCount(_items.Count);
        }

        private void BulkSell()
        {
            var candidates = _vm.BulkSellCandidates(Rarity.Uncommon);
            if (candidates.Count == 0) { Ctx.Manager.Toast(Ctx.T("nothing_to_sell")); return; }
            var value = candidates.Sum(c => _vm.SellValue(c.InstanceId));
            ConfirmPopup.Show(Ctx, Ctx.T("bulk_sell"), Ctx.F("bulk_sell_body", candidates.Count, value), () =>
            {
                var n = _vm.BulkSell(Rarity.Uncommon);
                Ctx.Manager.Toast(Ctx.F("sold_n", n));
            }, danger: true);
        }

        public override void OnClosed() => _vm?.Dispose();
    }

    /// <summary>Item details: stats, rarity, owner, sell.</summary>
    public sealed class ItemPopup : PopupBase
    {
        private InventoryViewModel _vm;
        private string _id;

        public static void Show(UIContext ctx, InventoryViewModel vm, string id) => ctx.Manager.ShowPopup<ItemPopup>(p => { p._vm = vm; p._id = id; });

        protected override void Build()
        {
            var info = Ctx.Session.Inventory.Get(_id);
            if (!info.HasValue) { Close(); return; }
            var item = info.Value;
            var head = UIKit.Rect("Head", Panel);
            UIKit.HStack(head.gameObject, Th.spacing, 0, TextAnchor.MiddleLeft, expandWidth: false);
            head.Size(preferredHeight: 220);
            ItemCard.Create(head, 200).Set(item.Slot, item.Rarity, ScreenKit.RarityColor(Ctx, item.Rarity), "", "", false);
            var col = UIKit.Rect("Col", head);
            UIKit.VStack(col.gameObject, 6, 0, TextAnchor.MiddleLeft);
            col.Size(preferredWidth: 620);
            UIKit.Text(col, "Name", ScreenKit.ItemName(Ctx, item), Th.fontLarge, ScreenKit.RarityColor(Ctx, item.Rarity), TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 70);
            UIKit.Text(col, "Kind", $"{ScreenKit.RarityName(Ctx, item.Rarity)} · {ScreenKit.SlotName(Ctx, item.Slot)}", Th.fontBody, Th.textDim).Size(preferredHeight: 56);
            if (item.IsEquipped) UIKit.Text(col, "By", Ctx.F("equipped_by", ScreenKit.HeroName(Ctx, item.EquippedBy)), Th.fontBody, Th.warning).Size(preferredHeight: 56);
            foreach (var m in _vm.ItemStats(_id)) UIKit.Text(Panel, "Stat", ScreenKit.FormatModifier(Ctx, m), Th.fontBody, Th.text).Size(preferredHeight: 56);
            var row = UIKit.Rect("Buttons", Panel);
            UIKit.HStack(row.gameObject, Th.spacing);
            row.Size(preferredHeight: Th.buttonHeight);
            UIKit.Button(row, "Close", Ctx.T("close"), Close, UIKit.ButtonStyle.Ghost);
            if (_vm.SellEnabled && !item.IsEquipped)
                UIKit.Button(row, "Sell", Ctx.F("sell_for", _vm.SellValue(_id)), () =>
                {
                    if (ScreenKit.Report(Ctx, _vm.Sell(_id))) { Ctx.PlaySfx("sell"); Close(); }
                }, UIKit.ButtonStyle.Danger);
        }
    }

    /// <summary>Victory/defeat, stars, animated reward reveal, unlock celebration, next/retry/home.</summary>
    public sealed class ResultScreen : ScreenBase
    {
        private ResultViewModel _vm;

        public override bool ShowTabBar => false;
        public override string TabId => "campaign";

        protected override void Build()
        {
            _vm = new ResultViewModel(Ctx.Session, (FightCompletion)Args);
            var body = UIKit.Rect("Body", Root).Stretch(Th.padding, Th.padding, Th.padding, Th.padding);
            var title = UIKit.Text(body, "Banner", Ctx.T(_vm.Victory ? "victory" : "defeat"), 120, _vm.Victory ? Th.primary : Th.negative, TextAlignmentOptions.Center, bold: true);
            ((RectTransform)title.transform).TopBand(170, 10);
            Tween.Scale(title.transform, 0.4f, 1f, 0.45f, Ease.OutBack);
            if (_vm.Victory && Ctx.Session.Db.Rules.StarsEnabled)
            {
                var stars = StarRow.Create(body, 110, 3);
                ((RectTransform)stars.transform).TopBand(120, 190);
                stars.Set(0, 3);
                for (var i = 1; i <= _vm.Stars; i++)
                {
                    var n = i;
                    Tween.To(stars, 0, 1, 0.01f, _ => stars.Set(n, 3), delay: 0.25f * i);
                }
                if (_vm.NewBest && _vm.Completion.PreviousBestStars > 0)
                    UIKit.Text(body, "Best", Ctx.T("new_best"), Th.fontBody, Th.positive, TextAlignmentOptions.Center).rectTransform.TopBand(60, 315);
            }
            var info = UIKit.Text(body, "Info", _vm.Victory ? Ctx.Session.Db.Rules.StarsEnabled ? "" : "" : Ctx.T("defeat_hint"), Th.fontBody, Th.textDim, TextAlignmentOptions.Center);
            ((RectTransform)info.transform).TopBand(70, 320);

            var scroll = UIKit.ScrollView(body, "Rewards", out var content, 16, 0);
            ((RectTransform)scroll.transform).Stretch(0, 400, 0, 380);
            var lines = _vm.Lines;
            if (lines.Count == 0) UIKit.Text(content, "None", Ctx.T("no_rewards"), Th.fontBody, Th.textDim, TextAlignmentOptions.Center).Size(preferredHeight: 80);
            for (var i = 0; i < lines.Count; i++) RewardRow(content, lines[i], 0.4f + i * 0.12f);
            if (_vm.ChapterCompleted != null)
                UIKit.Text(content, "Chapter", Ctx.F("chapter_cleared", Ctx.T(Ctx.Session.Db.GetChapter(_vm.ChapterCompleted).NameKey)), Th.fontLarge, Th.primary, TextAlignmentOptions.Center, bold: true).Size(preferredHeight: 90);

            var buttons = UIKit.Rect("Buttons", body).BottomBand(360);
            UIKit.VStack(buttons.gameObject, Th.spacing, 0, TextAnchor.LowerCenter);
            if (_vm.HasNextFight)
                TutorialAnchor.Mark(UIKit.Button(buttons, "Next", Ctx.T("next_fight_button"), () => GoTo(_vm.NextFightId)), "result_next");
            var row = UIKit.Rect("Row", buttons);
            UIKit.HStack(row.gameObject, Th.spacing);
            row.Size(preferredHeight: Th.buttonHeight);
            UIKit.Button(row, "Retry", Ctx.T(_vm.Victory ? "replay" : "retry"), () => GoTo(_vm.Completion.FightId), UIKit.ButtonStyle.Secondary);
            TutorialAnchor.Mark(UIKit.Button(row, "Home", Ctx.T("tab_home"), () => Ctx.Manager.PopToRoot(), UIKit.ButtonStyle.Ghost), "result_home");

            Ctx.PlaySfx(_vm.Victory ? "victory" : "defeat");
            Ctx.Haptics.Play(_vm.Victory ? HapticKind.Success : HapticKind.Warning);
        }

        public override void OnShow()
        {
            // After the reveal: post-fight story, then hero-unlock celebrations.
            if (_shownExtras) return;
            _shownExtras = true;
            var post = _vm.Victory ? Ctx.Session.Progress.GetBeats(_vm.Completion.FightId, post: true) : new List<StoryBeatDef>();
            StoryPopup.Show(Ctx, post, () =>
            {
                foreach (var h in _vm.NowUnlockable) Ctx.Manager.Toast(Ctx.F("hero_unlockable", ScreenKit.HeroName(Ctx, h)));
            });
        }

        private bool _shownExtras;

        private void GoTo(string fightId)
        {
            Ctx.Manager.PopToRoot();
            Ctx.Manager.Push<FightPreviewScreen>(fightId);
        }

        private void RewardRow(Transform parent, RewardLine l, float delay)
        {
            var panel = UIKit.Panel(parent, "Reward", Th.panel);
            UIKit.HStack(panel.gameObject, Th.spacing, 16, TextAnchor.MiddleLeft, expandWidth: false);
            panel.Size(preferredHeight: 150);
            var text = "";
            Color color = Th.text;
            switch (l.Type)
            {
                case RewardLine.Kind.Currency:
                    UIKit.Image(panel.transform, "Icon", Th.warning, Th.circle).Size(100, 100);
                    text = $"+{CurrencyChip.FormatAmount(l.Amount)} {Ctx.T("currency_" + l.Id + "_name")}";
                    break;
                case RewardLine.Kind.Shards:
                    var portrait = ScreenKit.Portrait(Ctx, panel.transform, l.Id, 100);
                    var col = UIKit.Rect("Col", panel.transform);
                    UIKit.VStack(col.gameObject, 4, 0, TextAnchor.MiddleLeft);
                    col.Size(preferredWidth: 700);
                    UIKit.Text(col, "Name", $"{ScreenKit.HeroName(Ctx, l.Id)}  +{Ctx.Plural("shards_count", l.Amount)}", Th.fontBody, ScreenKit.RarityColor(Ctx, l.Rarity), TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 54);
                    if (l.Converted) UIKit.Text(col, "Conv", Ctx.T("shards_converted"), Th.fontSmall, Th.textDim).Size(preferredHeight: 44);
                    else if (l.ShardsNeeded > 0)
                    {
                        var bar = ProgressBar.Create(col, 40);
                        bar.Set(l.ShardsBefore, l.ShardsNeeded);
                        Tween.To(bar, l.ShardsBefore, l.ShardsAfter, 0.6f, v => bar.Set(Mathf.RoundToInt(v), l.ShardsNeeded, v >= l.ShardsNeeded ? Th.positive : Th.secondary), Ease.OutCubic, delay: delay + 0.2f);
                    }
                    if (l.NowUnlockable) UIKit.Text(col, "Unlock", Ctx.T("unlock_ready"), Th.fontSmall, Th.positive, TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 44);
                    else if (l.StarUpAvailable) UIKit.Text(col, "Star", Ctx.T("starup_ready"), Th.fontSmall, Th.positive, TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 44);
                    break;
                case RewardLine.Kind.Item:
                    var card = ItemCard.Create(panel.transform, 120);
                    if (l.Item.HasValue)
                    {
                        var it = l.Item.Value;
                        card.Set(it.Slot, it.Rarity, ScreenKit.RarityColor(Ctx, it.Rarity), "", "", l.Better);
                        text = ScreenKit.ItemName(Ctx, it) + (l.Better ? "  " + Ctx.T("better_item") : "");
                    }
                    else text = Ctx.T("item_auto_sold");
                    color = ScreenKit.RarityColor(Ctx, l.Rarity);
                    var glow = UIKit.Image(card.transform, "Glow", color.WithAlpha(0.5f), Th.softDot);
                    glow.rectTransform.Stretch(-40, -40, -40, -40);
                    glow.transform.SetAsFirstSibling();
                    break;
            }
            if (text.Length > 0) UIKit.Text(panel.transform, "Text", text, Th.fontBody, color, TextAlignmentOptions.Left, bold: true).Size(preferredWidth: 700);
            var g = panel.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0;
            Tween.Fade(g, 1, 0.25f, null, delay);
            Tween.Scale(panel.transform, 0.85f, 1f, 0.3f, Ease.OutBack, null, delay);
            Tween.To(panel, 0, 1, 0.01f, _ => Ctx.PlaySfx("reward"), delay: delay);
        }

        public override bool HandleBack()
        {
            Ctx.Manager.PopToRoot();
            return true;
        }

        public override void OnClosed() => _vm?.Dispose();
    }

    /// <summary>Volumes, language, vibration, motion, battle defaults, reset (double confirm), credits, privacy, version.</summary>
    public sealed class SettingsScreen : ScreenBase
    {
        private SettingsViewModel _vm;
        private RectTransform _content;

        public override string TabId => "settings";

        protected override void Build()
        {
            _vm = new SettingsViewModel(Ctx.Session);
            var body = ScreenKit.Header(Ctx, Root, Ctx.T("tab_settings"));
            var scroll = UIKit.ScrollView(body, "Scroll", out _content, Th.spacing, Th.padding);
            ((RectTransform)scroll.transform).Stretch();
            Rebuild();
        }

        private void Rebuild()
        {
            if (this == null) return;
            UIKit.Clear(_content);
            var s = _vm.Settings;
            ScreenKit.Slider(Ctx, _content, Ctx.T("music_volume"), s.MusicVolume, v => s.MusicVolume = v);
            ScreenKit.Slider(Ctx, _content, Ctx.T("sfx_volume"), s.SfxVolume, v => s.SfxVolume = v);
            var lang = UIKit.Button(_content, "Language", Ctx.F("language_value", Ctx.T("language_name")), CycleLanguage, UIKit.ButtonStyle.Ghost);
            TutorialAnchor.Mark(lang, "settings_language");
            ScreenKit.Toggle(Ctx, _content, Ctx.T("vibration"), () => s.Vibration, v => s.Vibration = v);
            ScreenKit.Toggle(Ctx, _content, Ctx.T("screen_shake"), () => s.ScreenShake, v => s.ScreenShake = v);
            ScreenKit.Toggle(Ctx, _content, Ctx.T("reduce_motion"), () => s.ReduceMotion, v => { s.ReduceMotion = v; Tween.ReduceMotion = v; });
            ScreenKit.Toggle(Ctx, _content, Ctx.T("auto_skills"), () => s.AutoSkill, v => s.AutoSkill = v);
            Button speed = null;
            speed = UIKit.Button(_content, "Speed", Ctx.F("battle_speed_value", s.BattleSpeed), () => { _vm.CycleBattleSpeed(); speed.SetLabel(Ctx.F("battle_speed_value", s.BattleSpeed)); }, UIKit.ButtonStyle.Ghost);
            UIKit.Button(_content, "Credits", Ctx.T("credits"), () => MessagePopup.Show(Ctx, Ctx.T("credits"), Ctx.T("credits_body")), UIKit.ButtonStyle.Ghost);
            UIKit.Button(_content, "Privacy", Ctx.T("privacy_policy"), () => MessagePopup.Show(Ctx, Ctx.T("privacy_policy"), Ctx.T("privacy_body")), UIKit.ButtonStyle.Ghost);
            UIKit.Button(_content, "DebugInfo", Ctx.T("copy_debug_info"), () =>
            {
                GUIUtility.systemCopyBuffer = Ctx.DebugInfo();
                Ctx.Manager.Toast(Ctx.T("debug_info_copied"));
            }, UIKit.ButtonStyle.Ghost);
            UIKit.Button(_content, "Reset", Ctx.T("reset_progress"), ResetProgress, UIKit.ButtonStyle.Danger);
            UIKit.Text(_content, "Version", Ctx.F("version_value", Ctx.AppVersion), Th.fontSmall, Th.textDim, TextAlignmentOptions.Center).Size(preferredHeight: 60);
        }

        private void CycleLanguage()
        {
            if (!(Ctx.L is TableLocalizer loc)) return;
            var langs = loc.Languages.ToList();
            var next = langs[(langs.IndexOf(loc.Language) + 1) % langs.Count];
            _vm.Settings.Language = next;
            loc.SetLanguage(next);
            // Rebuild every visible text (chrome + stack) in the new language.
            UIScreensSetup.ShowHome(Ctx);
            Ctx.Manager.Push<SettingsScreen>();
        }

        private void ResetProgress()
        {
            ConfirmPopup.Show(Ctx, Ctx.T("reset_progress"), Ctx.T("reset_confirm_1"), () =>
                ConfirmPopup.Show(Ctx, Ctx.T("reset_progress"), Ctx.T("reset_confirm_2"), () =>
                {
                    _vm.ResetProgress();
                    UIScreensSetup.ShowHome(Ctx);
                    Ctx.Manager.Toast(Ctx.T("progress_reset"));
                }, danger: true), danger: true);
        }

        public override void OnClosed() => _vm?.Dispose();
    }
}
