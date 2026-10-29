using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    /// <summary>Inspector-friendly stat table: one float per <see cref="StatType"/> (resizes itself if the enum grows).</summary>
    [Serializable]
    public sealed class StatTable
    {
        [SerializeField] private float[] values = new float[StatBlock.Count];

        public float this[StatType s]
        {
            get { Ensure(); return values[(int)s]; }
            set { Ensure(); values[(int)s] = value; }
        }

        public void Ensure()
        {
            if (values == null) values = new float[StatBlock.Count];
            else if (values.Length != StatBlock.Count) Array.Resize(ref values, StatBlock.Count);
        }

        public StatBlock ToStatBlock()
        {
            Ensure();
            var b = new StatBlock();
            for (var i = 0; i < StatBlock.Count; i++) b[(StatType)i] = values[i];
            return b;
        }

        public void Set(StatBlock block)
        {
            Ensure();
            for (var i = 0; i < StatBlock.Count; i++) values[i] = block[(StatType)i];
        }
    }

    [Serializable]
    public struct StatModifierData
    {
        public StatType stat;
        public float value;
        public ModifierKind kind;
        public StatModifier ToCore() => new StatModifier(stat, value, kind);
    }

    [Serializable]
    public sealed class EffectData
    {
        public EffectType type;
        public EffectTarget target;
        [Tooltip("Damage/Heal/Shield: multiplier on caster attack.")]
        public float power = 1f;
        [Tooltip("ApplyStatus: stat to modify.")]
        public StatType stat;
        [Tooltip("ApplyStatus: percent change, 0.2 = +20%.")]
        public float value;
        [Tooltip("Turns (status, shield, stun).")]
        public int duration;
        public StackingRule stacking;
        public int maxStacks = 1;
        [Tooltip("Optional stacking identity; defaults to skill id + effect index.")]
        public string statusId;
        [Range(0, 1), Tooltip("Chance to apply (0 = always).")]
        public float chance;

        public EffectDef ToCore() => new EffectDef
        {
            Type = type, Target = target, Power = power, Stat = stat, Value = value, Duration = duration,
            Stacking = stacking, MaxStacks = maxStacks, StatusId = statusId, Chance = chance,
        };
    }

    [Serializable]
    public struct RarityWeightData
    {
        public Rarity rarity;
        public int weight;
    }

    [Serializable]
    public sealed class DropEntryData
    {
        public DropType type;
        [Tooltip("Currency id, hero id or equipment template id (depends on type).")]
        public string refId;
        [Tooltip("RandomHeroShardByRarity: hero rarity. Equipment: item rarity.")]
        public Rarity rarity;
        [Tooltip("RandomEquipment: any slot when true.")]
        public bool anySlot = true;
        public EquipmentSlot slot;
        public List<RarityWeightData> rarityWeights = new List<RarityWeightData>();
        public int weight = 1;
        public int amountMin = 1;
        public int amountMax = 1;

        public DropEntryDef ToCore()
        {
            var d = new DropEntryDef
            {
                Type = type, RefId = refId, Rarity = rarity, SlotFilter = anySlot ? (EquipmentSlot?)null : slot,
                Weight = weight, AmountMin = amountMin, AmountMax = amountMax,
            };
            foreach (var w in rarityWeights) d.RarityWeights.Add(new RarityWeight(w.rarity, w.weight));
            return d;
        }
    }

    [Serializable]
    public sealed class RollGroupData
    {
        public int minRolls = 1;
        public int maxRolls = 1;
        public bool withoutReplacement;
        public List<DropEntryData> entries = new List<DropEntryData>();
    }

    [Serializable]
    public sealed class SpawnData
    {
        public EnemyDefinition enemy;
        [Tooltip("Scales the enemy's base stats in this fight.")]
        public float statMultiplier = 1f;
    }

    [Serializable]
    public sealed class WaveData
    {
        public List<SpawnData> spawns = new List<SpawnData>();
    }

    [Serializable]
    public sealed class UnlockConditionData
    {
        public UnlockConditionType type;
        public string refId;
        public int value;
        public UnlockConditionDef ToCore() => new UnlockConditionDef { Type = type, RefId = refId, Value = value };
    }

    [Serializable]
    public sealed class CurrencyData
    {
        public string id;
        public string nameKey;
        public Sprite icon;
    }

    [Serializable]
    public struct CurrencyAmountData
    {
        public string currencyId;
        public long amount;
    }
}
