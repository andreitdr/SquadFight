using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Services
{
    /// <summary>The fixed-size party. Invariants: size = rules.PartySize, no duplicates, only unlocked heroes.</summary>
    public sealed class PartyService
    {
        private readonly ServiceContext _ctx;
        private readonly RosterService _roster;

        public PartyService(ServiceContext ctx, RosterService roster)
        {
            _ctx = ctx;
            _roster = roster;
            Normalize();
        }

        public int Size => _ctx.Db.Rules.PartySize;

        private List<string> Slots => _ctx.State.Data.Party;

        /// <summary>Slot contents (null = empty), copied.</summary>
        public string[] GetSlots()
        {
            Normalize();
            return Slots.Select(s => string.IsNullOrEmpty(s) ? null : s).ToArray();
        }

        /// <summary>Non-empty slots in slot order.</summary>
        public IReadOnlyList<string> Members => GetSlots().Where(s => s != null).ToList();

        public bool Contains(string heroId) => heroId != null && Slots.Contains(heroId);

        public int IndexOf(string heroId) => heroId == null ? -1 : Slots.IndexOf(heroId);

        /// <summary>Puts a hero in a slot. If the hero is already in another slot the two slots swap.</summary>
        public Result Assign(int slot, string heroId)
        {
            Normalize();
            if (slot < 0 || slot >= Size) return Result.Fail(ErrorCode.InvalidSlot, $"slot {slot}");
            if (heroId == null) return Remove(slot);
            if (!_roster.Exists(heroId)) return Result.Fail(ErrorCode.NotFound, heroId);
            if (!_roster.IsUnlocked(heroId)) return Result.Fail(ErrorCode.HeroLocked, heroId);
            var current = Slots.IndexOf(heroId);
            if (current == slot) return Result.Success;
            if (current >= 0) Slots[current] = Slots[slot];
            Slots[slot] = heroId;
            Changed();
            return Result.Success;
        }

        /// <summary>Adds to the first free slot.</summary>
        public Result Add(string heroId)
        {
            Normalize();
            if (Contains(heroId)) return Result.Fail(ErrorCode.DuplicateInParty, heroId);
            var free = Slots.FindIndex(string.IsNullOrEmpty);
            if (free < 0) return Result.Fail(ErrorCode.PartyFull);
            return Assign(free, heroId);
        }

        public Result Remove(int slot)
        {
            Normalize();
            if (slot < 0 || slot >= Size) return Result.Fail(ErrorCode.InvalidSlot, $"slot {slot}");
            if (Slots[slot] == null) return Result.Success;
            Slots[slot] = null;
            Changed();
            return Result.Success;
        }

        public Result RemoveHero(string heroId)
        {
            var i = IndexOf(heroId);
            return i < 0 ? Result.Fail(ErrorCode.NotFound, heroId) : Remove(i);
        }

        public Result Swap(int a, int b)
        {
            Normalize();
            if (a < 0 || a >= Size || b < 0 || b >= Size) return Result.Fail(ErrorCode.InvalidSlot);
            if (a == b) return Result.Success;
            var t = Slots[a];
            Slots[a] = Slots[b];
            Slots[b] = t;
            Changed();
            return Result.Success;
        }

        /// <summary>Fills empty slots with the strongest unlocked heroes not yet in the party.</summary>
        public void AutoFill(Func<string, float> power)
        {
            Normalize();
            var candidates = _roster.UnlockedHeroIds.Where(id => !Slots.Contains(id)).OrderByDescending(power).ThenBy(id => id, StringComparer.Ordinal).ToList();
            var changed = false;
            for (var i = 0; i < Size && candidates.Count > 0; i++)
            {
                if (Slots[i] != null) continue;
                Slots[i] = candidates[0];
                candidates.RemoveAt(0);
                changed = true;
            }
            if (changed) Changed();
        }

        /// <summary>Repairs the stored party: fixes length, drops unknown/locked/duplicate heroes.</summary>
        public bool Normalize()
        {
            var changed = false;
            var party = Slots;
            while (party.Count < Size) { party.Add(null); changed = true; }
            if (party.Count > Size)
            {
                // Keep the heroes from overflowing slots if there is room.
                var extra = party.Skip(Size).Where(h => !string.IsNullOrEmpty(h)).ToList();
                party.RemoveRange(Size, party.Count - Size);
                foreach (var h in extra)
                {
                    var free = party.FindIndex(string.IsNullOrEmpty);
                    if (free >= 0 && !party.Contains(h)) party[free] = h;
                }
                changed = true;
            }
            var seen = new HashSet<string>();
            for (var i = 0; i < party.Count; i++)
            {
                var h = party[i];
                if (h == "") { party[i] = null; changed = true; continue; }
                if (h == null) continue;
                if (!_roster.IsUnlocked(h) || !seen.Add(h))
                {
                    party[i] = null;
                    changed = true;
                }
            }
            return changed;
        }

        private void Changed()
        {
            _ctx.Commit();
            _ctx.Emit(new PartyChanged());
        }
    }
}
