using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;

namespace Game.ViewModels
{
    public enum HeroSort { Rarity, Power, Name }

    public sealed class HeroEntry
    {
        public HeroDef Def;
        public bool Unlocked;
        public int Star;
        public int MaxStar;
        public int Shards;
        /// <summary>Shards needed for the next step (unlock or star-up); 0 at max star.</summary>
        public int ShardsNeeded;
        public int Power;
        public bool Badge;
        public bool InParty;
    }

    /// <summary>Heroes collection: owned + locked silhouettes, sort and filters.</summary>
    public sealed class HeroListViewModel : ViewModelBase
    {
        private readonly GameSession _s;
        private readonly NotificationsModel _dots;
        public HeroSort Sort { get; private set; } = HeroSort.Rarity;
        public Rarity? RarityFilter { get; private set; }
        public HeroClass? ClassFilter { get; private set; }
        public bool OwnedOnly { get; private set; }

        public HeroListViewModel(GameSession s, NotificationsModel dots)
        {
            _s = s;
            _dots = dots;
            On<ShardsChanged>(s.Bus, _ => Raise());
            On<HeroUnlocked>(s.Bus, _ => Raise());
            On<HeroStarUpgraded>(s.Bus, _ => Raise());
            On<ItemEquipped>(s.Bus, _ => Raise());
            On<ItemUnequipped>(s.Bus, _ => Raise());
            On<PartyChanged>(s.Bus, _ => Raise());
            On<StateReset>(s.Bus, _ => Raise());
        }

        public static HeroEntry Entry(GameSession s, NotificationsModel dots, HeroDef h)
        {
            var unlocked = s.Roster.IsUnlocked(h.Id);
            int need;
            if (!unlocked) need = s.Roster.GetUnlockInfo(h.Id).Need;
            else
            {
                var star = s.Roster.GetStarUpInfo(h.Id);
                need = star.IsMaxStar ? 0 : star.NeedShards;
            }
            return new HeroEntry
            {
                Def = h, Unlocked = unlocked, Star = s.Roster.GetStar(h.Id), MaxStar = s.Db.MaxStar, Shards = s.Roster.GetShards(h.Id),
                ShardsNeeded = need, Power = s.Roster.GetPower(h.Id), Badge = dots != null && dots.HeroBadge(h.Id), InParty = s.Party.Contains(h.Id),
            };
        }

        public IReadOnlyList<HeroEntry> Items
        {
            get
            {
                IEnumerable<HeroEntry> q = _s.Db.Heroes.Select(h => Entry(_s, _dots, h));
                if (RarityFilter.HasValue) q = q.Where(e => e.Def.Rarity == RarityFilter.Value);
                if (ClassFilter.HasValue) q = q.Where(e => e.Def.Class == ClassFilter.Value);
                if (OwnedOnly) q = q.Where(e => e.Unlocked);
                // Owned first, then the chosen order; unlockable locked heroes float to the top of the locked group.
                switch (Sort)
                {
                    case HeroSort.Power: q = q.OrderByDescending(e => e.Unlocked).ThenByDescending(e => e.Badge).ThenByDescending(e => e.Power); break;
                    case HeroSort.Name: q = q.OrderByDescending(e => e.Unlocked).ThenBy(e => e.Def.Id, StringComparer.Ordinal); break;
                    default: q = q.OrderByDescending(e => e.Unlocked).ThenByDescending(e => e.Badge).ThenByDescending(e => e.Def.Rarity).ThenByDescending(e => e.Power); break;
                }
                return q.ToList();
            }
        }

        public void SetSort(HeroSort sort) { Sort = sort; Raise(nameof(Items)); }
        public void SetRarityFilter(Rarity? r) { RarityFilter = r; Raise(nameof(Items)); }
        public void SetClassFilter(HeroClass? c) { ClassFilter = c; Raise(nameof(Items)); }
        public void SetOwnedOnly(bool v) { OwnedOnly = v; Raise(nameof(Items)); }
    }

    public sealed class SlotView
    {
        public int Index;
        public EquipmentSlot Slot;
        public ItemInfo? Item;
        public bool Badge;
    }

    /// <summary>One hero: stats breakdown, skills, stars, unlock/star-up, equipment slots.</summary>
    public sealed class HeroDetailViewModel : ViewModelBase
    {
        private readonly GameSession _s;
        public string HeroId { get; }
        public HeroDef Def { get; }

        public HeroDetailViewModel(GameSession s, string heroId)
        {
            _s = s;
            HeroId = heroId;
            Def = s.Db.GetHero(heroId);
            On<ShardsChanged>(s.Bus, e => { if (e.HeroId == heroId) Raise(); });
            On<HeroUnlocked>(s.Bus, e => { if (e.HeroId == heroId) Raise(); });
            On<HeroStarUpgraded>(s.Bus, e => { if (e.HeroId == heroId) Raise(); });
            On<CurrencyChanged>(s.Bus, _ => Raise());
            On<ItemEquipped>(s.Bus, _ => Raise());
            On<ItemUnequipped>(s.Bus, _ => Raise());
            On<StateReset>(s.Bus, _ => Raise());
        }

        public bool Unlocked => _s.Roster.IsUnlocked(HeroId);
        public int Star => _s.Roster.GetStar(HeroId);
        public int MaxStar => _s.Db.MaxStar;
        public int Power => _s.Roster.GetPower(HeroId);
        public UnlockInfo UnlockInfo => _s.Roster.GetUnlockInfo(HeroId);
        public StarUpInfo StarUpInfo => _s.Roster.GetStarUpInfo(HeroId);
        public StatBreakdown Stats => _s.Roster.GetStatBreakdown(HeroId);
        public SkillDef BasicSkill => _s.Db.TryGetSkill(Def.BasicSkillId, out var k) ? k : null;
        public SkillDef ActiveSkill => _s.Db.TryGetSkill(Def.ActiveSkillId, out var k) ? k : null;
        public SkillDef PassiveSkill => _s.Db.TryGetSkill(Def.PassiveSkillId, out var k) ? k : null;
        public string PrimaryCurrency => _s.Db.Rules.PrimaryCurrencyId;
        public bool InParty => _s.Party.Contains(HeroId);

        public IReadOnlyList<SlotView> Slots
        {
            get
            {
                var equipped = _s.Inventory.GetEquippedBySlot(HeroId);
                var layout = _s.Db.Rules.SlotLayout;
                var bag = _s.Inventory.Query(includeEquipped: false);
                return layout.Select((slot, i) => new SlotView
                {
                    Index = i, Slot = slot, Item = equipped[i],
                    Badge = Unlocked && bag.Any(it => it.Slot == slot && _s.Inventory.PowerDelta(HeroId, it.InstanceId, i) > 0),
                }).ToList();
            }
        }

        public CommandResult Unlock() => CommandResult.From(_s.Roster.UnlockHero(HeroId));
        public CommandResult StarUp() => CommandResult.From(_s.Roster.UpgradeStar(HeroId));
        public CommandResult Unequip(int slotIndex) => CommandResult.From(_s.Inventory.Unequip(HeroId, slotIndex));

        public CommandResult AddToParty()
        {
            if (InParty) return CommandResult.Success;
            return CommandResult.From(_s.Party.Add(HeroId));
        }

        public CommandResult AutoEquip()
        {
            var layout = _s.Db.Rules.SlotLayout;
            for (var i = 0; i < layout.Count; i++)
            {
                var best = _s.Inventory.Query(layout[i], includeEquipped: false)
                    .Select(it => (it.InstanceId, d: _s.Inventory.PowerDelta(HeroId, it.InstanceId, i))).Where(x => x.d > 0).OrderByDescending(x => x.d).FirstOrDefault();
                if (best.InstanceId != null) _s.Inventory.Equip(HeroId, best.InstanceId, i);
            }
            return CommandResult.Success;
        }
    }

    public sealed class PickerEntry
    {
        public ItemInfo Item;
        public StatBlock Delta;
        public int PowerDelta;
        public string EquippedBy;
    }

    /// <summary>Choose an item for one hero slot: candidates with stat deltas vs the current item.</summary>
    public sealed class EquipmentPickerViewModel : ViewModelBase
    {
        private readonly GameSession _s;
        public string HeroId { get; }
        public int SlotIndex { get; }
        public EquipmentSlot Slot { get; }

        public EquipmentPickerViewModel(GameSession s, string heroId, int slotIndex)
        {
            _s = s;
            HeroId = heroId;
            SlotIndex = slotIndex;
            Slot = s.Db.Rules.SlotLayout[slotIndex];
            On<ItemEquipped>(s.Bus, _ => Raise());
            On<ItemUnequipped>(s.Bus, _ => Raise());
            On<ItemSold>(s.Bus, _ => Raise());
        }

        public ItemInfo? Current => _s.Inventory.GetEquipped(HeroId, SlotIndex);

        public IReadOnlyList<PickerEntry> Candidates
        {
            get
            {
                var current = _s.Roster.GetFinalStats(HeroId);
                var currentPower = _s.Roster.GetPower(HeroId);
                var currentId = Current?.InstanceId;
                return _s.Inventory.Query(Slot)
                    .Where(i => i.InstanceId != currentId)
                    .Select(i =>
                    {
                        var after = _s.Inventory.PreviewEquip(HeroId, i.InstanceId, SlotIndex);
                        var delta = after.Clone().AddScaled(current, -1);
                        return new PickerEntry { Item = i, Delta = delta, PowerDelta = PowerRating.Compute(after, _s.Db.Combat) - currentPower, EquippedBy = i.EquippedBy };
                    })
                    .OrderByDescending(e => e.PowerDelta).ThenByDescending(e => e.Item.Rarity).ToList();
            }
        }

        public List<StatModifier> ItemStats(string instanceId) => _s.Inventory.GetItemStats(instanceId);
        public CommandResult Equip(string instanceId) => CommandResult.From(_s.Inventory.Equip(HeroId, instanceId, SlotIndex));
        public CommandResult Unequip() => CommandResult.From(_s.Inventory.Unequip(HeroId, SlotIndex));
    }
}
