using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Config/Combat Config", fileName = "CombatConfig")]
    public sealed class CombatConfig : ScriptableObject
    {
        public float minDamage = 1f;
        [Tooltip("damage = atk × power × 100 / (100 + def × defenseK)")]
        public float defenseK = 1f;
        public int maxRounds = 100;
        [Range(0.01f, 1f)] public float critChanceCap = 1f;
        public AiTargeting aiTargeting = AiTargeting.LowestHp;
        [Tooltip("Power rating = Σ stat × weight.")]
        public StatTable powerWeights = new StatTable();

        public CombatRules ToCore() => new CombatRules
        {
            MinDamage = minDamage, DefenseK = defenseK, MaxRounds = maxRounds, CritChanceCap = critChanceCap,
            AiTargeting = aiTargeting, PowerWeights = powerWeights.ToStatBlock(),
        };
    }
}
