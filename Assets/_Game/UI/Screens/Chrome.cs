using Game.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Persistent top bar: currencies + party power.</summary>
    public sealed class TopBar : MonoBehaviour
    {
        private UIContext _ctx;
        private CurrencyBarViewModel _vm;
        private CurrencyChip _gold;
        private TextMeshProUGUI _power;

        public static TopBar Create(UIContext ctx)
        {
            var host = ctx.Manager.TopBarHost;
            var bg = UIKit.Image(host, "TopBarBg", ctx.Theme.panel.WithAlpha(0.95f));
            bg.rectTransform.Stretch();
            UIKit.HStack(bg.gameObject, ctx.Theme.spacing, 24, TextAnchor.MiddleLeft, expandWidth: false);
            var bar = bg.gameObject.AddComponent<TopBar>();
            bar._ctx = ctx;
            bar._vm = new CurrencyBarViewModel(ctx.Session);
            bar._gold = CurrencyChip.Create(bg.transform, ctx.Theme.warning);
            bar._gold.Size(preferredWidth: 340, preferredHeight: 100);
            var spacer = UIKit.Rect("Spacer", bg.transform);
            spacer.Size(flexibleWidth: 1);
            bar._power = UIKit.Text(bg.transform, "Power", "", ctx.Theme.fontBody, ctx.Theme.textDim, TextAlignmentOptions.Right, bold: true);
            bar._power.Size(preferredWidth: 420);
            bar._vm.Changed += _ => bar.Refresh();
            ctx.SceneDisposables.Add(bar._vm);
            ctx.SceneDisposables.Add(ctx.Session.Bus.Subscribe<Game.Services.PartyChanged>(_ => { if (bar != null) bar.Refresh(); }));
            ctx.SceneDisposables.Add(ctx.Session.Bus.Subscribe<Game.Services.HeroStarUpgraded>(_ => { if (bar != null) bar.Refresh(); }));
            ctx.SceneDisposables.Add(ctx.Session.Bus.Subscribe<Game.Services.ItemEquipped>(_ => { if (bar != null) bar.Refresh(); }));
            ctx.SceneDisposables.Add(ctx.Session.Bus.Subscribe<Game.Services.ItemUnequipped>(_ => { if (bar != null) bar.Refresh(); }));
            bar.Refresh();
            return bar;
        }

        public void Refresh()
        {
            if (this == null) return;
            _gold.Set(_vm.Amount(_ctx.Session.Db.Rules.PrimaryCurrencyId));
            _power.text = _ctx.F("power_value", _ctx.Session.PartyPower);
        }

    }

    /// <summary>Bottom tab bar: Home, Campaign, Heroes, Bag, Settings (red dots on Heroes and Bag).</summary>
    public sealed class TabBar : MonoBehaviour
    {
        private UIContext _ctx;
        private NotificationsModel _dots;
        private RedDot _heroesDot, _bagDot;
        private Button[] _buttons;
        private static readonly string[] Ids = { "home", "campaign", "heroes", "inventory", "settings" };

        public static TabBar Create(UIContext ctx, NotificationsModel dots)
        {
            var host = ctx.Manager.TabBarHost;
            var bg = UIKit.Image(host, "TabBarBg", ctx.Theme.panel);
            bg.rectTransform.Stretch();
            UIKit.HStack(bg.gameObject, 8, 14);
            var bar = bg.gameObject.AddComponent<TabBar>();
            bar._ctx = ctx;
            bar._dots = dots;
            var labels = new[] { "tab_home", "tab_campaign", "tab_heroes", "tab_inventory", "tab_settings" };
            bar._buttons = new Button[Ids.Length];
            for (var i = 0; i < Ids.Length; i++)
            {
                var id = Ids[i];
                var b = UIKit.Button(bg.transform, "Tab_" + id, ctx.T(labels[i]), () => Navigation.GoTab(ctx, id), UIKit.ButtonStyle.Ghost, 34);
                bar._buttons[i] = b;
                TutorialAnchor.Mark(b, "tab_" + id);
                if (id == "heroes") bar._heroesDot = RedDot.Attach(b.transform);
                if (id == "inventory") bar._bagDot = RedDot.Attach(b.transform);
            }
            dots.Changed += _ => bar.RefreshDots();
            ctx.Manager.TopChanged += _ => bar.Highlight();
            bar.RefreshDots();
            return bar;
        }

        public void RefreshDots()
        {
            if (this == null) return;
            _heroesDot.Show(_dots.AnyHeroBadge);
            _bagDot.Show(_dots.AnyItemBadge);
        }

        public void Highlight()
        {
            if (this == null) return;
            var top = _ctx.Manager.Top;
            var stackTab = top == null ? null : top.TabId;
            for (var i = 0; i < Ids.Length; i++)
                ((Image)_buttons[i].targetGraphic).color = Ids[i] == stackTab ? _ctx.Theme.secondary : _ctx.Theme.panelAlt;
        }
    }

    /// <summary>Tab navigation: Home is the root; other tabs sit directly on top of it.</summary>
    public static class Navigation
    {
        public static void GoTab(UIContext ctx, string id)
        {
            var m = ctx.Manager;
            if (m.Top != null && m.Top.TabId == id && m.Screens.Count <= 2) return;
            m.PopToRoot();
            switch (id)
            {
                case "campaign": m.Push<CampaignScreen>(); break;
                case "heroes": m.Push<HeroesScreen>(); break;
                case "inventory": m.Push<InventoryScreen>(); break;
                case "settings": m.Push<SettingsScreen>(); break;
            }
        }
    }
}
