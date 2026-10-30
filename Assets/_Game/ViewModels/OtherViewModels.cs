using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;

namespace Game.ViewModels
{
    /// <summary>All owned items with filters, sort, sell.</summary>
    public sealed class InventoryViewModel : ViewModelBase
    {
        private readonly GameSession _s;
        private readonly NotificationsModel _dots;
        public EquipmentSlot? SlotFilter { get; private set; }
        public Rarity? RarityFilter { get; private set; }
        public ItemSort Sort { get; private set; } = ItemSort.RarityDesc;

        public InventoryViewModel(GameSession s, NotificationsModel dots)
        {
            _s = s;
            _dots = dots;
            On<ItemAdded>(s.Bus, _ => Raise());
            On<ItemSold>(s.Bus, _ => Raise());
            On<ItemEquipped>(s.Bus, _ => Raise());
            On<ItemUnequipped>(s.Bus, _ => Raise());
            On<StateReset>(s.Bus, _ => Raise());
        }

        public IReadOnlyList<ItemInfo> Items => _s.Inventory.Query(SlotFilter, null, Sort).Where(i => !RarityFilter.HasValue || i.Rarity == RarityFilter.Value).ToList();
        public int Count => _s.Inventory.Count;
        public int Limit => _s.Inventory.Limit;
        public bool SellEnabled => _s.Db.Rules.SellEnabled;
        public bool Badge(string id) => _dots != null && _dots.ItemBadge(id);
        public long SellValue(string id) => _s.Inventory.GetSellValue(id);
        public List<StatModifier> ItemStats(string id) => _s.Inventory.GetItemStats(id);

        public void SetSlotFilter(EquipmentSlot? slot) { SlotFilter = slot; Raise(nameof(Items)); }
        public void SetRarityFilter(Rarity? r) { RarityFilter = r; Raise(nameof(Items)); }
        public void SetSort(ItemSort sort) { Sort = sort; Raise(nameof(Items)); }

        public CommandResult Sell(string id) => CommandResult.From(_s.Inventory.SellItem(id));

        /// <summary>Unequipped items at or below the rarity that are not upgrades for anyone (bulk sell preview).</summary>
        public IReadOnlyList<ItemInfo> BulkSellCandidates(Rarity maxRarity) =>
            _s.Inventory.Query(includeEquipped: false).Where(i => i.Rarity <= maxRarity && !_s.Inventory.IsUpgradeForParty(i.InstanceId, _s.Roster.UnlockedHeroIds)).ToList();

        public int BulkSell(Rarity maxRarity)
        {
            var sold = 0;
            foreach (var i in BulkSellCandidates(maxRarity)) if (_s.Inventory.SellItem(i.InstanceId).Ok) sold++;
            return sold;
        }
    }

    /// <summary>The 5-slot party with owned heroes to choose from.</summary>
    public sealed class PartyViewModel : ViewModelBase
    {
        private readonly GameSession _s;
        public Rarity? RarityFilter { get; private set; }
        public HeroClass? ClassFilter { get; private set; }

        public PartyViewModel(GameSession s)
        {
            _s = s;
            On<PartyChanged>(s.Bus, _ => Raise());
            On<HeroUnlocked>(s.Bus, _ => Raise());
            On<StateReset>(s.Bus, _ => Raise());
        }

        public string[] Slots => _s.Party.GetSlots();
        public int Power => _s.PartyPower;
        public int Size => _s.Party.Size;

        public IReadOnlyList<HeroEntry> Available =>
            _s.Roster.UnlockedHeroIds.Select(id => HeroListViewModel.Entry(_s, null, _s.Db.GetHero(id)))
                .Where(e => (!RarityFilter.HasValue || e.Def.Rarity == RarityFilter) && (!ClassFilter.HasValue || e.Def.Class == ClassFilter))
                .OrderByDescending(e => e.Power).ToList();

        public void SetRarityFilter(Rarity? r) { RarityFilter = r; Raise(); }
        public void SetClassFilter(HeroClass? c) { ClassFilter = c; Raise(); }

        /// <summary>Tapping a hero: removes it if already in the party, else adds it to the selected slot (or the first free one).</summary>
        public CommandResult Toggle(string heroId, int? slot = null)
        {
            if (_s.Party.Contains(heroId)) return CommandResult.From(_s.Party.RemoveHero(heroId));
            return CommandResult.From(slot.HasValue ? _s.Party.Assign(slot.Value, heroId) : _s.Party.Add(heroId));
        }

        public CommandResult Assign(int slot, string heroId) => CommandResult.From(_s.Party.Assign(slot, heroId));
        public CommandResult Remove(int slot) => CommandResult.From(_s.Party.Remove(slot));
        public CommandResult Swap(int a, int b) => CommandResult.From(_s.Party.Swap(a, b));

        public void AutoFill()
        {
            for (var i = 0; i < Size; i++) _s.Party.Remove(i);
            _s.Party.AutoFill(id => _s.Roster.GetPower(id));
        }
    }

    public enum FightState { Locked, Unlocked, Cleared }

    public sealed class FightNode
    {
        public FightDef Def;
        public int Index;
        public FightState State;
        public int Stars;
        public bool IsBoss;
        public bool IsNext;
    }

    public sealed class ChapterView
    {
        public ChapterDef Def;
        public bool Unlocked;
        public int Cleared;
        public int Stars;
        public int MaxStars;
    }

    /// <summary>Chapters and their fights with lock/clear/star state.</summary>
    public sealed class CampaignViewModel : ViewModelBase
    {
        private readonly GameSession _s;
        public int SelectedChapter { get; private set; }

        public CampaignViewModel(GameSession s)
        {
            _s = s;
            On<FightCompleted>(s.Bus, _ => Raise());
            On<PartyChanged>(s.Bus, _ => Raise());
            On<StateReset>(s.Bus, _ => Raise());
            var next = s.Progress.GetNextRecommendedFight();
            if (next != null && s.Db.TryGetFight(next, out var f)) SelectedChapter = s.Db.Chapters.FindIndex(c => c.Id == f.ChapterId);
            if (SelectedChapter < 0) SelectedChapter = 0;
        }

        public bool StarsEnabled => _s.Db.Rules.StarsEnabled;
        public int PartyPower => _s.PartyPower;

        public IReadOnlyList<ChapterView> Chapters => _s.Db.Chapters.Select(c => new ChapterView
        {
            Def = c, Unlocked = _s.Progress.IsChapterUnlocked(c.Id), Cleared = c.FightIds.Count(_s.Progress.IsFightCleared),
            Stars = _s.Progress.ChapterStars(c.Id), MaxStars = c.FightIds.Count * 3,
        }).ToList();

        public void SelectChapter(int index)
        {
            if (index < 0 || index >= _s.Db.Chapters.Count) return;
            SelectedChapter = index;
            Raise();
        }

        public IReadOnlyList<FightNode> Fights
        {
            get
            {
                var chapter = _s.Db.Chapters[SelectedChapter];
                var next = _s.Progress.GetNextRecommendedFight();
                return chapter.FightIds.Select((id, i) =>
                {
                    var f = _s.Db.GetFight(id);
                    var p = _s.Progress.GetFightProgress(id);
                    return new FightNode
                    {
                        Def = f, Index = i, State = p.Cleared ? FightState.Cleared : p.Unlocked ? FightState.Unlocked : FightState.Locked, Stars = p.BestStars,
                        IsBoss = f.Waves.Any(w => w.Spawns.Any(sp => _s.Db.TryGetEnemy(sp.EnemyId, out var e) && e.IsBoss)), IsNext = id == next && !p.Cleared,
                    };
                }).ToList();
            }
        }

        public IReadOnlyList<StoryBeatDef> SeenStory => _s.Progress.GetSeenBeats();
    }

    public sealed class EnemyView
    {
        public EnemyDef Def;
        public int Wave;
        public float Multiplier;
        public int Power;
    }

    public sealed class DropPreview
    {
        public bool Gold;
        public readonly List<string> HeroShards = new List<string>();
        public readonly List<Rarity> ShardRarities = new List<Rarity>();
        public readonly List<Rarity> ItemRarities = new List<Rarity>();
        /// <summary>Only filled when config showDropOdds is on: label → chance per clear.</summary>
        public readonly List<(string label, double chance)> Odds = new List<(string, double)>();
    }

    /// <summary>Before a fight: enemies, possible rewards, party power vs recommended, start.</summary>
    public sealed class FightPreviewViewModel : ViewModelBase
    {
        private readonly GameSession _s;
        public FightDef Fight { get; }

        public FightPreviewViewModel(GameSession s, string fightId)
        {
            _s = s;
            Fight = s.Db.GetFight(fightId);
            On<PartyChanged>(s.Bus, _ => Raise());
            On<SettingsChanged>(s.Bus, _ => Raise());
        }

        public int PartyPower => _s.PartyPower;
        public int RecommendedPower => Fight.RecommendedPower;
        /// <summary>Recommended power is the median casual player's power, so being a little under is normal: warn only below this share.</summary>
        public const float UnderpoweredRatio = 0.9f;
        public bool IsUnderpowered => RecommendedPower > 0 && PartyPower < RecommendedPower * UnderpoweredRatio;
        /// <summary>Slightly under the recommendation: shown as a caution colour, without the warning.</summary>
        public bool IsClose => RecommendedPower > 0 && !IsUnderpowered && PartyPower < RecommendedPower;
        public string[] PartySlots => _s.Party.GetSlots();
        public bool AutoSkill => _s.Settings.AutoSkill;
        public bool FirstClearAvailable => !_s.Progress.GetFightProgress(Fight.Id).FirstClearClaimed && !string.IsNullOrEmpty(Fight.FirstClearTableId);
        public int BestStars => _s.Progress.GetFightProgress(Fight.Id).BestStars;
        public int StarTurnLimit => ProgressionRules.StarTurnLimit(Fight, _s.Db.Rules);

        public IReadOnlyList<EnemyView> Enemies =>
            Fight.Waves.SelectMany((w, wi) => w.Spawns.Select(sp =>
            {
                var e = _s.Db.GetEnemy(sp.EnemyId);
                return new EnemyView { Def = e, Wave = wi, Multiplier = sp.StatMultiplier, Power = PowerRating.Compute(BattleSetupBuilder.ScaleCombatStats(e.BaseStats, sp.StatMultiplier), _s.Db.Combat) };
            })).ToList();

        public DropPreview Drops
        {
            get
            {
                var p = new DropPreview();
                void Scan(string tableId)
                {
                    if (!_s.Db.TryGetDropTable(tableId, out var t)) return;
                    foreach (var e in t.Guaranteed.Concat(t.Groups.SelectMany(g => g.Entries)).Where(e => e != null))
                    {
                        switch (e.Type)
                        {
                            case DropType.Currency: p.Gold = true; break;
                            case DropType.HeroShard: if (!p.HeroShards.Contains(e.RefId)) p.HeroShards.Add(e.RefId); break;
                            case DropType.RandomHeroShardByRarity: if (!p.ShardRarities.Contains(e.Rarity)) p.ShardRarities.Add(e.Rarity); break;
                            case DropType.Equipment: if (!p.ItemRarities.Contains(e.Rarity)) p.ItemRarities.Add(e.Rarity); break;
                            case DropType.RandomEquipment:
                                foreach (var w in e.RarityWeights.Where(w => w.Weight > 0)) if (!p.ItemRarities.Contains(w.Rarity)) p.ItemRarities.Add(w.Rarity);
                                break;
                        }
                    }
                    if (_s.Db.Rules.ShowDropOdds)
                        foreach (var kv in DropRoller.ExpectedPicks(t)) p.Odds.Add(($"{kv.Key.Type} {kv.Key.RefId} {kv.Key.Rarity}".Trim(), kv.Value));
                }
                Scan(Fight.DropTableId);
                if (FirstClearAvailable) Scan(Fight.FirstClearTableId);
                p.ShardRarities.Sort();
                p.ItemRarities.Sort();
                return p;
            }
        }

        public void SetAutoSkill(bool on) => _s.Settings.AutoSkill = on;

        public CommandResult CanStart() => CommandResult.From(_s.Battles.CanStart(Fight.Id));
    }

    public sealed class RewardLine
    {
        public enum Kind { Currency, Shards, Item }
        public Kind Type;
        public string Id;
        public long Amount;
        public int ShardsBefore, ShardsAfter, ShardsNeeded;
        public bool NowUnlockable, StarUpAvailable, Better, Converted;
        public ItemInfo? Item;
        public Rarity Rarity;
    }

    /// <summary>Result screen data: outcome, stars, reward lines with highlights, navigation targets.</summary>
    public sealed class ResultViewModel : ViewModelBase
    {
        private readonly GameSession _s;
        public FightCompletion Completion { get; }

        public ResultViewModel(GameSession s, FightCompletion completion)
        {
            _s = s;
            Completion = completion;
        }

        public bool Victory => Completion.Victory;
        public int Stars => Completion.Stars;
        public bool NewBest => Completion.Stars > Completion.PreviousBestStars;
        public string ChapterCompleted => Completion.ChapterCompletedId;
        public string NextFightId => _s.Progress.GetNextRecommendedFight();
        public bool HasNextFight => Victory && NextFightId != null && NextFightId != Completion.FightId && !_s.Progress.IsFightCleared(NextFightId);

        public IReadOnlyList<RewardLine> Lines
        {
            get
            {
                var lines = new List<RewardLine>();
                var r = Completion.Rewards;
                if (r?.Bundle == null) return lines;
                foreach (var c in r.Bundle.Currencies.Where(c => c.Value > 0))
                    lines.Add(new RewardLine { Type = RewardLine.Kind.Currency, Id = c.Key, Amount = c.Value });
                foreach (var sh in r.Bundle.Shards.Where(x => x.Value > 0))
                {
                    var hero = _s.Db.GetHero(sh.Key);
                    r.ShardProgress.TryGetValue(sh.Key, out var prog);
                    var unlocked = _s.Roster.IsUnlocked(sh.Key);
                    var need = unlocked ? (_s.Roster.GetStarUpInfo(sh.Key).IsMaxStar ? 0 : _s.Roster.GetStarUpInfo(sh.Key).NeedShards) : _s.Roster.GetUnlockInfo(sh.Key).Need;
                    lines.Add(new RewardLine
                    {
                        Type = RewardLine.Kind.Shards, Id = sh.Key, Amount = sh.Value, Rarity = hero.Rarity, ShardsBefore = prog.before, ShardsAfter = prog.after, ShardsNeeded = need,
                        NowUnlockable = r.NowUnlockable.Contains(sh.Key), StarUpAvailable = r.StarUpAvailable.Contains(sh.Key), Converted = r.ConvertedShards.ContainsKey(sh.Key),
                    });
                }
                foreach (var it in r.Bundle.Items)
                {
                    var info = _s.Inventory.Get(it.InstanceId);
                    lines.Add(new RewardLine
                    {
                        Type = RewardLine.Kind.Item, Id = it.InstanceId, Amount = 1, Item = info, Rarity = it.Rarity,
                        Better = r.BetterItems.Contains(it.InstanceId), Converted = r.AutoSold.Contains(it.InstanceId),
                    });
                }
                return lines.OrderBy(l => l.Type).ThenByDescending(l => l.Rarity).ToList();
            }
        }

        /// <summary>Heroes that became unlockable with these rewards (celebration popup).</summary>
        public IReadOnlyList<string> NowUnlockable => Completion.Rewards?.NowUnlockable ?? new List<string>();
    }

    /// <summary>Settings screen (backed by SettingsService).</summary>
    public sealed class SettingsViewModel : ViewModelBase
    {
        private readonly GameSession _s;

        public SettingsViewModel(GameSession s)
        {
            _s = s;
            On<SettingsChanged>(s.Bus, _ => Raise());
        }

        public SettingsService Settings => _s.Settings;

        public void CycleBattleSpeed()
        {
            var speeds = SettingsService.BattleSpeeds;
            var i = System.Array.IndexOf(speeds, Settings.BattleSpeed);
            Settings.BattleSpeed = speeds[(i + 1 + speeds.Length) % speeds.Length];
        }

        public void ResetProgress() => _s.ResetToNewGame();
    }

    /// <summary>Home: continue target, chapter progress, red dots.</summary>
    public sealed class HomeViewModel : ViewModelBase
    {
        private readonly GameSession _s;
        public NotificationsModel Dots { get; }

        public HomeViewModel(GameSession s, NotificationsModel dots)
        {
            _s = s;
            Dots = dots;
            On<FightCompleted>(s.Bus, _ => Raise());
            On<PartyChanged>(s.Bus, _ => Raise());
            On<StateReset>(s.Bus, _ => Raise());
            dots.Changed += Forward;
        }

        private void Forward(string p) => Raise(p);

        public string NextFightId => _s.Progress.GetNextRecommendedFight();
        public FightDef NextFight => NextFightId != null && _s.Db.TryGetFight(NextFightId, out var f) ? f : null;
        public ChapterDef CurrentChapter => NextFight != null ? _s.Db.GetChapter(NextFight.ChapterId) : _s.Db.Chapters.FirstOrDefault();
        public int ChapterIndex => CurrentChapter == null ? 0 : _s.Db.Chapters.IndexOf(CurrentChapter);
        public int ChapterCleared => CurrentChapter?.FightIds.Count(_s.Progress.IsFightCleared) ?? 0;
        public int ChapterTotal => CurrentChapter?.FightIds.Count ?? 0;
        public bool CampaignComplete => _s.Db.Fights.All(f => _s.Progress.IsFightCleared(f.Id));
        public int PartyPower => _s.PartyPower;
        public int TotalStars => _s.Db.Chapters.Sum(c => _s.Progress.ChapterStars(c.Id));
        public int MaxStars => _s.Db.Fights.Count * 3;

        public override void Dispose()
        {
            Dots.Changed -= Forward;
            base.Dispose();
        }
    }
}
