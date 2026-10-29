using Game.Core;
using Game.Save;
using Game.Services;

namespace Game.Tests.EditMode
{
    /// <summary>Counts saves; used instead of a real SaveManager in service tests.</summary>
    public sealed class RecordingSaver : ISaveScheduler
    {
        public int DirtyMarks;
        public int Saves;
        public void MarkDirty() => DirtyMarks++;
        public bool SaveNow() { Saves++; return true; }
    }

    public static class TestSession
    {
        public static GameSession Create(out RecordingSaver saver, GameDatabase db = null, ulong seed = 1)
        {
            db ??= TestContent.Create();
            saver = new RecordingSaver();
            var clock = new ManualClock();
            return new GameSession(db, NewGameFactory.Create(db, clock), saver, new SeededRandom(seed), clock);
        }

        public static GameSession Create(GameDatabase db = null, ulong seed = 1) => Create(out _, db, seed);
    }
}
