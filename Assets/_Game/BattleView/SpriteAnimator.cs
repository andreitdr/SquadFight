using System;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.BattleView
{
    /// <summary>
    /// Plays <see cref="SpriteAnimSet"/> clips on a uGUI Image. One-shot clips return to idle (or hold their last frame,
    /// e.g. death). Runs on unscaled time × <see cref="Speed"/> so it follows the battle speed and ignores timeScale.
    /// </summary>
    public sealed class SpriteAnimator : MonoBehaviour
    {
        public SpriteAnimSet Set;
        public Image Target;
        public float Speed = 1f;
        private SpriteAnimSet.Clip _clip;
        private float _time;
        private bool _hold;
        private Action _done;

        public string Current => _clip?.name;

        public static SpriteAnimator Attach(Image target, SpriteAnimSet set)
        {
            var a = target.gameObject.AddComponent<SpriteAnimator>();
            a.Set = set;
            a.Target = target;
            a.Play(SpriteAnimSet.Idle);
            return a;
        }

        /// <summary>Plays a clip; missing clips are ignored (falls back to idle). <paramref name="hold"/> keeps the last frame.</summary>
        public void Play(string clipName, bool hold = false, Action done = null)
        {
            var clip = Set != null ? Set.Get(clipName) : null;
            if (clip == null)
            {
                done?.Invoke();
                if (_clip == null && Set != null) clip = Set.Get(SpriteAnimSet.Idle);
                if (clip == null) return;
            }
            _clip = clip;
            _time = 0;
            _hold = hold;
            _done = done;
            Apply(0);
        }

        /// <summary>Total length of a clip at the current speed (0 if missing).</summary>
        public float Length(string clipName)
        {
            var c = Set != null ? Set.Get(clipName) : null;
            return c == null ? 0 : c.Duration / Mathf.Max(0.1f, Speed);
        }

        private void Update()
        {
            if (_clip == null) return;
            _time += Time.unscaledDeltaTime * Speed;
            var frame = Mathf.FloorToInt(_time * _clip.fps);
            if (frame >= _clip.frames.Length)
            {
                if (_clip.loop) frame %= _clip.frames.Length;
                else
                {
                    var done = _done;
                    _done = null;
                    if (_hold) { Apply(_clip.frames.Length - 1); _clip = null; }
                    else Play(SpriteAnimSet.Idle);
                    done?.Invoke();
                    return;
                }
            }
            Apply(frame);
        }

        private void Apply(int frame)
        {
            if (Target != null && _clip != null && _clip.frames.Length > 0) Target.sprite = _clip.frames[Mathf.Clamp(frame, 0, _clip.frames.Length - 1)];
        }
    }
}
