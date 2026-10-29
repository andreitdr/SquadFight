using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.App
{
    /// <summary>Optional visual transition around scene loads (implemented by the UI layer).</summary>
    public interface ISceneTransition
    {
        IEnumerator FadeOut();
        IEnumerator FadeIn();
    }

    /// <summary>
    /// Async single-scene loader with a transition hook. Lives on the persistent Boot object.
    /// A load requested while another is in progress (e.g. a scene that finishes immediately in Start) is queued.
    /// </summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        private string _queued;
        private Action _queuedDone;

        public ISceneTransition Transition { get; set; }
        public bool IsLoading { get; private set; }
        public string CurrentScene => SceneManager.GetActiveScene().name;

        public event Action<string> SceneLoaded;

        public void Load(string sceneName, Action onComplete = null)
        {
            if (IsLoading)
            {
                _queued = sceneName;
                _queuedDone = onComplete;
                return;
            }
            StartCoroutine(LoadRoutine(sceneName, onComplete));
        }

        /// <summary>The transition belongs to a scene object; once that scene unloads it is gone.</summary>
        private ISceneTransition LiveTransition => Transition is UnityEngine.Object o && o == null ? null : Transition;

        private IEnumerator LoadRoutine(string sceneName, Action onComplete)
        {
            IsLoading = true;
            var t = LiveTransition;
            if (t != null) yield return t.FadeOut();
            Transition = null;
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            t = LiveTransition; // the new scene may register its own transition in Start
            yield return null;
            t = LiveTransition;
            if (t != null) yield return t.FadeIn();
            IsLoading = false;
            SceneLoaded?.Invoke(sceneName);
            onComplete?.Invoke();
            if (_queued != null)
            {
                var next = _queued;
                var done = _queuedDone;
                _queued = null;
                _queuedDone = null;
                Load(next, done);
            }
        }
    }
}
