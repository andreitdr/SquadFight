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
    /// <summary>All heroes (owned + locked) as cards in a pooled 3-column grid with sort tabs.</summary>
    public sealed class HeroesScreen : ScreenBase
    {
        private HeroListViewModel _vm;
        private RecycleList _list;
        private IReadOnlyList<HeroEntry> _items = new List<HeroEntry>();

        public override string TabId => "heroes";

        protected override void Build()
        {
            _vm = new HeroListViewModel(Ctx.Session, UIScreensSetup.Dots);
            _vm.Changed += _ => Refresh();
            var body = ScreenKit.Header(Ctx, Root, Ctx.T("tab_heroes"), maxWidth: ScreenKit.WideWidth);
            var tabs = Tabs.Create(body, new[] { Ctx.T("sort_rarity"), Ctx.T("sort_power"), Ctx.T("sort_name") });
            ((RectTransform)tabs.transform).TopBand(110, 0);
            ((RectTransform)tabs.transform).offsetMin = new Vector2(Th.padding, ((RectTransform)tabs.transform).offsetMin.y);
            ((RectTransform)tabs.transform).offsetMax = new Vector2(-Th.padding, ((RectTransform)tabs.transform).offsetMax.y);
            tabs.Changed += i => _vm.SetSort((HeroSort)i);
            _list = RecycleList.Create(body, HeroCard.Height, parent =>
            {
                var card = HeroCard.Create(parent);
                return (RectTransform)card.transform;
            }, Bind, columns: 3, spacing: Th.spacing, padding: Th.padding);
            ((RectTransform)_list.transform).Stretch(0, 130, 0, 0);
            Refresh();
        }

        private void Bind(int index, RectTransform item)
        {
            var e = _items[index];
            var card = item.GetComponent<HeroCard>();
            ScreenKit.SetHeroCard(Ctx, card, e);
            card.Button.onClick.RemoveAllListeners();
            card.Button.onClick.AddListener(() => Ctx.Manager.Push<HeroDetailScreen>(e.Def.Id));
            var anchor = item.TryGetComponent<TutorialAnchor>(out var existing) ? existing : item.gameObject.AddComponent<TutorialAnchor>();
            anchor.SetId("hero_" + e.Def.Id);
        }

        public override void OnShow() => Refresh();

        private void Refresh()
        {
            if (this == null) return;
            _items = _vm.Items;
            _list.SetCount(_items.Count);
        }

        public override void OnClosed() => _vm?.Dispose();
    }

    /// <summary>One hero: portrait, stars, unlock/star-up, stats breakdown, skills, 7 equipment slots, lore.</summary>
    public sealed class HeroDetailScreen : ScreenBase
    {
        private HeroDetailViewModel _vm;
        private RectTransform _body, _content;

        public override string TabId => "heroes";

        protected override void Build()
        {
            _vm = new HeroDetailViewModel(Ctx.Session, (string)Args);
            _vm.Changed += _ => Refresh();
            _body = ScreenKit.Header(Ctx, Root, Ctx.T(_vm.Def.NameKey));
            var scroll = UIKit.ScrollView(_body, "Scroll", out _content, Th.spacing, Th.padding);
            ((RectTransform)scroll.transform).Stretch();
            Refresh();
        }

        public override void OnShow()
        {
            Refresh();
            TipPopup.ShowOnce(Ctx, _vm.Unlocked ? (_vm.StarUpInfo.CanUpgrade ? "tip_starup" : null) : "tip_shards");
        }

        private void Refresh()
        {
            if (this == null) return;
            UIKit.Clear(_content);
            var def = _vm.Def;
            var asset = Ctx.Config.GetHeroAsset(def.Id);
            var rarityColor = ScreenKit.RarityColor(Ctx, def.Rarity);

            // Portrait block
            var top = UIKit.Rect("Top", _content);
            UIKit.HStack(top.gameObject, Th.spacing, 0, TextAnchor.MiddleLeft, expandWidth: false);
            top.Size(preferredHeight: 300);
            var frame = UIKit.Image(top, "Frame", rarityColor, Th.circle);
            frame.Size(290, 290);
            var art = ScreenKit.HeroIcon(asset);
            var baseColor = art != null ? Color.white : asset.tint;
            var portrait = UIKit.Image(frame.transform, "Portrait", _vm.Unlocked ? baseColor : Color.Lerp(baseColor, Color.black, 0.6f), art != null ? art : Th.circle);
            portrait.rectTransform.Stretch(12, 12, 12, 12);
            var initial = UIKit.Text(portrait.transform, "Initial", art != null ? "" : Ctx.T(def.NameKey).Substring(0, 1), 150, new Color(0, 0, 0, 0.5f), TextAlignmentOptions.Center, bold: true);
            initial.rectTransform.Stretch();
            if (_vm.Unlocked)
            {
                // Tap the portrait for a line of dialogue.
                var talk = frame.gameObject.AddComponent<Button>();
                talk.targetGraphic = frame;
                frame.raycastTarget = true;
                frame.gameObject.AddComponent<ButtonFeedback>();
                var heroId = def.Id;
                talk.onClick.AddListener(() => Say(heroId, HeroLines.Select));
            }
            var info = UIKit.Rect("Info", top);
            UIKit.VStack(info.gameObject, 8, 0, TextAnchor.MiddleLeft);
            info.Size(preferredWidth: 620);
            UIKit.Text(info, "Rarity", $"{ScreenKit.RarityName(Ctx, def.Rarity)} · {ScreenKit.ClassName(Ctx, def.Class)}", Th.fontBody, rarityColor, TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 60);
            if (_vm.Unlocked)
            {
                StarRow.Create(info, 56, _vm.MaxStar).Set(_vm.Star, _vm.MaxStar);
                UIKit.Text(info, "Power", Ctx.F("power_value", _vm.Power), Th.fontLarge, Th.text, TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 70);
            }
            else UIKit.Text(info, "Locked", Ctx.T("locked"), Th.fontLarge, Th.textDim).Size(preferredHeight: 70);

            BuildProgression();

            if (_vm.Unlocked && !_vm.InParty)
                TutorialAnchor.Mark(UIKit.Button(_content, "AddParty", Ctx.T("add_to_party"), () => ScreenKit.Report(Ctx, _vm.AddToParty()), UIKit.ButtonStyle.Secondary), "hero_add_party");

            // Stats
            Section("stats");
            var bd = _vm.Stats;
            foreach (var s in StatBlock.All)
            {
                var row = StatRow.Create(_content, ScreenKit.StatName(Ctx, s));
                var bonus = bd.Final[s] - bd.Base[s];
                row.Set(ScreenKit.FormatStat(s, bd.Final[s]), bonus, bonus > 0.0001f ? "+" + ScreenKit.FormatStat(s, bonus) : null);
            }
            UIKit.Text(_content, "Formula", Ctx.F("stat_breakdown", bd.StarMultiplier.ToString("0.00")), Th.fontSmall, Th.textDim).Size(preferredHeight: 50);

            // Skills
            Section("skills");
            SkillRow(_vm.BasicSkill, Ctx.T("skill_basic"));
            SkillRow(_vm.ActiveSkill, Ctx.T("skill_active"));
            SkillRow(_vm.PassiveSkill, Ctx.T("skill_passive"));

            // Equipment
            if (_vm.Unlocked)
            {
                Section("equipment");
                var grid = UIKit.Rect("Slots", _content);
                var g = grid.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(220, 220);
                g.spacing = new Vector2(Th.spacing, Th.spacing);
                g.childAlignment = TextAnchor.UpperCenter;
                grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                foreach (var slot in _vm.Slots)
                {
                    var index = slot.Index;
                    var card = ItemCard.Create(grid, 220, () => Ctx.Manager.Push<EquipmentPickerScreen>(new EquipmentPickerArgs(_vm.HeroId, index)));
                    if (slot.Item.HasValue)
                    {
                        var it = slot.Item.Value;
                        card.Set(slot.Slot, it.Rarity, ScreenKit.RarityColor(Ctx, it.Rarity), ScreenKit.ItemName(Ctx, it), "", slot.Badge, ScreenKit.ItemIcon(Ctx, it));
                    }
                    else
                    {
                        card.SetEmpty(slot.Slot, ScreenKit.SlotName(Ctx, slot.Slot));
                        card.Dot.Show(slot.Badge);
                    }
                    TutorialAnchor.Mark(card, "equip_slot" + index);
                }
                UIKit.Button(_content, "AutoEquip", Ctx.T("auto_equip"), () => _vm.AutoEquip(), UIKit.ButtonStyle.Ghost);
            }

            if (!string.IsNullOrEmpty(def.LoreKey))
            {
                Section("lore");
                UIKit.Text(_content, "Lore", Ctx.T(def.LoreKey), Th.fontBody, Th.textDim).Size(preferredHeight: 160);
            }
        }

        private void BuildProgression()
        {
            if (!_vm.Unlocked)
            {
                var u = _vm.UnlockInfo;
                ProgressBar.Create(_content, 56).Set(u.Have, u.Need, u.CanUnlock ? Th.positive : Th.secondary);
                var b = UIKit.Button(_content, "Unlock", Ctx.F("unlock_cost", u.Need), () =>
                {
                    if (ScreenKit.Report(Ctx, _vm.Unlock()))
                    {
                        Ctx.Haptics.Play(HapticKind.Success);
                        Ctx.PlaySfx("unlock");
                        HeroUnlockedPopup.Show(Ctx, _vm.HeroId);
                    }
                });
                b.interactable = u.CanUnlock;
                TutorialAnchor.Mark(b, "hero_unlock");
                if (!u.CanUnlock) UIKit.Text(_content, "Hint", Ctx.T("unlock_hint"), Th.fontSmall, Th.textDim, TextAlignmentOptions.Center).Size(preferredHeight: 50);
                return;
            }
            var s = _vm.StarUpInfo;
            if (s.IsMaxStar)
            {
                UIKit.Text(_content, "Max", Ctx.T("max_star"), Th.fontBody, Th.warning, TextAlignmentOptions.Center).Size(preferredHeight: 60);
                return;
            }
            ProgressBar.Create(_content, 56).Set(s.HaveShards, s.NeedShards, s.CanUpgrade ? Th.positive : Th.secondary);
            var star = UIKit.Button(_content, "StarUp", Ctx.F("starup_cost", s.NeedShards, s.CurrencyCost, (s.NextMultiplier / s.CurrentMultiplier - 1) * 100), () =>
            {
                if (ScreenKit.Report(Ctx, _vm.StarUp()))
                {
                    Ctx.Haptics.Play(HapticKind.Success);
                    Ctx.PlaySfx("starup");
                    Ctx.Manager.Toast(Ctx.F("star_reached", _vm.Star));
                    Say(_vm.HeroId, HeroLines.StarUp);
                }
            });
            star.interactable = s.CanUpgrade;
            TutorialAnchor.Mark(star, "hero_starup");
        }

        private SpeechBubble _bubble;

        private void Say(string heroId, string trigger)
        {
            var line = HeroLines.Pick(Ctx, heroId, trigger);
            if (line == null) return;
            if (_bubble == null) _bubble = SpeechBubble.Create(Root);
            // Next to the portrait, which sits at the left edge of the (tablet-centred) content column.
            var tip = new Vector2(-Root.rect.width * 0.5f + _body.offsetMin.x + 330, Root.rect.height * 0.5f - 300);
            _bubble.Show(line, tip, 2.8f, 34, 640);
        }

        private void Section(string key) => UIKit.Text(_content, "Section", Ctx.T(key), Th.fontLarge, Th.primary, TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 70);

        private void SkillRow(SkillDef skill, string kind)
        {
            if (skill == null) return;
            var panel = UIKit.Panel(_content, "Skill", Th.panel);
            UIKit.VStack(panel.gameObject, 6, 24);
            UIKit.Text(panel.transform, "Name", $"{Ctx.T(skill.NameKey)}  <size=70%><color={UIKit.Hex(Th.textDim)}>{kind}{(skill.Cooldown > 0 ? " · " + Ctx.F("cooldown_turns", skill.Cooldown) : "")}</color></size>", Th.fontBody, Th.text, TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 56);
            UIKit.Text(panel.transform, "Desc", Ctx.T(skill.DescriptionKey), Th.fontSmall, Th.textDim).Size(preferredHeight: 90);
        }

        public override void OnClosed() => _vm?.Dispose();
    }

    public sealed class EquipmentPickerArgs
    {
        public readonly string HeroId;
        public readonly int SlotIndex;
        public EquipmentPickerArgs(string heroId, int slotIndex) { HeroId = heroId; SlotIndex = slotIndex; }
    }

    /// <summary>Items for one slot with stat deltas vs the equipped one (green/red), "equipped by" badge, equip/unequip.</summary>
    public sealed class EquipmentPickerScreen : ScreenBase
    {
        private EquipmentPickerViewModel _vm;
        private RectTransform _content;

        public override string TabId => "heroes";

        protected override void Build()
        {
            var a = (EquipmentPickerArgs)Args;
            _vm = new EquipmentPickerViewModel(Ctx.Session, a.HeroId, a.SlotIndex);
            TipPopup.ShowOnce(Ctx, "tip_equipment");
            _vm.Changed += _ => Refresh();
            var body = ScreenKit.Header(Ctx, Root, ScreenKit.SlotName(Ctx, _vm.Slot) + " · " + ScreenKit.HeroName(Ctx, a.HeroId));
            var scroll = UIKit.ScrollView(body, "Scroll", out _content, Th.spacing, Th.padding);
            ((RectTransform)scroll.transform).Stretch();
            Refresh();
        }

        private void Refresh()
        {
            if (this == null) return;
            UIKit.Clear(_content);
            var current = _vm.Current;
            UIKit.Text(_content, "Current", Ctx.T("currently_equipped"), Th.fontLarge, Th.primary, TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 70);
            if (current.HasValue)
            {
                Row(current.Value, null, 0, null, isCurrent: true);
                UIKit.Button(_content, "Unequip", Ctx.T("unequip"), () => ScreenKit.Report(Ctx, _vm.Unequip()), UIKit.ButtonStyle.Ghost);
            }
            else UIKit.Text(_content, "Empty", Ctx.T("slot_empty"), Th.fontBody, Th.textDim).Size(preferredHeight: 60);

            UIKit.Text(_content, "Choose", Ctx.T("choose_item"), Th.fontLarge, Th.primary, TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 70);
            var list = _vm.Candidates;
            if (list.Count == 0) UIKit.Text(_content, "None", Ctx.T("no_items_for_slot"), Th.fontBody, Th.textDim).Size(preferredHeight: 100);
            var first = true;
            foreach (var e in list)
            {
                var b = Row(e.Item, e.Delta, e.PowerDelta, e.EquippedBy, isCurrent: false);
                if (first) TutorialAnchor.Mark(b, "picker_first");
                first = false;
            }
        }

        private Button Row(ItemInfo item, StatBlock delta, int powerDelta, string equippedBy, bool isCurrent)
        {
            var panel = UIKit.Panel(_content, "Row", Th.panel);
            UIKit.HStack(panel.gameObject, Th.spacing, 20, TextAnchor.MiddleLeft, expandWidth: false);
            panel.Size(preferredHeight: 240);
            var card = ItemCard.Create(panel.transform, 190);
            card.Set(item.Slot, item.Rarity, ScreenKit.RarityColor(Ctx, item.Rarity), ScreenKit.RarityName(Ctx, item.Rarity), "", false, ScreenKit.ItemIcon(Ctx, item));
            var texts = UIKit.Rect("Texts", panel.transform);
            UIKit.VStack(texts.gameObject, 4, 0, TextAnchor.MiddleLeft);
            // The text column gives way on narrow screens (tall phones are narrower than the 1080 reference) so the row fits.
            texts.Size(preferredWidth: isCurrent ? 740 : 500).minWidth = 200;
            UIKit.Text(texts, "Name", ScreenKit.ItemName(Ctx, item), Th.fontBody, ScreenKit.RarityColor(Ctx, item.Rarity), TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 52);
            var stats = string.Join("\n", _vm.ItemStats(item.InstanceId).Select(m => ScreenKit.FormatModifier(Ctx, m)));
            UIKit.Text(texts, "Stats", stats, Th.fontSmall, Th.text).Size(preferredHeight: 110);
            if (equippedBy != null) UIKit.Text(texts, "By", Ctx.F("equipped_by", ScreenKit.HeroName(Ctx, equippedBy)), Th.fontSmall, Th.warning).Size(preferredHeight: 44);
            if (isCurrent) return null;
            var right = UIKit.Rect("Right", panel.transform);
            UIKit.VStack(right.gameObject, 6, 0, TextAnchor.MiddleCenter);
            right.Size(preferredWidth: 220);
            var deltaText = delta == null ? "" : string.Join("\n", StatBlock.All.Where(s => Mathf.Abs(delta[s]) > 0.0001f)
                .Select(s => $"<color={UIKit.Hex(delta[s] > 0 ? Th.positive : Th.negative)}>{(delta[s] > 0 ? "+" : "")}{ScreenKit.FormatStat(s, delta[s])} {ScreenKit.StatName(Ctx, s)}</color>").Take(3));
            UIKit.Text(right, "Delta", deltaText, 26, Th.text, TextAlignmentOptions.Center).Size(preferredHeight: 100);
            var equip = UIKit.Button(right, "Equip", Ctx.T("equip"), () =>
            {
                if (equippedBy != null)
                    ConfirmPopup.Show(Ctx, Ctx.T("move_item_title"), Ctx.F("move_item_body", ScreenKit.HeroName(Ctx, equippedBy)), () => Equip(item.InstanceId));
                else Equip(item.InstanceId);
            }, powerDelta > 0 ? UIKit.ButtonStyle.Primary : UIKit.ButtonStyle.Secondary, Th.fontBody);
            equip.Size(preferredHeight: 110);
            return equip;
        }

        private void Equip(string id)
        {
            if (!ScreenKit.Report(Ctx, _vm.Equip(id))) return;
            Ctx.PlaySfx("equip");
            Close();
        }

        public override void OnClosed() => _vm?.Dispose();
    }

    /// <summary>Celebration when a hero is unlocked.</summary>
    public sealed class HeroUnlockedPopup : PopupBase
    {
        private string _heroId;

        public static void Show(UIContext ctx, string heroId) => ctx.Manager.ShowPopup<HeroUnlockedPopup>(p => p._heroId = heroId);

        protected override void Build()
        {
            var def = Ctx.Session.Db.GetHero(_heroId);
            UIKit.Text(Panel, "Title", Ctx.T("hero_unlocked"), Th.fontTitle, Th.primary, TextAlignmentOptions.Center, bold: true).Size(preferredHeight: 100);
            var holder = UIKit.Rect("Holder", Panel);
            holder.Size(preferredHeight: 320);
            var p = ScreenKit.Portrait(Ctx, holder, _heroId, 300);
            p.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(300, 300));
            Tween.Scale(p.transform, 0.3f, 1f, 0.45f, Ease.OutBack);
            UIKit.Text(Panel, "Name", Ctx.T(def.NameKey), Th.fontLarge, ScreenKit.RarityColor(Ctx, def.Rarity), TextAlignmentOptions.Center, bold: true).Size(preferredHeight: 80);
            UIKit.Text(Panel, "Rarity", ScreenKit.RarityName(Ctx, def.Rarity), Th.fontBody, Th.textDim, TextAlignmentOptions.Center).Size(preferredHeight: 60);
            var quote = HeroLines.Pick(Ctx, _heroId, HeroLines.Unlock);
            if (quote != null) UIKit.Text(Panel, "Quote", "\u201C" + quote + "\u201D", Th.fontBody, Th.text, TextAlignmentOptions.Center).Size(preferredHeight: 130);
            var ok = UIKit.Button(Panel, "Ok", Ctx.T("great"), Close);
            TutorialAnchor.Mark(ok, "unlock_ok");
        }
    }

    /// <summary>Five slots + owned heroes; tap a hero to add/remove, tap a slot to select it, auto-fill.</summary>
    public sealed class PartyScreen : ScreenBase
    {
        private PartyViewModel _vm;
        private RectTransform _slots, _pool;
        private TextMeshProUGUI _power;
        private int? _selectedSlot;

        public override string TabId => "home";

        protected override void Build()
        {
            _vm = new PartyViewModel(Ctx.Session);
            TipPopup.ShowOnce(Ctx, "tip_party_power");
            _vm.Changed += _ => Refresh();
            var body = ScreenKit.Header(Ctx, Root, Ctx.T("tab_party"));
            _slots = UIKit.Rect("Slots", body).TopBand(230, 10);
            UIKit.HStack(_slots.gameObject, 14, 0, TextAnchor.MiddleCenter, expandWidth: false);
            _power = UIKit.Text(body, "Power", "", Th.fontLarge, Th.text, TextAlignmentOptions.Center, bold: true);
            ((RectTransform)_power.transform).TopBand(80, 250);
            var auto = UIKit.Button(body, "Auto", Ctx.T("auto_fill"), () => _vm.AutoFill(), UIKit.ButtonStyle.Ghost, Th.fontBody);
            ((RectTransform)auto.transform).Anchor(new Vector2(1, 1), new Vector2(250, 100), new Vector2(-Th.padding, -330), new Vector2(1, 1));
            UIKit.Text(body, "Hint", Ctx.T("party_hint"), 30, Th.textDim).rectTransform.Anchor(new Vector2(0, 1), new Vector2(700, 110), new Vector2(Th.padding, -325), new Vector2(0, 1));
            var scroll = UIKit.ScrollView(body, "Pool", out _pool, Th.spacing, Th.padding);
            ((RectTransform)scroll.transform).Stretch(0, 440, 0, 0);
            DestroyImmediate(_pool.GetComponent<VerticalLayoutGroup>());
            var grid = _pool.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(HeroCard.Width, HeroCard.Height);
            grid.spacing = new Vector2(Th.spacing, Th.spacing);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.padding = new RectOffset((int)Th.padding, (int)Th.padding, (int)Th.padding, (int)Th.padding);
            Refresh();
        }

        public override void OnShow() => Refresh();

        private void Refresh()
        {
            if (this == null) return;
            UIKit.Clear(_slots);
            var slots = _vm.Slots;
            for (var i = 0; i < slots.Length; i++)
            {
                var index = i;
                var cell = UIKit.Image(_slots, "Slot" + i, _selectedSlot == i ? Th.secondary : Th.panel, Th.rounded, raycast: true);
                cell.Size(176, 220);
                var b = cell.gameObject.AddComponent<Button>();
                b.targetGraphic = cell;
                b.onClick.AddListener(() => OnSlot(index));
                var p = ScreenKit.Portrait(Ctx, cell.transform, slots[i], 140);
                p.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(140, 140), new Vector2(0, -14), new Vector2(0.5f, 1));
                var name = UIKit.Text(cell.transform, "Name", slots[i] != null ? ScreenKit.HeroName(Ctx, slots[i]) : Ctx.T("empty"), 24, Th.text, TextAlignmentOptions.Center);
                name.rectTransform.Stretch(4, 166, 4, 4);
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.enableAutoSizing = true; name.fontSizeMin = 14; name.fontSizeMax = 24;
                TutorialAnchor.Mark(b, "party_slot" + i);
            }
            _power.text = Ctx.F("power_value", _vm.Power);

            UIKit.Clear(_pool);
            foreach (var e in _vm.Available)
            {
                var card = HeroCard.Create(_pool);
                var entry = e;
                ScreenKit.SetHeroCard(Ctx, card, e);
                card.Shards.gameObject.SetActive(false);
                if (e.InParty) card.Background.color = Th.positive;
                card.Button.onClick.AddListener(() =>
                {
                    ScreenKit.Report(Ctx, _vm.Toggle(entry.Def.Id, _selectedSlot));
                    _selectedSlot = null;
                });
                TutorialAnchor.Mark(card, "party_pick_" + e.Def.Id);
            }
        }

        private void OnSlot(int index)
        {
            var slots = _vm.Slots;
            if (_selectedSlot.HasValue && _selectedSlot.Value != index)
            {
                ScreenKit.Report(Ctx, _vm.Swap(_selectedSlot.Value, index));
                _selectedSlot = null;
            }
            else if (slots[index] != null && _selectedSlot == index)
            {
                ScreenKit.Report(Ctx, _vm.Remove(index));
                _selectedSlot = null;
            }
            else _selectedSlot = _selectedSlot == index ? (int?)null : index;
            Refresh();
        }

        public override void OnClosed() => _vm?.Dispose();
    }
}
