using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    public enum Ease { Linear, OutQuad, InQuad, InOutQuad, OutBack, OutCubic }

    /// <summary>Minimal tween engine (D4: own helper, no DOTween). Unscaled time; honours <see cref="ReduceMotion"/>.</summary>
    public sealed class Tween : MonoBehaviour
    {
        private sealed class Job
        {
            public float From, To, Duration, Elapsed, Delay;
            public Action<float> Setter;
            public Action Done;
            public Ease Ease;
            public UnityEngine.Object Owner;
            public bool HasOwner;
            public int Id;
        }

        private static Tween _runner;
        private static int _nextId = 1;
        private readonly List<Job> _jobs = new List<Job>();
        private readonly List<Job> _adding = new List<Job>();

        /// <summary>Accessibility: when true every tween completes instantly.</summary>
        public static bool ReduceMotion { get; set; }

        public static int ActiveCount => _runner == null ? 0 : _runner._jobs.Count + _runner._adding.Count;

        private static Tween Runner
        {
            get
            {
                if (_runner != null) return _runner;
                var go = new GameObject("[Tween]") { hideFlags = HideFlags.HideInHierarchy };
                if (Application.isPlaying) DontDestroyOnLoad(go);
                _runner = go.AddComponent<Tween>();
                return _runner;
            }
        }

        /// <summary>Animates a float; the tween stops silently if <paramref name="owner"/> is destroyed.</summary>
        public static int To(UnityEngine.Object owner, float from, float to, float duration, Action<float> setter, Ease ease = Ease.OutQuad, Action done = null, float delay = 0f)
        {
            if (ReduceMotion || duration <= 0f)
            {
                setter(to);
                done?.Invoke();
                return 0;
            }
            setter(from);
            var job = new Job { From = from, To = to, Duration = duration, Setter = setter, Done = done, Ease = ease, Owner = owner, HasOwner = owner != null, Delay = delay, Id = _nextId++ };
            Runner._adding.Add(job);
            return job.Id;
        }

        public static void Kill(int id)
        {
            if (_runner == null || id == 0) return;
            _runner._jobs.RemoveAll(j => j.Id == id);
            _runner._adding.RemoveAll(j => j.Id == id);
        }

        public static int Fade(CanvasGroup g, float to, float duration, Action done = null, float delay = 0f) =>
            To(g, g.alpha, to, duration, v => g.alpha = v, Ease.OutQuad, done, delay);

        public static int Scale(Transform t, float from, float to, float duration, Ease ease = Ease.OutBack, Action done = null, float delay = 0f) =>
            To(t, from, to, duration, v => t.localScale = new Vector3(v, v, 1), ease, done, delay);

        public static int Move(RectTransform t, Vector2 from, Vector2 to, float duration, Ease ease = Ease.OutCubic, Action done = null, float delay = 0f) =>
            To(t, 0, 1, duration, k => t.anchoredPosition = Vector2.LerpUnclamped(from, to, k), ease, done, delay);

        public static float Evaluate(Ease e, float t)
        {
            switch (e)
            {
                case Ease.OutQuad: return 1 - (1 - t) * (1 - t);
                case Ease.InQuad: return t * t;
                case Ease.InOutQuad: return t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
                case Ease.OutCubic: return 1 - Mathf.Pow(1 - t, 3);
                case Ease.OutBack:
                    const float c1 = 1.70158f, c3 = c1 + 1;
                    return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
                default: return t;
            }
        }

        private void Update()
        {
            if (_adding.Count > 0)
            {
                _jobs.AddRange(_adding);
                _adding.Clear();
            }
            var dt = Time.unscaledDeltaTime;
            for (var i = _jobs.Count - 1; i >= 0; i--)
            {
                var j = _jobs[i];
                if (j.HasOwner && j.Owner == null) { _jobs.RemoveAt(i); continue; }
                if (j.Delay > 0) { j.Delay -= dt; continue; }
                j.Elapsed += dt;
                var k = Mathf.Clamp01(j.Elapsed / j.Duration);
                try { j.Setter(Mathf.LerpUnclamped(j.From, j.To, Evaluate(j.Ease, k))); }
                catch (MissingReferenceException) { _jobs.RemoveAt(i); continue; }
                if (k < 1f) continue;
                _jobs.RemoveAt(i);
                j.Done?.Invoke();
            }
        }
    }
}
