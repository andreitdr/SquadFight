namespace Game.Core
{
    public interface IGameLogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
    }

    public sealed class NullLogger : IGameLogger
    {
        public static readonly NullLogger Instance = new NullLogger();
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) { }
    }
}
