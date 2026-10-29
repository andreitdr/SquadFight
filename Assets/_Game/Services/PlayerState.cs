using System.Collections.Generic;
using Game.Save;

namespace Game.Services
{
    /// <summary>The single authoritative in-memory player state (the loaded <see cref="SaveData"/>) plus lookup indexes. No rules.</summary>
    public sealed class PlayerState
    {
        private readonly Dictionary<string, HeroSave> _heroes = new Dictionary<string, HeroSave>();
        private readonly Dictionary<string, EquipmentSave> _items = new Dictionary<string, EquipmentSave>();
        private readonly Dictionary<string, FightProgressSave> _fights = new Dictionary<string, FightProgressSave>();

        public SaveData Data { get; private set; }

        public PlayerState(SaveData data) => Replace(data);

        public void Replace(SaveData data)
        {
            Data = data;
            Reindex();
        }

        public void Reindex()
        {
            _heroes.Clear();
            foreach (var h in Data.Heroes) if (!_heroes.ContainsKey(h.HeroId)) _heroes[h.HeroId] = h;
            _items.Clear();
            foreach (var e in Data.Equipment) if (!_items.ContainsKey(e.InstanceId)) _items[e.InstanceId] = e;
            _fights.Clear();
            foreach (var f in Data.Progress.Fights) if (!_fights.ContainsKey(f.FightId)) _fights[f.FightId] = f;
        }

        public HeroSave GetHero(string heroId) => heroId != null && _heroes.TryGetValue(heroId, out var h) ? h : null;

        public HeroSave GetOrCreateHero(string heroId)
        {
            var h = GetHero(heroId);
            if (h != null) return h;
            h = new HeroSave { HeroId = heroId };
            Data.Heroes.Add(h);
            _heroes[heroId] = h;
            return h;
        }

        public EquipmentSave GetItem(string instanceId) => instanceId != null && _items.TryGetValue(instanceId, out var e) ? e : null;

        public void AddItem(EquipmentSave item)
        {
            Data.Equipment.Add(item);
            _items[item.InstanceId] = item;
        }

        public bool RemoveItem(string instanceId)
        {
            var item = GetItem(instanceId);
            if (item == null) return false;
            Data.Equipment.Remove(item);
            _items.Remove(instanceId);
            return true;
        }

        public FightProgressSave GetFight(string fightId) => fightId != null && _fights.TryGetValue(fightId, out var f) ? f : null;

        public FightProgressSave GetOrCreateFight(string fightId)
        {
            var f = GetFight(fightId);
            if (f != null) return f;
            f = new FightProgressSave { FightId = fightId };
            Data.Progress.Fights.Add(f);
            _fights[fightId] = f;
            return f;
        }

        public long NextAcquiredOrder()
        {
            long max = 0;
            foreach (var e in Data.Equipment) if (e.AcquiredOrder > max) max = e.AcquiredOrder;
            return max + 1;
        }
    }
}
