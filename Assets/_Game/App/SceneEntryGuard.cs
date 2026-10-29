using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.App
{
    /// <summary>
    /// Placed in Main/Battle. If the scene is played directly (Editor) without passing through Boot,
    /// load Boot first and come back here afterwards.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public sealed class SceneEntryGuard : MonoBehaviour
    {
        public static bool RedirectedToBoot { get; private set; }

        private void Awake()
        {
            if (AppContext.IsBooted) return;
            RedirectedToBoot = true;
            GameBootstrap.PendingScene = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(SceneNames.Boot, LoadSceneMode.Single);
        }
    }
}
