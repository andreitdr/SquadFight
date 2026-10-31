using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Save;

namespace Game.Services
{
    /// <summary>
    /// Makes a loaded save consistent with the current content (after a content update): adds new heroes, clamps stars,
    /// drops equip links to unknown heroes/slots, removes duplicated equips. Never deletes owned items or shards.
    /// </summary>
    public static class SaveReconciler
    {
        public static List<string> Reconcile(SaveData data, GameDatabase db)
        {
            var fixes = new List<string>();
            var known = new HashSet<string>(data.Heroes.Select(h => h.HeroId));
            foreach (var h in db.Heroes)
                if (!known.Contains(h.Id))
                {
                    var starting = db.Rules.StartingHeroIds.Contains(h.Id);
                    data.Heroes.Add(new HeroSave { HeroId = h.Id, Unlocked = starting, Stars = starting ? 1 : 0 });
                    fixes.Add($"added hero {h.Id}");
                }

            // Duplicate hero rows: merge shards into the first.
            foreach (var g in data.Heroes.GroupBy(h => h.HeroId).Where(g => g.Count() > 1).ToList())
            {
                var first = g.First();
                foreach (var dup in g.Skip(1))
                {
                    first.Shards += dup.Shards;
                    first.Unlocked |= dup.Unlocked;
                    first.Stars = System.Math.Max(first.Stars, dup.Stars);
                    first.ActiveSkillLevel = System.Math.Max(first.ActiveSkillLevel, dup.ActiveSkillLevel);
                    first.PassiveSkillLevel = System.Math.Max(first.PassiveSkillLevel, dup.PassiveSkillLevel);
                    data.Heroes.Remove(dup);
                }
                fixes.Add($"merged duplicate hero rows {g.Key}");
            }

            var maxStar = db.MaxStar;
            foreach (var h in data.Heroes)
            {
                if (h.Shards < 0) { h.Shards = 0; fixes.Add($"negative shards {h.HeroId}"); }
                if (h.Unlocked && h.Stars < 1) { h.Stars = 1; fixes.Add($"star fixed {h.HeroId}"); }
                if (h.Stars > maxStar) { h.Stars = maxStar; fixes.Add($"star clamped {h.HeroId}"); }
                if (!h.Unlocked && h.Stars != 0) h.Stars = 0;
                var maxSkill = db.MaxSkillLevel;
                if (h.ActiveSkillLevel < 1 || h.ActiveSkillLevel > maxSkill) { h.ActiveSkillLevel = System.Math.Max(1, System.Math.Min(maxSkill, h.ActiveSkillLevel)); fixes.Add($"active skill level clamped {h.HeroId}"); }
                if (h.PassiveSkillLevel < 1 || h.PassiveSkillLevel > maxSkill) { h.PassiveSkillLevel = System.Math.Max(1, System.Math.Min(maxSkill, h.PassiveSkillLevel)); fixes.Add($"passive skill level clamped {h.HeroId}"); }
            }

            foreach (var key in data.Currencies.Keys.ToList())
                if (data.Currencies[key] < 0) { data.Currencies[key] = 0; fixes.Add($"negative currency {key}"); }

            // Equipment: unknown owner or slot → unequipped; two items in one hero slot → keep the first.
            var layout = db.Rules.SlotLayout;
            var taken = new HashSet<(string, int)>();
            var ids = new HashSet<string>();
            foreach (var e in data.Equipment.ToList())
            {
                if (!ids.Add(e.InstanceId))
                {
                    data.Equipment.Remove(e);
                    fixes.Add($"removed duplicate item instance {e.InstanceId}");
                    continue;
                }
                if (string.IsNullOrEmpty(e.EquippedByHeroId)) { e.EquippedByHeroId = null; e.EquippedSlotIndex = -1; continue; }
                var heroOk = data.Heroes.Any(h => h.HeroId == e.EquippedByHeroId && h.Unlocked) && db.TryGetHero(e.EquippedByHeroId, out _);
                var templateOk = db.TryGetEquipment(e.TemplateId, out var t);
                var slotOk = templateOk && e.EquippedSlotIndex >= 0 && e.EquippedSlotIndex < layout.Count && layout[e.EquippedSlotIndex] == t.Slot;
                if (!heroOk || !slotOk || !taken.Add((e.EquippedByHeroId, e.EquippedSlotIndex)))
                {
                    fixes.Add($"unequipped {e.InstanceId}");
                    e.EquippedByHeroId = null;
                    e.EquippedSlotIndex = -1;
                }
            }

            // Player level: a content update may lower the cap.
            if (data.Player.Level > db.MaxPlayerLevel) { data.Player.Level = db.MaxPlayerLevel; data.Player.Xp = 0; fixes.Add("player level clamped"); }
            if (data.Player.Level < 1) { data.Player.Level = 1; fixes.Add("player level fixed"); }
            var need = PlayerLevelRules.XpToNext(data.Player.Level, db);
            if (data.Player.Xp < 0 || (need > 0 && data.Player.Xp >= need) || (need == 0 && data.Player.Xp != 0)) { data.Player.Xp = need > 0 ? System.Math.Max(0, System.Math.Min(need - 1, data.Player.Xp)) : 0; fixes.Add("player xp clamped"); }

            foreach (var f in data.Progress.Fights.Where(f => f.BestStars < 0 || f.BestStars > 3)) f.BestStars = System.Math.Max(0, System.Math.Min(3, f.BestStars));
            return fixes;
        }
    }
}
