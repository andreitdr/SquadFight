using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.ViewModels;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Gear shop: rarity tabs, current-tier weapons and armour with prices (red dot = upgrade for the party), buy popup.</summary>
    public sealed class ShopScreen : ScreenBase
    {
        private ShopViewModel _vm;
        private RecycleList _list;
        private TextMeshProUGUI _tier;
        private IReadOnlyList<ShopEntry> _entries = new List<ShopEntry>();

        public override string TabId => "home";

        protected override void Build()
        {
            _vm = new ShopViewModel(Ctx.Session);
            _vm.Changed += _ => Refresh();
            var body = ScreenKit.Header(Ctx, Root, Ctx.T("shop"), maxWidth: ScreenKit.WideWidth);
            var rarities = _vm.Rarities;
            var tabs = Tabs.Create(body, rarities.Select(r => ScreenKit.RarityName(Ctx, r)).ToArray(), 0, 100);
            var trt = (RectTransform)tabs.transform;
            trt.TopBand(100, 0);
            trt.offsetMin = new Vector2(16, trt.offsetMin.y);
            trt.offsetMax = new Vector2(-16, trt.offsetMax.y);
            foreach (var t in tabs.GetComponentsInChildren<TextMeshProUGUI>()) { t.fontSizeMax = 28; t.fontSize = 28; t.fontSizeMin = 14; }
            tabs.Changed += i => _vm.SelectRarity(rarities[i]);
            _tier = UIKit.Text(body, "Tier", "", Th.fontSmall, Th.textDim);
            ((RectTransform)_tier.transform).Anchor(new Vector2(0, 1), new Vector2(900, 70), new Vector2(Th.padding, -110), new Vector2(0, 1));
            _list = RecycleList.Create(body, ShopCell.Height, ShopCell.Create, Bind, columns: 4, spacing: 18, padding: Th.padding);
            ((RectTransform)_list.transform).Stretch(0, 190, 0, 0);
            Refresh();
        }

        private void Bind(int index, RectTransform rt)
        {
            var e = _entries[index];
            var o = e.Offer;
            var cell = rt.GetComponent<ShopCell>();
            var card = cell.Card;
            card.Set(o.Slot, o.Rarity, ScreenKit.RarityColor(Ctx, o.Rarity), Ctx.T(o.Template.NameKey), "", e.UpgradeFor != null,
                Ctx.Config.GetEquipmentAsset(o.Template.Id)?.icon);
            cell.Price.text = o.Price.ToString("N0");
            cell.Price.color = o.CanAfford ? Th.warning : Th.negative;
            card.Button.onClick.RemoveAllListeners();
            card.Button.onClick.AddListener(() => ShopItemPopup.Show(Ctx, _vm, e));
        }

        public override void OnShow() => Refresh();

        private void Refresh()
        {
            if (this == null) return;
            _entries = _vm.Entries;
            _tier.text = Ctx.F("shop_tier", _vm.Tier);
            _list.SetCount(_entries.Count);
        }

        public override void OnClosed() => _vm?.Dispose();
    }

    /// <summary>Shop grid cell: item card with the price underneath (gold when affordable, red when not).</summary>
    public sealed class ShopCell : MonoBehaviour
    {
        public const float Height = 290;
        private const float CardSize = 230;
        public ItemCard Card;
        public TextMeshProUGUI Price;

        public static RectTransform Create(Transform parent)
        {
            var rt = UIKit.Rect("ShopCell", parent);
            var cell = rt.gameObject.AddComponent<ShopCell>();
            cell.Card = ItemCard.Create(rt, CardSize);
            ((RectTransform)cell.Card.transform).TopBand(CardSize);
            cell.Price = UIKit.Text(rt, "Price", "", 40, UIKit.Theme.warning, TextAlignmentOptions.Center, bold: true);
            cell.Price.rectTransform.BottomBand(Height - CardSize - 4);
            return rt;
        }
    }

    /// <summary>One shop item: stats at the chosen rarity, best party upgrade, buy.</summary>
    public sealed class ShopItemPopup : PopupBase
    {
        private ShopViewModel _vm;
        private ShopEntry _entry;

        public static void Show(UIContext ctx, ShopViewModel vm, ShopEntry entry) => ctx.Manager.ShowPopup<ShopItemPopup>(p => { p._vm = vm; p._entry = entry; });

        protected override void Build()
        {
            var o = _entry.Offer;
            var color = ScreenKit.RarityColor(Ctx, o.Rarity);
            var head = UIKit.Rect("Head", Panel);
            UIKit.HStack(head.gameObject, Th.spacing, 0, TextAnchor.MiddleLeft, expandWidth: false);
            head.Size(preferredHeight: 220);
            ItemCard.Create(head, 200).Set(o.Slot, o.Rarity, color, "", "", false, Ctx.Config.GetEquipmentAsset(o.Template.Id)?.icon);
            var col = UIKit.Rect("Col", head);
            UIKit.VStack(col.gameObject, 6, 0, TextAnchor.MiddleLeft);
            col.Size(preferredWidth: 620);
            UIKit.Text(col, "Name", Ctx.T(o.Template.NameKey), Th.fontLarge, color, TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 70);
            UIKit.Text(col, "Kind", $"{ScreenKit.RarityName(Ctx, o.Rarity)} · {ScreenKit.SlotName(Ctx, o.Slot)} · {Ctx.F("shop_tier", o.Template.Tier)}", Th.fontBody, Th.textDim).Size(preferredHeight: 56);
            foreach (var m in _vm.Stats(o)) UIKit.Text(Panel, "Stat", ScreenKit.FormatModifier(Ctx, m), Th.fontBody, Th.text).Size(preferredHeight: 56);
            if (_entry.UpgradeFor != null)
                UIKit.Text(Panel, "Upgrade", Ctx.F("shop_upgrade_for", ScreenKit.HeroName(Ctx, _entry.UpgradeFor), _entry.PowerGain), Th.fontBody, Th.positive).Size(preferredHeight: 56);
            var row = UIKit.Rect("Buttons", Panel);
            UIKit.HStack(row.gameObject, Th.spacing);
            row.Size(preferredHeight: Th.buttonHeight);
            UIKit.Button(row, "Close", Ctx.T("close"), Close, UIKit.ButtonStyle.Ghost);
            var buy = UIKit.Button(row, "Buy", Ctx.F("buy_for", o.Price.ToString("N0")), () =>
            {
                if (!ScreenKit.Report(Ctx, _vm.Buy(o))) return;
                Ctx.Haptics.Play(HapticKind.Success);
                Ctx.PlaySfx("reward");
                Ctx.Manager.Toast(Ctx.F("bought_item", Ctx.T(o.Template.NameKey)));
                Close();
            });
            buy.interactable = _vm.Gold >= o.Price;
        }
    }
}
