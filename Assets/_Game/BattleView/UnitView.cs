using System.Collections.Generic;
using Game.Core;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.BattleView
{
    /// <summary>One combatant on the battlefield: body sprite, HP/shield bars, status icons, name. Mirrors simulator state.</summary>
    public sealed class UnitView : MonoBehaviour
    {
        public int Index;
        public Team Team;
        public int Hp, MaxHp, Shield;
        public RectTransform Body;
        public Image BodyImage;
        private Image _hpFill, _shieldFill, _highlight;
        private RectTransform _statusRow;
        private readonly Dictionary<string, Image> _statusIcons = new Dictionary<string, Image>();
        private Color _baseColor;
        private Vector2 _home;
        private int _flashTween, _moveTween;

        public Vector2 Home => _home;
        public bool Dead { get; private set; }

        public static UnitView Create(Transform parent, Combatant c, string displayName, Sprite sprite, Color tint, Vector2 anchor, float size, Theme th)
        {
            var root = UIKit.Rect("Unit_" + c.Index, parent);
            root.anchorMin = root.anchorMax = anchor;
            root.pivot = new Vector2(0.5f, 0.6f);
            root.sizeDelta = new Vector2(size, size + 90);
            root.anchoredPosition = Vector2.zero;
            var v = root.gameObject.AddComponent<UnitView>();
            v.Index = c.Index;
            v.Team = c.Team;
            v.MaxHp = c.MaxHp;
            v.Hp = c.Hp;
            v._home = Vector2.zero;
            v._highlight = UIKit.Image(root, "Highlight", th.primary.WithAlpha(0f), th.softDot);
            v._highlight.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(size * 1.5f, size * 1.5f), new Vector2(0, size * 0.25f), new Vector2(0.5f, 1));
            v.Body = UIKit.Rect("Body", root);
            v.Body.Anchor(new Vector2(0.5f, 1), new Vector2(size, size), Vector2.zero, new Vector2(0.5f, 1));
            v.BodyImage = UIKit.Image(v.Body, "Sprite", tint, sprite);
            v.BodyImage.rectTransform.Stretch();
            v._baseColor = tint;
            var initial = UIKit.Text(v.BodyImage.transform, "Initial", displayName.Length > 0 ? displayName.Substring(0, 1) : "?", size * 0.45f, new Color(0, 0, 0, 0.5f), TextAlignmentOptions.Center, bold: true);
            initial.rectTransform.Stretch();
            var hpBg = UIKit.Image(root, "HpBg", new Color(0, 0, 0, 0.6f), th.rounded);
            hpBg.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(size, 22), new Vector2(0, -size - 8), new Vector2(0.5f, 1));
            v._hpFill = UIKit.Image(hpBg.transform, "Fill", c.Team == Team.Player ? th.positive : th.negative, th.rounded);
            v._hpFill.rectTransform.Stretch(2, 2, 2, 2);
            v._hpFill.type = Image.Type.Filled;
            v._hpFill.fillMethod = Image.FillMethod.Horizontal;
            v._shieldFill = UIKit.Image(hpBg.transform, "Shield", th.secondary.WithAlpha(0.85f), th.rounded);
            v._shieldFill.rectTransform.Stretch(2, 2, 2, 12);
            v._shieldFill.type = Image.Type.Filled;
            v._shieldFill.fillMethod = Image.FillMethod.Horizontal;
            v._shieldFill.fillAmount = 0;
            var name = UIKit.Text(root, "Name", displayName, 24, th.text, TextAlignmentOptions.Center);
            name.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(size + 60, 32), new Vector2(0, -size - 32), new Vector2(0.5f, 1));
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true; name.fontSizeMin = 14; name.fontSizeMax = 24;
            v._statusRow = UIKit.Rect("Status", root);
            v._statusRow.Anchor(new Vector2(0.5f, 1), new Vector2(size, 28), new Vector2(0, -size - 64), new Vector2(0.5f, 1));
            UIKit.HStack(v._statusRow.gameObject, 4, 0, TextAnchor.MiddleCenter, expandWidth: false);
            v.SetHp(c.Hp, c.Shield);
            return v;
        }

        public void SetHp(int hp, int shield)
        {
            Hp = hp;
            Shield = shield;
            _hpFill.fillAmount = MaxHp <= 0 ? 0 : Mathf.Clamp01((float)hp / MaxHp);
            _shieldFill.fillAmount = MaxHp <= 0 ? 0 : Mathf.Clamp01((float)shield / MaxHp);
        }

        public void SetActing(bool on, float duration)
        {
            var target = on ? 0.55f : 0f;
            Tween.To(_highlight, _highlight.color.a, target, duration, a => _highlight.color = _highlight.color.WithAlpha(a));
            if (on) Tween.Scale(Body, 1f, 1.08f, duration, Ease.OutBack);
            else Tween.Scale(Body, Body.localScale.x, 1f, duration);
        }

        public void Flash(float duration)
        {
            Tween.Kill(_flashTween);
            _flashTween = Tween.To(BodyImage, 1f, 0f, duration, k => BodyImage.color = Color.Lerp(_baseColor, Color.white, k));
        }

        /// <summary>Position in the parent's space (where the Fx layer draws).</summary>
        public Vector2 LocalPos => transform.localPosition;

        /// <summary>Lunge toward a point (parent space) and back.</summary>
        public void Lunge(Vector2 toward, float duration)
        {
            var rt = (RectTransform)transform;
            var dir = (toward - LocalPos).normalized * 70f;
            Tween.Kill(_moveTween);
            _moveTween = Tween.To(rt, 0f, 1f, duration, k => rt.anchoredPosition = _home + dir * Mathf.Sin(k * Mathf.PI), Ease.Linear);
        }

        public void Shake(float duration, float strength = 14f)
        {
            var rt = (RectTransform)transform;
            Tween.Kill(_moveTween);
            _moveTween = Tween.To(rt, 0f, 1f, duration, k => rt.anchoredPosition = _home + new Vector2(Mathf.Sin(k * 40f) * strength * (1 - k), 0), Ease.Linear, () => rt.anchoredPosition = _home);
        }

        public void AddStatus(string id, bool buff, Theme th)
        {
            if (_statusIcons.ContainsKey(id)) return;
            var icon = UIKit.Image(_statusRow, "S_" + id, id == "stun" ? th.warning : buff ? th.positive : th.negative, id == "stun" ? th.star : th.triangle);
            icon.Size(26, 26);
            if (!buff && id != "stun") icon.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
            _statusIcons[id] = icon;
        }

        public void RemoveStatus(string id)
        {
            if (!_statusIcons.TryGetValue(id, out var icon)) return;
            _statusIcons.Remove(id);
            Destroy(icon.gameObject);
        }

        public void ClearStatuses()
        {
            foreach (var i in _statusIcons.Values) Destroy(i.gameObject);
            _statusIcons.Clear();
        }

        public void Die(float duration)
        {
            Dead = true;
            ClearStatuses();
            var group = gameObject.TryGetComponent<CanvasGroup>(out var existing) ? existing : gameObject.AddComponent<CanvasGroup>();
            Tween.Fade(group, 0.15f, duration);
            Tween.Scale(Body, 1f, 0.7f, duration, Ease.InQuad);
        }
    }
}
