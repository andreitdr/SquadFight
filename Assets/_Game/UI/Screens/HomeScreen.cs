using Game.ViewModels;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public sealed class HomeScreen : ScreenBase
    {
        private HomeViewModel _vm;
        private TextMeshProUGUI _chapter, _next, _stars;
        private ProgressBar _progress;
        private UnityEngine.UI.Button _continue;
        private RedDot _heroesDot;

        public override string TabId => "home";

        protected override void Build()
        {
            _vm = new HomeViewModel(Ctx.Session, UIScreensSetup.Dots ?? new NotificationsModel(Ctx.Session));
            _vm.Changed += _ => Refresh();
            var col = UIKit.Rect("Column", Root).Stretch(Th.padding, Th.padding, Th.padding, Th.padding);
            UIKit.VStack(col.gameObject, Th.spacing, 0, TextAnchor.UpperCenter);

            if (Th.logo != null)
            {
                var logo = UIKit.Image(col, "Logo", Color.white, Th.logo);
                logo.Size(preferredHeight: 380);
                Tween.Scale(logo.transform, 0.85f, 1f, 0.5f, Ease.OutBack);
            }
            else UIKit.Text(col, "Title", "SquadFight", Th.fontHuge, Th.primary, TextAlignmentOptions.Center, bold: true).Size(preferredHeight: 170);

            var card = UIKit.Panel(col, "ChapterCard", Th.panel);
            UIKit.VStack(card.gameObject, 16, Th.padding);
            _chapter = UIKit.Text(card.transform, "Chapter", "", Th.fontLarge, Th.text, TextAlignmentOptions.Left, bold: true);
            _chapter.Size(preferredHeight: 70);
            _chapter.textWrappingMode = TextWrappingModes.NoWrap;
            _chapter.enableAutoSizing = true;
            _chapter.fontSizeMin = 30;
            _chapter.fontSizeMax = Th.fontLarge;
            _progress = ProgressBar.Create(card.transform, 48);
            _next = UIKit.Text(card.transform, "Next", "", Th.fontBody, Th.textDim);
            _next.Size(preferredHeight: 60);
            _stars = UIKit.Text(card.transform, "Stars", "", Th.fontBody, Th.warning);
            _stars.Size(preferredHeight: 60);

            _continue = UIKit.Button(col, "Continue", Ctx.T("continue"), OnContinue, UIKit.ButtonStyle.Primary, Th.fontTitle);
            _continue.Size(preferredHeight: 180);
            TutorialAnchor.Mark(_continue, "home_continue");

            var row = UIKit.Rect("Row", col);
            UIKit.HStack(row.gameObject, Th.spacing);
            row.Size(preferredHeight: Th.buttonHeight);
            var party = UIKit.Button(row, "Party", Ctx.T("tab_party"), () => Ctx.Manager.Push<PartyScreen>(), UIKit.ButtonStyle.Secondary);
            TutorialAnchor.Mark(party, "home_party");
            var heroes = UIKit.Button(row, "Heroes", Ctx.T("tab_heroes"), () => Navigation.GoTab(Ctx, "heroes"), UIKit.ButtonStyle.Secondary);
            _heroesDot = RedDot.Attach(heroes.transform);
            Refresh();
        }

        private void OnContinue()
        {
            var id = _vm.NextFightId;
            if (id != null) Ctx.Manager.Push<FightPreviewScreen>(id);
        }

        public override void OnShow() => Refresh();

        private void Refresh()
        {
            if (this == null) return;
            var ch = _vm.CurrentChapter;
            _chapter.text = ch != null ? Ctx.F("chapter_title", _vm.ChapterIndex + 1, Ctx.T(ch.NameKey)) : "";
            _progress.Set(_vm.ChapterCleared, _vm.ChapterTotal);
            _next.text = _vm.CampaignComplete ? Ctx.T("campaign_complete") : _vm.NextFight != null ? Ctx.F("next_fight", Ctx.T(_vm.NextFight.NameKey)) : "";
            _stars.text = Ctx.F("stars_total", _vm.TotalStars, _vm.MaxStars);
            _continue.SetLabel(_vm.CampaignComplete ? Ctx.T("replay") : Ctx.T("continue"));
            _heroesDot.Show(_vm.Dots.AnyHeroBadge);
        }

        public override void OnClosed() => _vm?.Dispose();
    }
}
