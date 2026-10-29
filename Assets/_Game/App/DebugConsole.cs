#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Game.Services;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.App
{
    /// <summary>Dev-only text console (IMGUI). Toggle: backquote key in the Editor, four-finger tap on device. Stripped from release builds.</summary>
    public sealed class DebugConsole : MonoBehaviour
    {
        public DebugCommands Commands { get; set; }

        private bool _visible;
        private bool _fourDown;
        private string _input = "";
        private readonly List<string> _log = new List<string> { "Type 'help'." };
        private Vector2 _scroll;
        private GUIStyle _style;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame) _visible = !_visible;
            var touches = Touchscreen.current?.touches;
            var active = 0;
            if (touches != null)
                foreach (var t in touches)
                    if (t.isInProgress) active++;
            if (active >= 4 && !_fourDown) _visible = !_visible;
            _fourDown = active >= 4;
        }

        private void OnGUI()
        {
            if (!_visible || Commands == null) return;
            var scale = Mathf.Max(1f, Screen.height / 900f);
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * scale), wordWrap = true };
            var area = Screen.safeArea;
            var rect = new Rect(area.x + 8, Screen.height - area.yMax + 8, area.width - 16, area.height * 0.45f);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 6, rect.y + 6, rect.width - 12, rect.height - 12));
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var line in _log) GUILayout.Label(line, _style);
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("debug_input");
            _input = GUILayout.TextField(_input, GUILayout.Height(28 * scale));
            var submit = GUILayout.Button("Run", GUILayout.Width(70 * scale), GUILayout.Height(28 * scale))
                         || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.FocusControl("debug_input");
            if (submit && _input.Trim().Length > 0)
            {
                _log.Add("> " + _input);
                _log.Add(Commands.Execute(_input));
                if (_log.Count > 200) _log.RemoveRange(0, _log.Count - 200);
                _input = "";
                _scroll.y = float.MaxValue;
            }
        }
    }
}
#endif
