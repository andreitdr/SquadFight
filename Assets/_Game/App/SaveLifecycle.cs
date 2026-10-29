using Game.Save;
using UnityEngine;

namespace Game.App
{
    /// <summary>Drives <see cref="SaveManager"/> from Unity: debounced ticks every frame, flush on pause/focus loss/quit.</summary>
    public sealed class SaveLifecycle : MonoBehaviour
    {
        public SaveManager Manager { get; set; }

        private void Update() => Manager?.Tick();

        private void OnApplicationPause(bool paused)
        {
            if (paused) Manager?.Flush();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Manager?.Flush();
        }

        private void OnApplicationQuit() => Manager?.Flush();
    }
}
