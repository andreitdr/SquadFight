using TMPro;

namespace Game.UI
{
    /// <summary>Home (step 12 replaces the placeholder body).</summary>
    public sealed class HomeScreen : ScreenBase
    {
        public override string TabId => "home";

        protected override void Build()
        {
            var col = UIKit.Rect("Column", Root).Stretch(Th.padding, Th.padding, Th.padding, Th.padding);
            UIKit.VStack(col.gameObject, Th.spacing, 0, UnityEngine.TextAnchor.MiddleCenter);
            UIKit.Text(col, "Title", "SquadFight", Th.fontHuge, Th.primary, TextAlignmentOptions.Center, bold: true).Size(preferredHeight: 160);
            UIKit.Text(col, "Sub", Ctx.T("loading"), Th.fontBody, Th.textDim, TextAlignmentOptions.Center).Size(preferredHeight: 80);
        }
    }
}
