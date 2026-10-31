using Game.Core;
using Game.Save;

namespace Game.Services
{
    /// <summary>Composition of all player-state services over one <see cref="SaveData"/>. Built in Boot (or by tests/simulators).</summary>
    public sealed class GameSession
    {
        public GameDatabase Db { get; }
        public PlayerState State { get; }
        public EventBus Bus { get; }
        public ServiceContext Context { get; }
        public IRandom Random { get; }
        public IClock Clock { get; }

        public CurrencyService Currency { get; }
        public RosterService Roster { get; }
        public PartyService Party { get; }
        public InventoryService Inventory { get; }
        public ProgressService Progress { get; }
        public RewardService Rewards { get; }
        public PlayerLevelService Levels { get; }
        public SettingsService Settings { get; }
        public TutorialService Tutorial { get; }
        public DailyBonusService Daily { get; }
        public BattleFlowController Battles { get; }

        public GameSession(GameDatabase db, SaveData data, ISaveScheduler saver = null, IRandom random = null, IClock clock = null, IGameLogger log = null)
        {
            Db = db;
            Clock = clock ?? new SystemClock();
            Random = random ?? SeededRandom.FromTime();
            if (data.RngState != null && data.RngState.Length == 2) Random.SetState(data.RngState);
            State = new PlayerState(data);
            Bus = new EventBus(log);
            Context = new ServiceContext(db, State, Bus, saver, Random, Clock, log);
            Currency = new CurrencyService(Context);
            Roster = new RosterService(Context);
            Roster.Currency = Currency;
            Inventory = new InventoryService(Context) { Roster = Roster, Currency = Currency };
            Roster.Inventory = Inventory;
            Party = new PartyService(Context, Roster);
            Progress = new ProgressService(Context, Roster, Party);
            Rewards = new RewardService(Context, Currency, Roster, Inventory, Party);
            Progress.Rewards = Rewards;
            Levels = new PlayerLevelService(Context, Rewards);
            Progress.Levels = Levels;
            Battles = new BattleFlowController(this);
            Settings = new SettingsService(Context);
            Tutorial = new TutorialService(Context);
            Daily = new DailyBonusService(Context, Rewards);
        }

        public int PartyPower => Roster.GetTotalPower(Party.Members);

        /// <summary>The live data to persist (RNG state captured so drop sequences continue after restart).</summary>
        public SaveData ExportForSave()
        {
            State.Data.RngState = Random.GetState();
            return State.Data;
        }

        /// <summary>Wipes progress and starts over (Settings → Reset progress, debug "reset").</summary>
        public void ResetToNewGame(bool keepSettings = true)
        {
            var settings = State.Data.Settings;
            var fresh = NewGameFactory.Create(Db, Clock);
            if (keepSettings) fresh.Settings = settings;
            State.Replace(fresh);
            Party.Normalize();
            Context.Commit();
            Context.Saver.SaveNow();
            Context.Emit(new StateReset());
        }
    }
}
