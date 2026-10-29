#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.App
{
    /// <summary>Dev-only FPS/memory overlay. Toggle: three-finger tap on device, F3 in the Editor. Stripped from release builds.</summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        private bool _visible;
        private float _accum;
        private int _frames;
        private float _fps;
        private bool _threeFingerDown;
        private GUIStyle _style;

        private void Update()
        {
            _accum += Time.unscaledDeltaTime;
            _frames++;
            if (_accum >= 0.5f)
            {
                _fps = _frames / _accum;
                _accum = 0f;
                _frames = 0;
            }

            var touches = Touchscreen.current?.touches;
            var active = 0;
            if (touches != null)
                foreach (var t in touches)
                    if (t.isInProgress) active++;
            if (active >= 3 && !_threeFingerDown) _visible = !_visible;
            _threeFingerDown = active >= 3;

            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 60) };
            var mono = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / (1024f * 1024f);
            var total = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
            var area = Screen.safeArea;
            GUI.Label(new Rect(area.x + 10, Screen.height - area.yMax + 10, 600, 200),
                $"FPS {_fps:0.0}\nMono {mono:0.0} MB\nTotal {total:0.0} MB", _style);
        }
    }
}
#endif
