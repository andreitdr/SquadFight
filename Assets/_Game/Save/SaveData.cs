using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Save
{
    /// <summary>
    /// The whole persistent player state. Plain data; rules live in Game.Services.
    /// Bump <see cref="SaveVersions.Current"/> and add an <see cref="ISaveMigration"/> whenever the JSON shape changes.
    /// </summary>
    public sealed class SaveData
    {
        public int Version = SaveVersions.Current;
        public DateTime CreatedUtc;
        public DateTime LastSavedUtc;
        public Dictionary<string, long> Currencies = new Dictionary<string, long>();
        public List<HeroSave> Heroes = new List<HeroSave>();
        /// <summary>Party slots (length = party size); null or empty = free slot.</summary>
        public List<string> Party = new List<string>();
        public List<EquipmentSave> Equipment = new List<EquipmentSave>();
        public ProgressSave Progress = new ProgressSave();
        public SettingsSave Settings = new SettingsSave();
        public PitySave Pity = new PitySave();
        public TutorialSave Tutorial = new TutorialSave();
        public DailyBonusSave Daily = new DailyBonusSave();
        public PlayerLevelSave Player = new PlayerLevelSave();
        /// <summary>Optional RNG state for reproducible drop sequences.</summary>
        public ulong[] RngState;
    }

    public sealed class HeroSave
    {
        public string HeroId;
        public bool Unlocked;
        public int Stars;
        /// <summary>Shards held (tracked for locked heroes too).</summary>
        public int Shards;
        /// <summary>Gold-bought skill levels (1 = as authored).</summary>
        public int ActiveSkillLevel = 1;
        public int PassiveSkillLevel = 1;
    }

    public sealed class EquipmentSave
    {
        /// <summary>GUID string, unique per instance.</summary>
        public string InstanceId;
        public string TemplateId;
        public Rarity Rarity;
        /// <summary>Final rolled stats when roll variation is enabled; null = use template × rarity.</summary>
        public List<StatModifier> RolledStats;
        public string EquippedByHeroId;
        /// <summary>Index into the slot layout when equipped; -1 otherwise.</summary>
        public int EquippedSlotIndex = -1;
        /// <summary>Monotonic acquisition order (for "newest" sorting).</summary>
        public long AcquiredOrder;
    }

    public sealed class ProgressSave
    {
        public List<FightProgressSave> Fights = new List<FightProgressSave>();
        public List<string> ClaimedChapterRewards = new List<string>();
        public List<string> SeenStoryBeats = new List<string>();
        public long TotalFights;
    }

    public sealed class FightProgressSave
    {
        public string FightId;
        public bool Cleared;
        public int BestStars;
        public int Clears;
        public bool FirstClearClaimed;
    }

    /// <summary>Bad-luck protection counters (step 10).</summary>
    public sealed class PitySave
    {
        public int FightsWithoutShard;
    }

    /// <summary>Onboarding progress (step 18): resumes after a restart.</summary>
    public sealed class TutorialSave
    {
        /// <summary>Index of the next scripted step; equal to the step count when finished.</summary>
        public int Step;
        public bool Skipped;
        public List<string> SeenTips = new List<string>();
    }

    /// <summary>Optional daily login bonus (config flag).</summary>
    public sealed class DailyBonusSave
    {
        /// <summary>Local calendar date of the last claim (yyyy-MM-dd), empty if never.</summary>
        public string LastClaimDate = "";
        public int Streak;
    }

    /// <summary>Player level (account XP from defeated enemies; bosses give the most).</summary>
    public sealed class PlayerLevelSave
    {
        public int Level = 1;
        /// <summary>XP into the current level.</summary>
        public int Xp;
        /// <summary>All XP ever earned (statistics; also kept past the level cap).</summary>
        public long TotalXp;
    }

    public sealed class SettingsSave
    {
        /// <summary>New games start muted; the player turns audio up in Settings.</summary>
        public float MusicVolume = 0f;
        public float SfxVolume = 0f;
        /// <summary>Empty = detect from the system language on first run.</summary>
        public string Language = "";
        public bool Vibration = true;
        public bool ScreenShake = true;
        public bool ReduceMotion;
        public float BattleSpeed = 1f;
        public bool AutoSkill = true;
    }

    public static class SaveVersions
    {
        /// <summary>v1: initial format (hero field "star"). v2: renamed to "stars". v3: added "pity". v4: "tutorial" + "daily". v5: audio muted. v6: hero skill levels. v7: "player" level.</summary>
        public const int Current = 7;
    }
}
