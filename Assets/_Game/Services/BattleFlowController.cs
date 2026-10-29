using Game.Core;

namespace Game.Services
{
    /// <summary>A battle in progress: the simulator plus what is needed to finish it.</summary>
    public sealed class ActiveBattle
    {
        public FightDef Fight;
        public BattleSetup Setup;
        public BattleSimulator Simulator;
        public ulong Seed;
        public bool Finished;
        public bool Retreated;
    }

    /// <summary>
    /// Orchestrates start fight → simulate/play → result → rewards → progress. The presentation layer drives
    /// <see cref="ActiveBattle.Simulator"/> step by step (or not at all) and then calls <see cref="Finish"/>.
    /// </summary>
    public sealed class BattleFlowController
    {
        private readonly GameSession _session;

        public ActiveBattle Current { get; private set; }

        public BattleFlowController(GameSession session) => _session = session;

        public Result CanStart(string fightId)
        {
            if (Current != null && !Current.Finished) return Result.Fail(ErrorCode.InvalidArgument, "a battle is already in progress");
            if (!_session.Db.TryGetFight(fightId, out _)) return Result.Fail(ErrorCode.NotFound, fightId);
            if (!_session.Progress.IsFightUnlocked(fightId)) return Result.Fail(ErrorCode.FightLocked, fightId);
            if (_session.Party.Members.Count == 0) return Result.Fail(ErrorCode.InvalidArgument, "the party is empty");
            return Result.Success;
        }

        /// <summary>Builds the battle. <paramref name="seed"/> null = drawn from the game RNG (replays pass a fixed seed).</summary>
        public Result<ActiveBattle> Start(string fightId, ulong? seed = null)
        {
            var check = CanStart(fightId);
            if (!check.Ok) return Result<ActiveBattle>.Fail(check.Code, check.Detail);
            var fight = _session.Db.GetFight(fightId);
            var s = seed ?? NextSeed(_session.Random);
            var setup = BattleSetupFactory.Create(_session, fight);
            var sim = new BattleSimulator(setup, new SeededRandom(s)) { AutoSkills = _session.State.Data.Settings.AutoSkill };
            Current = new ActiveBattle { Fight = fight, Setup = setup, Simulator = sim, Seed = s };
            if (seed == null) _session.Context.Commit(); // the game RNG advanced
            return Result<ActiveBattle>.Success(Current);
        }

        /// <summary>
        /// Finishes the current battle (runs any remaining turns instantly = "skip") and records the outcome.
        /// The battle stays current until it is recorded, so a failure can be retried instead of losing a victory.
        /// </summary>
        public Result<FightCompletion> Finish()
        {
            var battle = Current;
            if (battle == null || battle.Finished) return Result<FightCompletion>.Fail(ErrorCode.InvalidArgument, "no battle in progress");
            BattleResult result;
            try { result = battle.Simulator.RunToEnd(); }
            catch (System.Exception e)
            {
                _session.Context.Log.Error($"[Battle] simulation of {battle.Fight.Id} failed: {e}");
                Discard();
                return Result<FightCompletion>.Fail(ErrorCode.Unknown, e.Message);
            }
            return Record(battle, result);
        }

        /// <summary>Leaves the battle: counts as a defeat (no victory rewards, consolation if configured). A battle that already ended is finished normally.</summary>
        public Result<FightCompletion> Retreat()
        {
            var battle = Current;
            if (battle == null || battle.Finished) return Result<FightCompletion>.Fail(ErrorCode.InvalidArgument, "no battle in progress");
            if (battle.Simulator.IsOver) return Finish();
            battle.Retreated = true;
            var result = battle.Simulator.BuildResult();
            result.Outcome = BattleOutcome.Defeat;
            return Record(battle, result);
        }

        /// <summary>Drops the current battle without recording anything (e.g. it could not be presented).</summary>
        public void Discard()
        {
            if (Current != null) Current.Finished = true;
            Current = null;
        }

        private Result<FightCompletion> Record(ActiveBattle battle, BattleResult result)
        {
            var completion = _session.Progress.CompleteFight(battle.Fight.Id, result, unlockVerifiedAtStart: true);
            if (completion.Ok)
            {
                battle.Finished = true;
                if (Current == battle) Current = null;
            }
            return completion;
        }

        /// <summary>Headless: start and finish in one call (debug console, simulators, tests).</summary>
        public Result<FightCompletion> RunInstant(string fightId, ulong? seed = null)
        {
            var start = Start(fightId, seed);
            return start.Ok ? Finish() : Result<FightCompletion>.Fail(start.Code, start.Detail);
        }

        public static ulong NextSeed(IRandom rng) => ((ulong)(uint)rng.NextInt(int.MinValue, int.MaxValue) << 32) | (uint)rng.NextInt(int.MinValue, int.MaxValue);
    }
}
