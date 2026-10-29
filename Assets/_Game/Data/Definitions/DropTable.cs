using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Drop Table", fileName = "drops_new")]
    public sealed class DropTable : ContentDefinition
    {
        [Tooltip("Always given.")]
        public List<DropEntryData> guaranteed = new List<DropEntryData>();
        [Tooltip("Each group rolls min..max times, picking entries by weight.")]
        public List<RollGroupData> groups = new List<RollGroupData>();

        public DropTableDef ToCore() => new DropTableDef
        {
            Id = id,
            Guaranteed = guaranteed.Where(e => e != null).Select(e => e.ToCore()).ToList(),
            Groups = groups.Where(g => g != null).Select(g => new RollGroupDef
            {
                MinRolls = g.minRolls, MaxRolls = g.maxRolls, WithoutReplacement = g.withoutReplacement,
                Entries = g.entries.Where(e => e != null).Select(e => e.ToCore()).ToList(),
            }).ToList(),
        };
    }
}
