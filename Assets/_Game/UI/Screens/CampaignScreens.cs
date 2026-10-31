using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Chapter selector + fights as nodes along a zig-zag path (locked / unlocked / cleared, stars, boss marker).</summary>
    public sealed class CampaignScreen : ScreenBase
    {
        private CampaignViewModel _vm;
        private TextMeshProUGUI _chapterTitle, _chapterInfo;
        private RectTransform _list;
        private Image _banner;
        private Button _prev, _next;

        public override string TabId => "campaign";

        protected override void Build()
        {
            _vm = new CampaignViewModel(Ctx.Session);
            _vm.Changed += _ => Refresh();
            var body = ScreenKit.Header(Ctx, Root, Ctx.T("tab_campaign"));
            var top = UIKit.Rect("ChapterBar", body).TopBand(170);
            top.gameObject.AddComponent<RectMask2D>();
            _banner = UIKit.Image(top, "Banner", Color.white);
            _banner.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(1080, 422));
            UIKit.Image(top, "Shade", new Color(0.04f, 0.03f, 0.08f, 0.72f)).rectTransform.Stretch();
            _prev = UIKit.Button(top, "Prev", "<", () => _vm.SelectChapter(_vm.SelectedChapter - 1), UIKit.ButtonStyle.Ghost, Th.fontTitle);
            ((RectTransform)_prev.transform).Anchor(new Vector2(0, 0.5f), new Vector2(130, 130), new Vector2(Th.padding, 0), new Vector2(0, 0.5f));
            _next = UIKit.Button(top, "Next", ">", () => _vm.SelectChapter(_vm.SelectedChapter + 1), UIKit.ButtonStyle.Ghost, Th.fontTitle);
            ((RectTransform)_next.transform).Anchor(new Vector2(1, 0.5f), new Vector2(130, 130), new Vector2(-Th.padding, 0), new Vector2(1, 0.5f));
            _chapterTitle = UIKit.Text(top, "Title", "", Th.fontLarge, Th.text, TextAlignmentOptions.Center, bold: true);
            _chapterTitle.rectTransform.Stretch(190, 10, 190, 80);
            _chapterTitle.textWrappingMode = TextWrappingModes.NoWrap;
            _chapterTitle.enableAutoSizing = true;
            _chapterTitle.fontSizeMin = 28;
            _chapterTitle.fontSizeMax = Th.fontLarge;
            _chapterInfo = UIKit.Text(top, "Info", "", Th.fontBody, Th.warning, TextAlignmentOptions.Center);
            _chapterInfo.rectTransform.Stretch(190, 95, 190, 5);
            var scroll = UIKit.ScrollView(body, "Fights", out _list, Th.spacing, Th.padding);
            ((RectTransform)scroll.transform).Stretch(0, 180, 0, 0);
            var story = UIKit.Button(body, "Story", Ctx.T("story_replay"), OpenJournal, UIKit.ButtonStyle.Ghost, Th.fontBody);
            ((RectTransform)story.transform).Anchor(new Vector2(1, 0), new Vector2(300, 100), new Vector2(-Th.padding, Th.padding), new Vector2(1, 0));
            Refresh();
        }

        public override void OnShow() => Refresh();

        private void Refresh()
        {
            if (this == null) return;
            var chapters = _vm.Chapters;
            var ch = chapters[_vm.SelectedChapter];
            _chapterTitle.text = Ctx.F("chapter_title", _vm.SelectedChapter + 1, Ctx.T(ch.Def.NameKey));
            var chAsset = Ctx.Config.GetChapterAsset(ch.Def.Id);
            _banner.sprite = chAsset != null ? chAsset.banner : null;
            _banner.enabled = _banner.sprite != null;
            _banner.color = ch.Unlocked ? Color.white : new Color(0.35f, 0.35f, 0.4f);
            _chapterInfo.text = ch.Unlocked ? Ctx.F("chapter_progress", ch.Cleared, ch.Def.FightIds.Count, ch.Stars, ch.MaxStars) : Ctx.T("chapter_locked");
            _prev.interactable = _vm.SelectedChapter > 0;
            _next.interactable = _vm.SelectedChapter < chapters.Count - 1;
            UIKit.Clear(_list);
            foreach (var node in _vm.Fights) BuildNode(node);
            var side = _vm.SideQuests;
            if (ch.Unlocked && side.Count > 0)
            {
                UIKit.Text(_list, "SideTitle", Ctx.T("side_quests"), Th.fontLarge, Th.primary, TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 80);
                UIKit.Text(_list, "SideHint", Ctx.T("side_quests_hint"), Th.fontSmall, Th.textDim, TextAlignmentOptions.Left).Size(preferredHeight: 60);
                foreach (var node in side) BuildNode(node);
            }
        }

        private void OpenJournal()
        {
            var beats = _vm.ChapterJournal;
            if (beats.Count == 0) Ctx.Manager.Toast(Ctx.T("journal_empty"));
            else StoryPopup.Show(Ctx, beats, null, replay: true);
        }

        private void BuildNode(FightNode n)
        {
            var row = UIKit.Rect("Node", _list);
            row.Size(preferredHeight: 190);
            var color = n.State == FightState.Locked ? Th.locked : n.IsNext ? Th.secondary : n.IsSideQuest ? Th.panel : Th.panelAlt;
            var card = UIKit.Image(row, "Card", color, Th.rounded, raycast: true);
            var left = n.Index % 2 == 0;
            // Side quests sit off the zig-zag main path, full width.
            card.rectTransform.anchorMin = new Vector2(n.IsSideQuest ? 0 : left ? 0 : 0.18f, 0);
            card.rectTransform.anchorMax = new Vector2(n.IsSideQuest ? 1 : left ? 0.82f : 1, 1);
            card.rectTransform.offsetMin = card.rectTransform.offsetMax = Vector2.zero;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            card.gameObject.AddComponent<ButtonFeedback>();
            var id = n.Def.Id;
            var lockedHint = n.IsSideQuest && n.UnlockedBy != null ? Ctx.F("side_quest_locked", Ctx.T(n.UnlockedBy.NameKey)) : Ctx.T(n.IsSideQuest ? "side_quest_locked_other" : "error_fightlocked");
            button.onClick.AddListener(() =>
            {
                if (n.State == FightState.Locked) Ctx.Manager.Toast(lockedHint);
                else Ctx.Manager.Push<FightPreviewScreen>(id);
            });
            if (n.IsNext) TutorialAnchor.Mark(button, "campaign_next");

            var num = UIKit.Image(card.transform, "Number", n.IsBoss ? Th.negative : n.IsSideQuest ? Th.primary : Th.background, n.IsBoss ? Th.star : Th.circle);
            num.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(130, 130), new Vector2(28, 0), new Vector2(0, 0.5f));
            var numText = UIKit.Text(num.transform, "N", n.IsSideQuest ? "?" : (n.Index + 1).ToString(), Th.fontLarge, n.IsSideQuest ? Th.background : Th.text, TextAlignmentOptions.Center, bold: true);
            numText.rectTransform.Stretch();
            var name = UIKit.Text(card.transform, "Name", Ctx.T(n.Def.NameKey) + (n.IsBoss ? "  · " + Ctx.T("boss") : ""), Th.fontLarge, Th.text, TextAlignmentOptions.Left, bold: true);
            name.rectTransform.Stretch(180, 20, 24, 90);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            var info = UIKit.Text(card.transform, "Info", n.State == FightState.Locked ? (n.IsSideQuest ? lockedHint : Ctx.T("locked")) : n.Def.RecommendedPower > 0 ? Ctx.F("recommended_power", n.Def.RecommendedPower) : "", Th.fontSmall, Th.textDim);
            info.rectTransform.Stretch(180, 100, 220, 20);
            if (n.State != FightState.Locked && _vm.StarsEnabled)
            {
                var stars = StarRow.Create(card.transform, 44, 3);
                ((RectTransform)stars.transform).Anchor(new Vector2(1, 0), new Vector2(170, 50), new Vector2(-24, 24), new Vector2(1, 0));
                stars.Set(n.Stars, 3);
            }
        }

        public override void OnClosed() => _vm?.Dispose();
    }

    /// <summary>Fight preview: enemies, possible rewards, party and power vs recommended, auto toggle, start.</summary>
    public sealed class FightPreviewScreen : ScreenBase
    {
        private FightPreviewViewModel _vm;
        private RectTransform _content;

        public override bool ShowTabBar => false;
        public override string TabId => "campaign";

        protected override void Build()
        {
            _vm = new FightPreviewViewModel(Ctx.Session, (string)Args);
            _vm.Changed += _ => Refresh();
            var body = ScreenKit.Header(Ctx, Root, Ctx.T(_vm.Fight.NameKey));
            var scroll = UIKit.ScrollView(body, "Scroll", out _content, Th.spacing, Th.padding);
            ((RectTransform)scroll.transform).Stretch(0, 0, 0, 220);
            var start = UIKit.Button(body, "Start", Ctx.T("start_fight"), OnStart, UIKit.ButtonStyle.Primary, Th.fontTitle);
            ((RectTransform)start.transform).Anchor(new Vector2(0.5f, 0), new Vector2(980, 170), new Vector2(0, 30), new Vector2(0.5f, 0));
            TutorialAnchor.Mark(start, "preview_start");
            Refresh();
        }

        public override void OnShow() => Refresh();

        private void Section(string key) => UIKit.Text(_content, "Section", Ctx.T(key), Th.fontLarge, Th.primary, TextAlignmentOptions.Left, bold: true).Size(preferredHeight: 70);

        private void Refresh()
        {
            if (this == null) return;
            UIKit.Clear(_content);
            if (_vm.Fight.IsSideQuest)
                UIKit.Text(_content, "SideQuest", Ctx.T("side_quest") + " · " + Ctx.T("side_quests_hint"), Th.fontSmall, Th.primary, TextAlignmentOptions.Left).Size(preferredHeight: 90);
            Section("enemies");
            foreach (var wave in _vm.Enemies.GroupBy(e => e.Wave))
            {
                if (_vm.Fight.Waves.Count > 1) UIKit.Text(_content, "Wave", Ctx.F("wave_n", wave.Key + 1), Th.fontBody, Th.textDim).Size(preferredHeight: 56);
                var row = UIKit.Rect("WaveRow", _content);
                UIKit.HStack(row.gameObject, 16, 0, TextAnchor.MiddleLeft, expandWidth: false);
                row.Size(preferredHeight: 190);
                foreach (var e in wave)
                {
                    var asset = Ctx.Config.GetEnemyAsset(e.Def.Id);
                    var cell = UIKit.Panel(row, "Enemy", Th.panel);
                    cell.Size(180, 190);
                    var art = ScreenKit.EnemyIcon(asset);
                    var icon = UIKit.Image(cell.transform, "Icon", art != null ? Color.white : asset != null ? asset.tint : Color.gray, art != null ? art : asset != null && asset.sprite != null ? asset.sprite : Th.diamond);
                    icon.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(e.Def.IsBoss ? 120 : 100, e.Def.IsBoss ? 120 : 100), new Vector2(0, -12), new Vector2(0.5f, 1));
                    var n = UIKit.Text(cell.transform, "Name", ScreenKit.EnemyName(Ctx, e.Def.Id), 26, e.Def.IsBoss ? Th.negative : Th.text, TextAlignmentOptions.Center, bold: e.Def.IsBoss);
                    n.rectTransform.Stretch(4, 128, 4, 30);
                    n.textWrappingMode = TextWrappingModes.NoWrap;
                    n.enableAutoSizing = true; n.fontSizeMin = 16; n.fontSizeMax = 26;
                    var p = UIKit.Text(cell.transform, "Power", e.Power.ToString(), 24, Th.textDim, TextAlignmentOptions.Center);
                    p.rectTransform.Stretch(4, 160, 4, 0);
                }
            }

            Section("possible_rewards");
            var d = _vm.Drops;
            var lines = new List<string>();
            if (d.Gold) lines.Add(Ctx.T("gold"));
            if (d.HeroShards.Count > 0) lines.Add(Ctx.F("shards_of", string.Join(", ", d.HeroShards.Select(h => ScreenKit.HeroName(Ctx, h)))));
            if (d.ShardRarities.Count > 0) lines.Add(Ctx.F("random_shards", string.Join(", ", d.ShardRarities.Select(r => ScreenKit.RarityName(Ctx, r)))));
            if (d.ItemRarities.Count > 0) lines.Add(Ctx.F("equipment_rarities", string.Join(", ", d.ItemRarities.Select(r => ScreenKit.RarityName(Ctx, r)))));
            foreach (var l in lines)
            {
                var t = UIKit.Text(_content, "Drop", "• " + l, Th.fontBody, Th.text);
                t.Size(preferredHeight: 60);
                t.enableAutoSizing = true;
                t.fontSizeMin = 26;
                t.fontSizeMax = Th.fontBody;
            }
            if (_vm.FirstClearAvailable) UIKit.Text(_content, "First", Ctx.T("first_clear_bonus"), Th.fontBody, Th.positive).Size(preferredHeight: 60);
            foreach (var (label, chance) in d.Odds) UIKit.Text(_content, "Odds", $"{label}: {chance * 100:0.#}%", Th.fontSmall, Th.textDim).Size(preferredHeight: 48);

            Section("tab_party");
            var partyRow = UIKit.Rect("Party", _content);
            UIKit.HStack(partyRow.gameObject, 16, 0, TextAnchor.MiddleCenter, expandWidth: false);
            partyRow.Size(preferredHeight: 150);
            foreach (var id in _vm.PartySlots) ScreenKit.Portrait(Ctx, partyRow, id, 140);
            var power = UIKit.Text(_content, "Power", _vm.RecommendedPower > 0 ? Ctx.F("power_vs_recommended", _vm.PartyPower, _vm.RecommendedPower) : Ctx.F("power_value", _vm.PartyPower),
                Th.fontLarge, _vm.IsUnderpowered ? Th.negative : _vm.IsClose ? Th.warning : Th.positive, TextAlignmentOptions.Center, bold: true);
            power.Size(preferredHeight: 70);
            if (_vm.IsUnderpowered) UIKit.Text(_content, "Warn", Ctx.T("underpowered_hint"), Th.fontSmall, Th.textDim, TextAlignmentOptions.Center).Size(preferredHeight: 110);
            var edit = UIKit.Button(_content, "EditParty", Ctx.T("edit_party"), () => Ctx.Manager.Push<PartyScreen>(), UIKit.ButtonStyle.Secondary);
            TutorialAnchor.Mark(edit, "preview_party");
            ScreenKit.Toggle(Ctx, _content, Ctx.T("auto_skills"), () => _vm.AutoSkill, v => _vm.SetAutoSkill(v));
            if (Ctx.Session.Db.Rules.StarsEnabled)
                UIKit.Text(_content, "StarRules", Ctx.F("star_rules", _vm.StarTurnLimit), Th.fontSmall, Th.textDim, TextAlignmentOptions.Center).Size(preferredHeight: 100);
        }

        private void OnStart()
        {
            if (!ScreenKit.Report(Ctx, _vm.CanStart())) return;
            var beats = Ctx.Session.Progress.GetBeats(_vm.Fight.Id, post: false);
            if (beats.Count > 0) StoryPopup.Show(Ctx, beats, () => Ctx.StartBattle(_vm.Fight.Id));
            else Ctx.StartBattle(_vm.Fight.Id);
        }

        public override void OnClosed() => _vm?.Dispose();
    }

    /// <summary>Dialogue: one beat per tap (speaker portrait, name, text), skippable. Marks beats as seen.</summary>
    public sealed class StoryPopup : PopupBase
    {
        private IReadOnlyList<StoryBeatDef> _beats;
        private Action _done;
        private bool _replay;
        private int _index;
        private TextMeshProUGUI _speaker, _text;
        private Image _portrait;

        public override bool CloseOnBackdrop => false;

        public static void Show(UIContext ctx, IReadOnlyList<StoryBeatDef> beats, Action done, bool replay = false)
        {
            if (beats == null || beats.Count == 0) { done?.Invoke(); return; }
            ctx.Manager.ShowPopup<StoryPopup>(p => { p._beats = beats; p._done = done; p._replay = replay; });
        }

        protected override void Build()
        {
            var head = UIKit.Rect("Head", Panel);
            UIKit.HStack(head.gameObject, Th.spacing, 0, TextAnchor.MiddleLeft, expandWidth: false);
            head.Size(preferredHeight: 140);
            _portrait = UIKit.Image(head, "Portrait", Color.white, Th.circle);
            _portrait.Size(130, 130);
            _speaker = UIKit.Text(head, "Speaker", "", Th.fontLarge, Th.primary, TextAlignmentOptions.Left, bold: true);
            _speaker.Size(preferredWidth: 600);
            _text = UIKit.Text(Panel, "Text", "", Th.fontBody, Th.text);
            _text.Size(preferredHeight: 300);
            var row = UIKit.Rect("Buttons", Panel);
            UIKit.HStack(row.gameObject, Th.spacing);
            row.Size(preferredHeight: Th.buttonHeight);
            UIKit.Button(row, "Skip", Ctx.T("skip"), Finish, UIKit.ButtonStyle.Ghost);
            var next = UIKit.Button(row, "Next", Ctx.T("next"), Advance);
            TutorialAnchor.Mark(next, "story_next");
            Show();
        }

        private void Show()
        {
            var b = _beats[_index];
            _speaker.text = Ctx.T(b.SpeakerKey);
            _text.text = Ctx.T(b.TextKey);
            var asset = Ctx.Config.GetStoryBeatAsset(b.Id);
            var hero = Ctx.Config.HeroAssets.FirstOrDefault(h => h.nameKey == b.SpeakerKey);
            var enemy = Ctx.Config.EnemyAssets.FirstOrDefault(e => e.nameKey == b.SpeakerKey);
            // Beat-specific portrait first, then the speaker's own icon art, then the tinted placeholder.
            var art = asset != null && asset.portrait != null ? asset.portrait : hero != null ? ScreenKit.HeroIcon(hero) : ScreenKit.EnemyIcon(enemy);
            _portrait.sprite = art != null ? art : Th.circle;
            _portrait.color = art != null ? Color.white : hero != null ? hero.tint : enemy != null ? enemy.tint : Th.textDim;
        }

        private void Advance()
        {
            _index++;
            if (_index >= _beats.Count) Finish();
            else Show();
        }

        private void Finish()
        {
            if (!_replay) Ctx.Session.Progress.MarkBeatsSeen(_beats.Select(b => b.Id));
            Close();
            _done?.Invoke();
        }

        public override bool HandleBack()
        {
            Finish();
            return true;
        }
    }
}
