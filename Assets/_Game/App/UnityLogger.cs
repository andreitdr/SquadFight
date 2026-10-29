using Game.Core;

namespace Game.App
{
    /// <summary>The only place game code talks to UnityEngine.Debug.</summary>
    public sealed class UnityLogger : IGameLogger
    {
        public void Info(string message) => UnityEngine.Debug.Log(message);
        public void Warning(string message) => UnityEngine.Debug.LogWarning(message);
        public void Error(string message) => UnityEngine.Debug.LogError(message);
    }
}
