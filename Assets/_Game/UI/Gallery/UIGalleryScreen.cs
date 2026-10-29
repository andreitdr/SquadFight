using System.Linq;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Every widget on one scrollable page for visual QA (16:9, 19.5:9, 4:3) and a 1,000-item pooled list.</summary>
    public sealed class UIGalleryScreen : ScreenBase
    {
        public override bool ShowTopBar => false;
        public override bool ShowTabBar => false;

        protected override void Build()
        {
            var scroll = UIKit.ScrollView(Root, "Scroll", out var content, Th.spacing, Th.padding);
            ((RectTransform)scroll.transform).Stretch();
            UIKit.Text(content, "Title", Ctx.T("gallery_title"), Th.fontTitle, Th.text, TextAlignmentOptions.Center, bold: true).Size(preferredHeight: 100);

            Section(content, "Buttons");
            foreach (UIKit.ButtonStyle style in System.Enum.GetValues(typeof(UIKit.ButtonStyle)))
                UIKit.Button(content, style.ToString(), style.ToString(), () => Ctx.Manager.Toast(style + " pressed"), style);

            Section(content, "Tabs");
            var tabs = Tabs.Create(content, new[] { Ctx.T("tab_heroes"), Ctx.T("tab_inventory"), Ctx.T("tab_settings") });
            tabs.Changed += i => Ctx.Manager.Toast("Tab " + i);

            Section(content, "Progress & stats");
            ProgressBar.Create(content, 44).Set(7, 20);
            ProgressBar.Create(content, 44).Set(20, 20, Th.positive);
            StatRow.Create(content, Ctx.T("stat_attack")).Set("120", +15);
            StatRow.Create(content, Ctx.T("stat_defense")).Set("40", -6);
            StatRow.Create(content, Ctx.T("stat_speed")).Set("105");

            Section(content, "Currency");
            var chipRow = UIKit.Rect("Chips", content);
            UIKit.HStack(chipRow.gameObject, Th.spacing, 0, TextAnchor.MiddleCenter, expandWidth: false);
            chipRow.Size(preferredHeight: 100);
            CurrencyChip.Create(chipRow, Th.warning).Set(1234);
            CurrencyChip.Create(chipRow, Th.secondary).Set(5_600_000);

            Section(content, "Hero cards (all rarities)");
            var heroGrid = Grid(content, HeroCard.Height);
            var db = Ctx.Session.Db;
            foreach (var r in System.Enum.GetValues(typeof(Rarity)).Cast<Rarity>())
            {
                var hero = db.Heroes.FirstOrDefault(h => h.Rarity == r);
                if (hero == null) continue;
                var asset = Ctx.Config.GetHeroAsset(hero.Id);
                var card = HeroCard.Create(heroGrid);
                var unlocked = r <= Rarity.Uncommon;
                card.Set(Ctx.T(hero.NameKey), r, Ctx.Config.GetRarityColor(r), asset.tint, asset.portrait, unlocked, unlocked ? 2 : 0, db.MaxStar, 7, 20, Ctx.F("power_value", 812), r == Rarity.Rare);
            }

            Section(content, "Item cards (all slots)");
            var itemGrid = Grid(content, 200);
            var i2 = 0;
            foreach (var slot in System.Enum.GetValues(typeof(EquipmentSlot)).Cast<EquipmentSlot>())
            {
                var rarity = (Rarity)(i2++ % 5);
                ItemCard.Create(itemGrid).Set(slot, rarity, Ctx.Config.GetRarityColor(rarity), Ctx.T("slot_" + slot.ToString().ToLowerInvariant()), i2 % 2 == 0 ? "E" : "", i2 == 3);
            }
            ItemCard.Create(itemGrid).SetEmpty(EquipmentSlot.Ring, Ctx.T("slot_ring"));

            Section(content, "Popups & overlays");
            UIKit.Button(content, "Confirm", "ConfirmPopup", () => ConfirmPopup.Show(Ctx, Ctx.T("quit_title"), Ctx.T("quit_body"), () => Ctx.Manager.Toast("confirmed")), UIKit.ButtonStyle.Secondary);
            UIKit.Button(content, "Message", "MessagePopup", () => MessagePopup.Show(Ctx, "Info", Ctx.T("error_insufficientfunds")), UIKit.ButtonStyle.Secondary);
            UIKit.Button(content, "Toast", "Toast", () => Ctx.Manager.Toast("Hello from a toast"), UIKit.ButtonStyle.Secondary);
            UIKit.Button(content, "Loading", "Loading (1 s)", () => { Ctx.Manager.ShowLoading(true); Invoke(nameof(HideLoading), 1f); }, UIKit.ButtonStyle.Secondary);
            UIKit.Button(content, "List", "Pooled list ×1000", () => Ctx.Manager.Push<GalleryListScreen>(), UIKit.ButtonStyle.Secondary);

            Section(content, "Safe area simulation");
            UIKit.Button(content, "Notch", "Notch (top 120 px)", () => SafeAreaFitter.SimulatedSafeArea = new Rect(0, 60, Screen.width, Screen.height - 180), UIKit.ButtonStyle.Ghost);
            UIKit.Button(content, "NoSim", "Real safe area", () => SafeAreaFitter.SimulatedSafeArea = null, UIKit.ButtonStyle.Ghost);
        }

        private void HideLoading() => Ctx.Manager.ShowLoading(false);

        private void Section(Transform parent, string title) =>
            UIKit.Text(parent, "Section", title, Th.fontLarge, Th.primary, TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 80);

        private RectTransform Grid(Transform parent, float cellHeight)
        {
            var grid = UIKit.Rect("Grid", parent);
            var g = grid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            g.cellSize = new Vector2(cellHeight > 300 ? HeroCard.Width : 200, cellHeight);
            g.spacing = new Vector2(Th.spacing, Th.spacing);
            g.childAlignment = TextAnchor.UpperCenter;
            var fitter = grid.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            return grid;
        }
    }

    /// <summary>1,000 rows in a pooled list (performance check).</summary>
    public sealed class GalleryListScreen : ScreenBase
    {
        public override bool ShowTopBar => false;
        public override bool ShowTabBar => false;
        public RecycleList List { get; private set; }

        protected override void Build()
        {
            UIKit.Button(Root, "Back", Ctx.T("back"), Close, UIKit.ButtonStyle.Ghost).GetComponent<RectTransform>().TopBand(Th.buttonHeight);
            List = RecycleList.Create(Root, 140, parent =>
            {
                var row = UIKit.Panel(parent, "Row", Th.panel);
                var label = UIKit.Text(row.transform, "Label", "", Th.fontBody, Th.text);
                label.rectTransform.Stretch(24, 0, 24, 0);
                return row.rectTransform;
            }, (i, rt) => rt.GetComponentInChildren<TextMeshProUGUI>().text = "Row #" + (i + 1));
            ((RectTransform)List.transform).Stretch(0, Th.buttonHeight + Th.spacing, 0, 0);
            List.SetCount(1000);
        }
    }
}
