using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.App
{
    /// <summary>Optional visual transition around scene loads (implemented by the UI layer later).</summary>
    public interface ISceneTransition
    {
        IEnumerator FadeOut();
        IEnumerator FadeIn();
    }

    /// <summary>Async single-scene loader with a transition hook. Lives on the persistent Boot object.</summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        public ISceneTransition Transition { get; set; }
        public bool IsLoading { get; private set; }
        public string CurrentScene => SceneManager.GetActiveScene().name;

        public event Action<string> SceneLoaded;

        public void Load(string sceneName, Action onComplete = null)
        {
            if (IsLoading) return;
            StartCoroutine(LoadRoutine(sceneName, onComplete));
        }

        private IEnumerator LoadRoutine(string sceneName, Action onComplete)
        {
            IsLoading = true;
            if (Transition != null) yield return Transition.FadeOut();
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            if (Transition != null) yield return Transition.FadeIn();
            IsLoading = false;
            SceneLoaded?.Invoke(sceneName);
            onComplete?.Invoke();
        }
    }
}
