using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.Services;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.BattleView
{
    public sealed class BattleScreenArgs
    {
        public ActiveBattle Battle;
        /// <summary>Called once when the player leaves: false = finished/skip, true = retreat.</summary>
        public Action<bool> Leave;
    }

    /// <summary>
    /// Plays the simulator turn by turn as animations. Presentation never changes outcomes: skip just runs the
    /// remaining turns instantly. In dev builds every turn checks that bars match the simulator (desync assert).
    /// </summary>
    public sealed class BattleScreen : ScreenBase
    {
        /// <summary>Tests force a high speed to play whole battles quickly.</summary>
        public static float? SpeedOverride;

        /// <summary>Low quality tier turns off decorative sparks/projectiles.</summary>
        public static bool RichEffects = true;
        private readonly System.Collections.Generic.List<(Image dot, TextMeshProUGUI letter)> _orderDots = new System.Collections.Generic.List<(Image, TextMeshProUGUI)>();

        private BattleScreenArgs _args;
        private BattleSimulator _sim;
        private RectTransform _stage;
        private BattleFx _fx;
        private TextMeshProUGUI _wave, _round, _banner;
        private Button _speedButton, _autoButton;
        private RectTransform _turnOrder;
        private readonly Dictionary<int, UnitView> _units = new Dictionary<int, UnitView>();
        private readonly List<SkillButton> _skillButtons = new List<SkillButton>();
        private UnitView _acting;
        private SpeechBubble _bubble;
        private float _nextBubbleAt;
        private float _speed = 1f;
        private bool _paused, _skip, _left;
        public bool Finished { get; private set; }
        public int EventsPlayed { get; private set; }
        public int DesyncErrors { get; private set; }
        public BattleFx Fx => _fx;

        public override bool ShowTopBar => false;
        public override bool ShowTabBar => false;

        private sealed class SkillButton
        {
            public int Index;
            public Button Button;
            public Image Cooldown, Ready, Queued, Hp;
        }

        protected override void Build()
        {
            _args = (BattleScreenArgs)Args;
            _sim = _args.Battle.Simulator;
            _speed = SpeedOverride ?? Ctx.Session.Settings.BattleSpeed;
            var bg = UIKit.Image(Root, "Background", Color.Lerp(Th.background, Th.secondary, 0.15f), Th.pixel);
            bg.rectTransform.Stretch();
            var chapter = Ctx.Config.GetChapterAsset(_args.Battle.Fight?.ChapterId);
            var scene = chapter != null && chapter.background != null ? chapter.background : Th.battleBackground;
            if (scene != null)
            {
                var art = UIKit.Image(Root, "Scene", Color.white, scene);
                art.rectTransform.Stretch();
                art.preserveAspect = false;
                var fit = art.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = scene.rect.width / scene.rect.height;
                // darken behind the HUD and the skill bar so they stay readable
                UIKit.Image(Root, "TopShade", new Color(0, 0, 0, 0.45f)).rectTransform.TopBand(250);
                UIKit.Image(Root, "BottomShade", new Color(0, 0, 0, 0.45f)).rectTransform.BottomBand(340);
            }

            _stage = UIKit.Rect("Stage", Root).Stretch(0, 260, 0, 330);
            _fx = BattleFx.Create(_stage, Th);
            _bubble = SpeechBubble.Create(_stage, 520);

            BuildHud();
            foreach (var p in _sim.Players) SpawnUnit(p);
            BuildSkillBar();
            Ctx.PlaySfx("battle_start");
            StartCoroutine(PlayLoop());
        }

        // ---------------- layout ----------------

        private static Vector2 SlotAnchor(Team team, int slot, int count)
        {
            // Front row = slots 0-2 (closer to the middle), back row = 3-4.
            var front = slot < 3;
            var rowCount = front ? Math.Min(3, count) : Math.Max(0, count - 3);
            var i = front ? slot : slot - 3;
            var x = rowCount <= 1 ? 0.5f : 0.5f + (i - (rowCount - 1) / 2f) * (front ? 0.29f : 0.33f);
            float y;
            if (team == Team.Enemy) y = front ? 0.62f : 0.86f;
            else y = front ? 0.36f : 0.12f;
            return new Vector2(x, y);
        }

        private void SpawnUnit(Combatant c)
        {
            var count = c.Team == Team.Player ? _sim.Players.Count == 0 ? 5 : _sim.Players.Max(p => p.Slot) + 1 : _sim.Enemies.Count;
            var slot = c.Team == Team.Player ? c.Slot : _sim.Enemies.ToList().IndexOf(c);
            string name;
            Sprite sprite;
            Color tint;
            SpriteAnimSet anim;
            if (c.Team == Team.Player)
            {
                var a = Ctx.Config.GetHeroAsset(c.DefId);
                name = ScreenKit.HeroName(Ctx, c.DefId);
                sprite = a != null && a.portrait != null ? a.portrait : Th.circle;
                tint = a != null && a.portrait == null ? a.tint : Color.white;
                anim = a != null ? a.animations : null;
            }
            else
            {
                var a = Ctx.Config.GetEnemyAsset(c.DefId);
                name = ScreenKit.EnemyName(Ctx, c.DefId);
                sprite = a != null && a.sprite != null ? a.sprite : Th.diamond;
                tint = a != null ? a.tint : Color.gray;
                anim = a != null ? a.animations : null;
            }
            var size = c.IsBoss ? 300f : 190f;
            var anchor = c.IsBoss && c.Team == Team.Enemy ? new Vector2(0.5f, 0.8f) : SlotAnchor(c.Team, Mathf.Max(0, c.Team == Team.Player ? c.Slot : slot), Math.Max(1, count));
            var view = UnitView.Create(_stage, c, name, sprite, tint, anchor, size, Th, anim);
            view.SetAnimSpeed(_speed);
            _units[c.Index] = view;
            _fx.transform.SetAsLastSibling();
            _bubble.transform.SetAsLastSibling();
        }

        private void BuildHud()
        {
            var top = UIKit.Rect("Hud", Root).TopBand(250);
            var row = UIKit.Rect("Row", top).TopBand(130, 20);
            UIKit.HStack(row.gameObject, 12, 0, TextAnchor.MiddleCenter, expandWidth: false);
            var pause = UIKit.Button(row, "Pause", "II", ShowPause, UIKit.ButtonStyle.Ghost, Th.fontLarge);
            pause.Size(120, 116);
            TutorialAnchor.Mark(pause, "battle_pause");
            var info = UIKit.Rect("Info", row);
            UIKit.VStack(info.gameObject, 0, 0, TextAnchor.MiddleCenter);
            info.Size(preferredWidth: 250, preferredHeight: 116);
            _wave = UIKit.Text(info, "Wave", "", Th.fontBody, Th.text, TextAlignmentOptions.Center, bold: true);
            _wave.Size(preferredHeight: 60);
            _round = UIKit.Text(info, "Round", "", Th.fontSmall, Th.textDim, TextAlignmentOptions.Center);
            _round.Size(preferredHeight: 50);
            _autoButton = UIKit.Button(row, "Auto", "", ToggleAuto, UIKit.ButtonStyle.Ghost, Th.fontBody);
            _autoButton.Size(160, 116);
            TutorialAnchor.Mark(_autoButton, "battle_auto");
            _speedButton = UIKit.Button(row, "Speed", "", CycleSpeed, UIKit.ButtonStyle.Ghost, Th.fontBody);
            _speedButton.Size(130, 116);
            var skip = UIKit.Button(row, "Skip", Ctx.T("skip"), () => _skip = true, UIKit.ButtonStyle.Ghost, Th.fontBody);
            skip.Size(160, 116);
            TutorialAnchor.Mark(skip, "battle_skip");
            _turnOrder = UIKit.Rect("TurnOrder", top).TopBand(90, 160);
            UIKit.HStack(_turnOrder.gameObject, 8, 0, TextAnchor.MiddleCenter, expandWidth: false);
            _banner = UIKit.Text(Root, "Banner", "", 110, Th.primary, TextAlignmentOptions.Center, bold: true);
            _banner.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(1000, 220));
            _banner.outlineWidth = 0.25f;
            _banner.outlineColor = new Color32(0, 0, 0, 220);
            _banner.gameObject.SetActive(false);
            RefreshHud();
        }

        private void BuildSkillBar()
        {
            var bar = UIKit.Rect("Skills", Root).BottomBand(300, 20);
            UIKit.HStack(bar.gameObject, 10, 0, TextAnchor.MiddleCenter, expandWidth: false);
            foreach (var p in _sim.Players.OrderBy(p => p.Slot))
            {
                var index = p.Index;
                var cell = UIKit.Image(bar, "Skill" + index, Th.panel, Th.rounded, raycast: true);
                cell.Size(184, 270);
                var b = cell.gameObject.AddComponent<Button>();
                b.targetGraphic = cell;
                cell.gameObject.AddComponent<ButtonFeedback>();
                b.onClick.AddListener(() => QueueSkill(index));
                var portrait = ScreenKit.Portrait(Ctx, cell.transform, p.DefId, 150);
                portrait.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(150, 150), new Vector2(0, -14), new Vector2(0.5f, 1));
                var ready = UIKit.Image(cell.transform, "Ready", Th.primary.WithAlpha(0.6f), Th.ring);
                ready.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(176, 176), new Vector2(0, -1), new Vector2(0.5f, 1));
                var cd = UIKit.Image(cell.transform, "Cooldown", new Color(0, 0, 0, 0.65f), Th.circle);
                cd.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(150, 150), new Vector2(0, -14), new Vector2(0.5f, 1));
                cd.type = Image.Type.Filled;
                cd.fillMethod = Image.FillMethod.Radial360;
                var queued = UIKit.Image(cell.transform, "Queued", Th.positive, Th.circle);
                queued.rectTransform.Anchor(new Vector2(1, 1), new Vector2(40, 40), new Vector2(-8, -8), new Vector2(1, 1));
                var skillName = UIKit.Text(cell.transform, "SkillName", p.Active != null ? Ctx.T(p.Active.NameKey) : "", 22, Th.text, TextAlignmentOptions.Center);
                skillName.rectTransform.Stretch(6, 170, 6, 40);
                skillName.textWrappingMode = TextWrappingModes.NoWrap;
                skillName.enableAutoSizing = true; skillName.fontSizeMin = 12; skillName.fontSizeMax = 22;
                var hpBg = UIKit.Image(cell.transform, "HpBg", new Color(0, 0, 0, 0.6f), Th.rounded);
                hpBg.rectTransform.Anchor(new Vector2(0.5f, 0), new Vector2(160, 20), new Vector2(0, 14), new Vector2(0.5f, 0));
                var hp = UIKit.Image(hpBg.transform, "Hp", Th.positive, Th.rounded);
                hp.rectTransform.Stretch(2, 2, 2, 2);
                hp.type = Image.Type.Filled;
                hp.fillMethod = Image.FillMethod.Horizontal;
                TutorialAnchor.Mark(b, "battle_skill" + p.Slot);
                _skillButtons.Add(new SkillButton { Index = index, Button = b, Cooldown = cd, Ready = ready, Queued = queued, Hp = hp });
            }
        }

        private void Update()
        {
            if (_sim == null) return;
            foreach (var s in _skillButtons)
            {
                var c = _sim.Get(s.Index);
                var active = c.Active;
                var alive = c.Alive;
                s.Cooldown.fillAmount = !alive ? 1f : active == null ? 1f : active.Cooldown <= 0 ? 0f : Mathf.Clamp01((float)c.ActiveCooldown / Mathf.Max(1, active.Cooldown));
                s.Ready.enabled = alive && c.ActiveReady;
                s.Queued.enabled = alive && _sim.IsSkillQueued(s.Index);
                s.Hp.fillAmount = _units.TryGetValue(s.Index, out var v) ? Mathf.Clamp01((float)v.Hp / Mathf.Max(1, v.MaxHp)) : 0f;
                s.Button.interactable = alive && active != null && !_sim.IsOver;
            }
        }

        private void RefreshHud()
        {
            _wave.text = Ctx.F("wave_progress", _sim.WaveIndex + 1, _sim.WaveCount);
            _round.text = Ctx.F("round_n", Math.Max(1, _sim.Round));
            _speedButton.SetLabel("×" + _speed.ToString("0"));
            _autoButton.SetLabel(Ctx.T("auto"));
            ((Image)_autoButton.targetGraphic).color = _sim.AutoSkills ? Th.positive : Th.panelAlt;
        }

        /// <summary>Pooled dots (no allocations per turn once 8 exist).</summary>
        private void RefreshTurnOrder()
        {
            var upcoming = _sim.UpcomingThisRound;
            for (var i = 0; i < 8; i++)
            {
                if (i >= _orderDots.Count)
                {
                    var dot = UIKit.Image(_turnOrder, "T", Color.white, Th.circle);
                    dot.Size(70, 70);
                    var t = UIKit.Text(dot.transform, "I", "", 34, Color.black.WithAlpha(0.6f), TextAlignmentOptions.Center, bold: true);
                    t.rectTransform.Stretch();
                    _orderDots.Add((dot, t));
                }
                var (img, letter) = _orderDots[i];
                var active = i < upcoming.Count;
                img.gameObject.SetActive(active);
                if (!active) continue;
                var c = upcoming[i];
                img.color = c.Team == Team.Player ? Th.positive : Th.negative;
                var name = c.Team == Team.Player ? ScreenKit.HeroName(Ctx, c.DefId) : ScreenKit.EnemyName(Ctx, c.DefId);
                letter.text = name.Length > 0 ? name.Substring(0, 1) : "?";
                var art = c.Team == Team.Player ? ScreenKit.HeroIcon(Ctx.Config.GetHeroAsset(c.DefId)) : ScreenKit.EnemyIcon(Ctx.Config.GetEnemyAsset(c.DefId));
                var iconT = img.transform.Find("Icon");
                var icon = iconT != null ? iconT.GetComponent<Image>() : null;
                if (icon == null && art != null)
                {
                    icon = UIKit.Image(img.transform, "Icon", Color.white, art);
                    icon.rectTransform.Stretch(5, 5, 5, 5);
                }
                if (icon != null)
                {
                    icon.sprite = art;
                    icon.enabled = art != null;
                }
                if (art != null) letter.text = "";
            }
        }

        // ---------------- controls ----------------

        private void QueueSkill(int index)
        {
            if (_sim.QueuePlayerSkill(index))
            {
                Ctx.Haptics.Play(HapticKind.Light);
                Ctx.PlaySfx("ui_click");
            }
        }

        private void ToggleAuto()
        {
            _sim.AutoSkills = !_sim.AutoSkills;
            Ctx.Session.Settings.AutoSkill = _sim.AutoSkills;
            RefreshHud();
        }

        private void CycleSpeed()
        {
            _speed = _speed >= 3f ? 1f : _speed + 1f;
            Ctx.Session.Settings.BattleSpeed = _speed;
            foreach (var u in _units.Values) u.SetAnimSpeed(_speed);
            RefreshHud();
        }

        private void ShowPause()
        {
            if (_left) return;
            _paused = true;
            Ctx.Manager.ShowPopup<BattlePausePopup>(p =>
            {
                p.OnResume = () => _paused = false;
                p.OnRetreat = () => ConfirmPopup.Show(Ctx, Ctx.T("retreat_title"), Ctx.T("retreat_body"), () => Leave(retreat: true), () => _paused = false, danger: true);
            });
        }

        public override bool HandleBack()
        {
            ShowPause();
            return true;
        }

        private void Leave(bool retreat)
        {
            if (_left) return;
            _left = true;
            Finished = true;
            _args.Leave?.Invoke(retreat);
        }

        // ---------------- playback ----------------

        private float D(float seconds) => seconds / Mathf.Max(0.1f, _speed);

        private IEnumerator Wait(float seconds)
        {
            var end = Time.unscaledTime + D(seconds);
            while ((Time.unscaledTime < end || _paused) && !_skip && !_left) yield return null;
        }

        private IEnumerator PlayLoop()
        {
            yield return null;
            var cursor = 0;
            while (!_sim.IsOver && !_skip && !_left)
            {
                while (_paused && !_left) yield return null;
                if (_skip || _left) break;
                _sim.Step();
                RefreshTurnOrder();
                for (; cursor < _sim.Events.Count && !_skip && !_left; cursor++)
                {
                    var e = _sim.Events[cursor];
                    EventsPlayed++;
                    var wait = Play(e);
                    if (wait > 0) yield return Wait(wait);
                }
                CheckSync();
            }
            if (_left) yield break;
            if (_skip)
            {
                Leave(retreat: false); // BattleFlow.Finish runs the remaining turns instantly
                yield break;
            }
            yield return Wait(0.8f);
            Leave(retreat: false);
        }

        /// <summary>Applies one event to the views and returns how long to wait (seconds at ×1).</summary>
        private float Play(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.WaveStarted:
                    foreach (var c in _sim.Enemies) if (!_units.ContainsKey(c.Index)) SpawnUnit(c);
                    foreach (var u in _units.Values.Where(u => u.Team == Team.Enemy && u.Dead).ToList()) { Destroy(u.gameObject); _units.Remove(u.Index); }
                    RefreshHud();
                    var boss = _sim.Enemies.Any(x => x.IsBoss);
                    ShowBanner(boss ? Ctx.T("boss_banner") : Ctx.F("wave_n", e.Wave + 1), boss ? Th.negative : Th.text);
                    if (boss) Ctx.PlaySfx("boss");
                    var bossUnit = _sim.Enemies.FirstOrDefault(x => x.IsBoss);
                    if (bossUnit != null) Bark(bossUnit.Index, HeroLines.BattleStart, 1f, force: true);
                    else if (e.Wave == 0) Bark(RandomAlive(Team.Player), HeroLines.BattleStart, 1f, force: true);
                    else Bark(RandomAlive(Team.Enemy), HeroLines.BattleStart, 0.35f);
                    return boss ? 1.1f : 0.7f;
                case BattleEventType.RoundStarted:
                    RefreshHud();
                    return 0.05f;
                case BattleEventType.TurnStarted:
                    if (_acting != null) _acting.SetActing(false, D(0.1f));
                    if (_units.TryGetValue(e.Actor, out _acting)) _acting.SetActing(true, D(0.12f));
                    return 0.12f;
                case BattleEventType.ActionChosen:
                {
                    if (!_units.TryGetValue(e.Actor, out var actor)) return 0f;
                    var c = _sim.Get(e.Actor);
                    var isActive = c.Active != null && e.SkillId == c.Active.Id;
                    actor.PlayAction(isActive);
                    if (isActive)
                    {
                        _fx.FloatText(actor.LocalPos + new Vector2(0, 150), Ctx.T(c.Active.NameKey), Th.primary, 40, D(0.9f));
                        Ctx.PlaySfx("skill");
                        Bark(e.Actor, HeroLines.Skill, c.IsBoss ? 0.8f : 0.5f);
                    }
                    var first = e.Targets != null && e.Targets.Length > 0 && _units.TryGetValue(e.Targets[0], out var t0) ? t0 : null;
                    var ranged = c.Team == Team.Player ? IsRanged(c.DefId) : c.Basic != null && c.DefId.Contains("shaman") || c.DefId.Contains("archer") || c.DefId.Contains("cultist") || c.DefId.Contains("priest") || c.DefId.Contains("lich");
                    if (first != null && first.Team != actor.Team)
                    {
                        if (ranged && RichEffects)
                            foreach (var ti in e.Targets.Distinct())
                                if (_units.TryGetValue(ti, out var tv)) _fx.Projectile(actor.LocalPos, tv.LocalPos, isActive ? Th.primary : Th.text, D(0.25f));
                        else actor.Lunge(first.LocalPos, D(0.28f));
                    }
                    else if (first != null && RichEffects) _fx.Spark(first.LocalPos, Th.positive, 220, D(0.35f));
                    return 0.3f;
                }
                case BattleEventType.Stunned:
                    if (_units.TryGetValue(e.Actor, out var stunned)) _fx.FloatText(stunned.LocalPos + new Vector2(0, 120), Ctx.T("stunned"), Th.warning, 36, D(0.7f));
                    return 0.35f;
                case BattleEventType.DamageDealt:
                {
                    if (!_units.TryGetValue(e.Target, out var target)) return 0f;
                    var shieldAfter = target.Shield - e.Absorbed;
                    target.SetHp(e.HpAfter, Math.Max(0, shieldAfter));
                    target.Flash(D(0.2f));
                    if (e.HpAfter > 0) target.PlayHit();
                    if (target.Team == Team.Player && target.CrossedLowHp()) Bark(e.Target, HeroLines.LowHp, 0.7f);
                    _fx.Spark(target.LocalPos, e.Crit ? Th.warning : Color.white, e.Crit ? 260 : 180, D(0.25f));
                    _fx.FloatText(target.LocalPos + new Vector2(UnityEngine.Random.Range(-30f, 30f), 80), (e.Crit ? Ctx.T("crit") + " " : "") + e.Amount, e.Crit ? Th.warning : Th.negative, e.Crit ? 64 : 48, D(0.8f));
                    if (e.Crit && Ctx.Session.Settings.ScreenShake && !Tween.ReduceMotion) target.Shake(D(0.25f), 18);
                    Ctx.PlaySfx(e.Crit ? "crit" : "hit");
                    if (e.Crit) Ctx.Haptics.Play(HapticKind.Light);
                    return 0.22f;
                }
                case BattleEventType.HealApplied:
                    if (_units.TryGetValue(e.Target, out var healed))
                    {
                        healed.SetHp(e.HpAfter, healed.Shield);
                        _fx.FloatText(healed.LocalPos + new Vector2(0, 80), "+" + e.Amount, Th.positive, 48, D(0.8f));
                        Ctx.PlaySfx("heal");
                    }
                    return 0.2f;
                case BattleEventType.ShieldApplied:
                    if (_units.TryGetValue(e.Target, out var shielded))
                    {
                        shielded.SetHp(shielded.Hp, shielded.Shield + e.Amount);
                        _fx.FloatText(shielded.LocalPos + new Vector2(0, 80), "+" + e.Amount, Th.secondary, 44, D(0.8f));
                    }
                    return 0.2f;
                case BattleEventType.StatusApplied:
                    if (_units.TryGetValue(e.Target, out var st))
                    {
                        var buff = e.StatusId != "stun" && e.Value > 0;
                        st.AddStatus(e.StatusId, buff, Th, Th.StatusIcon(e.StatusId, e.Stat, buff));
                        var label = e.StatusId == "stun" ? Ctx.T("stunned") : $"{ScreenKit.StatName(Ctx, e.Stat)} {(e.Value > 0 ? "+" : "")}{e.Value * 100:0}%";
                        _fx.FloatText(st.LocalPos + new Vector2(0, 130), label, buff ? Th.positive : e.StatusId == "stun" ? Th.warning : Th.negative, 32, D(0.8f));
                    }
                    return 0.18f;
                case BattleEventType.StatusExpired:
                    if (_units.TryGetValue(e.Target, out var exp) && !_sim.Get(e.Target).Statuses.Any(s => s.StatusId == e.StatusId)) exp.RemoveStatus(e.StatusId);
                    return 0f;
                case BattleEventType.ShieldExpired:
                    if (_units.TryGetValue(e.Target, out var se)) se.SetHp(se.Hp, 0);
                    return 0f;
                case BattleEventType.CombatantDied:
                    if (_units.TryGetValue(e.Target, out var dead))
                    {
                        dead.SetHp(0, 0);
                        dead.Die(D(0.35f));
                        Ctx.PlaySfx("death");
                        var deadBoss = _sim.Get(e.Target) != null && _sim.Get(e.Target).IsBoss;
                        if (dead.Team == Team.Player) Bark(RandomAlive(Team.Player), HeroLines.AllyDown, 0.6f);
                        else if (deadBoss) Bark(e.Target, HeroLines.Defeat, 1f, force: true);
                        else if (_units.TryGetValue(e.Actor, out var killer) && killer.Team == Team.Player) Bark(e.Actor, HeroLines.Kill, 0.3f);
                    }
                    return 0.35f;
                case BattleEventType.WaveCleared:
                    return 0.4f;
                case BattleEventType.BattleEnded:
                    if (_acting != null) _acting.SetActing(false, D(0.1f));
                    var win = e.Outcome == BattleOutcome.Victory;
                    if (win) foreach (var u in _units.Values) if (u.Team == Team.Player) u.PlayVictory();
                    Bark(win ? RandomAlive(Team.Player) : _sim.Players.Count > 0 ? _sim.Players[UnityEngine.Random.Range(0, _sim.Players.Count)].Index : -1,
                        win ? HeroLines.Victory : HeroLines.Defeat, 1f, force: true);
                    ShowBanner(Ctx.T(win ? "victory" : e.Outcome == BattleOutcome.Timeout ? "timeout" : "defeat"), win ? Th.primary : Th.negative);
                    Ctx.PlaySfx(win ? "victory" : "defeat");
                    return 1.2f;
                default:
                    return 0f;
            }
        }

        // ---------------- dialogue barks (presentation only; never touches the simulation)

        private int RandomAlive(Team team)
        {
            var alive = _units.Values.Where(u => u.Team == team && !u.Dead && _sim.Get(u.Index) != null && _sim.Get(u.Index).Alive).ToList();
            return alive.Count == 0 ? -1 : alive[UnityEngine.Random.Range(0, alive.Count)].Index;
        }

        /// <summary>Speech bubble over a unit with the given chance; rate-limited so lines never pile up.</summary>
        private void Bark(int index, string trigger, float chance, bool force = false)
        {
            if (index < 0 || _bubble == null || _skip || _speed > 3.5f || !_units.TryGetValue(index, out var unit)) return;
            if (!force && (Time.unscaledTime < _nextBubbleAt || UnityEngine.Random.value > chance)) return;
            var c = _sim.Get(index);
            var line = c != null ? HeroLines.Pick(Ctx, c.DefId, trigger) : null;
            if (line == null) return;
            var hold = D(1.8f);
            var half = _stage.rect.width * 0.5f - 270f;
            var tip = unit.LocalPos + new Vector2(0, unit.Size * 0.62f);
            tip.x = Mathf.Clamp(tip.x, -half, half);
            _bubble.Show(line, tip, hold, 28, 520);
            _nextBubbleAt = Time.unscaledTime + hold + D(0.6f);
        }

        private bool IsRanged(string heroId) =>
            Ctx.Session.Db.TryGetHero(heroId, out var h) && (h.Class == HeroClass.Ranger || h.Class == HeroClass.Mage || h.Class == HeroClass.Support);

        private void ShowBanner(string text, Color color)
        {
            _banner.text = text;
            _banner.color = color;
            _banner.gameObject.SetActive(true);
            Tween.Scale(_banner.transform, 0.5f, 1f, D(0.3f), Ease.OutBack);
            Tween.To(_banner, 0f, 1f, D(1.0f), _ => { }, Ease.Linear, () => { if (_banner != null) _banner.gameObject.SetActive(false); });
        }

        /// <summary>Dev builds: every unit's bars must match the simulator after each turn.</summary>
        private void CheckSync()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var kv in _units)
            {
                var c = _sim.Get(kv.Key);
                if (c == null || (c.Team == Team.Enemy && c.Wave != _sim.WaveIndex && !kv.Value.Dead)) continue;
                if (kv.Value.Hp != c.Hp || kv.Value.Shield != c.Shield)
                {
                    DesyncErrors++;
                    Debug.LogError($"[BattleView] desync on #{c.Index} {c.DefId}: view {kv.Value.Hp}/{kv.Value.Shield} vs sim {c.Hp}/{c.Shield}");
                    kv.Value.SetHp(c.Hp, c.Shield);
                }
            }
#endif
        }
    }

    public sealed class BattlePausePopup : PopupBase
    {
        public Action OnResume, OnRetreat;
        public override bool CloseOnBackdrop => false;

        protected override void Build()
        {
            UIKit.Text(Panel, "Title", Ctx.T("paused"), Th.fontTitle, Th.text, TextAlignmentOptions.Center, bold: true).Size(preferredHeight: 100);
            UIKit.Button(Panel, "Resume", Ctx.T("resume"), () => { Close(); OnResume?.Invoke(); });
            UIKit.Button(Panel, "Retreat", Ctx.T("retreat"), () => { Close(); OnRetreat?.Invoke(); }, UIKit.ButtonStyle.Danger);
        }

        public override bool HandleBack()
        {
            Close();
            OnResume?.Invoke();
            return true;
        }
    }
}
