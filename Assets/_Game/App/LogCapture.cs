using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Services;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Keeps the last log lines in memory and in persistentDataPath/last_log.txt (written on pause/quit/errors) so players
    /// can send an offline bug report via Settings → Copy debug info. No data ever leaves the device by itself.
    /// </summary>
    public sealed class LogCapture : MonoBehaviour
    {
        private const int MaxLines = 300;
        private readonly Queue<string> _lines = new Queue<string>();
        private readonly object _lock = new object();
        private string _path;
        private bool _dirty;
        private float _lastWrite;

        public static LogCapture Install(GameObject host, string folder)
        {
            var c = host.AddComponent<LogCapture>();
            c._path = Path.Combine(folder, "last_log.txt");
            Application.logMessageReceivedThreaded += c.OnLog;
            return c;
        }

        private void OnDestroy() => Application.logMessageReceivedThreaded -= OnLog;

        private void OnLog(string message, string stackTrace, LogType type)
        {
            var line = $"{DateTime.UtcNow:HH:mm:ss} [{type}] {message}";
            if (type == LogType.Exception || type == LogType.Error) line += "\n" + Truncate(stackTrace, 600);
            lock (_lock)
            {
                _lines.Enqueue(line);
                while (_lines.Count > MaxLines) _lines.Dequeue();
                _dirty = true;
            }
        }

        private static string Truncate(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s.Substring(0, n) + "…";

        public string Recent(int count)
        {
            lock (_lock)
            {
                var arr = _lines.ToArray();
                var start = Math.Max(0, arr.Length - count);
                return string.Join("\n", arr, start, arr.Length - start);
            }
        }

        private void Update()
        {
            if (_dirty && Time.unscaledTime - _lastWrite > 10f) Flush();
        }

        private void OnApplicationPause(bool paused) { if (paused) Flush(); }
        private void OnApplicationQuit() => Flush();

        public void Flush()
        {
            if (!_dirty || _path == null) return;
            _lastWrite = Time.unscaledTime;
            try
            {
                string text;
                lock (_lock) { text = string.Join("\n", _lines); _dirty = false; }
                File.WriteAllText(_path, text);
            }
            catch (Exception) { /* logging must never break the game */ }
        }

        /// <summary>Plain-text report for the clipboard.</summary>
        public static string BuildDebugInfo(GameSession session, LogCapture capture)
        {
            var d = session.State.Data;
            var sb = new StringBuilder();
            sb.AppendLine($"SquadFight {Application.version} ({Application.platform}, {(Debug.isDebugBuild ? "dev" : "release")})");
            sb.AppendLine($"Device: {SystemInfo.deviceModel} · {SystemInfo.operatingSystem} · RAM {SystemInfo.systemMemorySize} MB · GPU {SystemInfo.graphicsDeviceName}");
            sb.AppendLine($"Screen: {Screen.width}x{Screen.height} safe {Screen.safeArea} · lang {d.Settings.Language}");
            sb.AppendLine($"Save v{d.Version}: fights {session.Progress.TotalClearedFights}/{session.Db.Fights.Count} cleared, total {d.Progress.TotalFights}, heroes {session.Roster.UnlockedHeroIds.Count()}/{session.Db.Heroes.Count}, items {d.Equipment.Count}, gold {session.Currency.Get(session.Db.Rules.PrimaryCurrencyId)}");
            sb.AppendLine("--- recent log ---");
            sb.AppendLine(capture != null ? capture.Recent(80) : "(no log)");
            return sb.ToString();
        }
    }
}
