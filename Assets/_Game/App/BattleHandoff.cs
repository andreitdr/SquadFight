using Game.Services;

namespace Game.App
{
    /// <summary>Carries a battle between scenes: Main starts it, Battle plays it, Main shows the recorded result.</summary>
    public sealed class BattleHandoff
    {
        public string FightId;
        /// <summary>Set by the Battle scene once the outcome is recorded; consumed by Main.</summary>
        public FightCompletion Completion;

        public FightCompletion TakeCompletion()
        {
            var c = Completion;
            Completion = null;
            return c;
        }
    }
}
