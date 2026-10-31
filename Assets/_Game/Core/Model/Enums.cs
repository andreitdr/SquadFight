namespace Game.Core
{
    /// <summary>Shared by heroes and items (A4).</summary>
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

    /// <summary>Combat stats. Append new values at the end; StatBlock and config tables size themselves from this enum.</summary>
    public enum StatType { MaxHp, Attack, Defense, Speed, CritChance, CritDamage }

    /// <summary>Slot kinds; the actual layout (e.g. two Ring slots) comes from <c>GameRules.SlotLayout</c>.</summary>
    public enum EquipmentSlot { Weapon, Helmet, Chest, Gloves, Boots, Ring }

    public enum ModifierKind { Flat, Percent }

    public enum HeroClass { Tank, Warrior, Ranger, Mage, Support }

    /// <summary>Append only: assets store the numeric value.</summary>
    public enum TargetingRule { SingleEnemy, AllEnemies, SingleAlly, AllAllies, Self, LowestHpAlly, RandomEnemies, LowestHpEnemy, HighestAttackEnemy, RandomAllies, AllOtherAllies }

    /// <summary>Append only: assets store the numeric value. Field use per type is documented on <see cref="EffectDef"/>.</summary>
    public enum EffectType
    {
        Damage, Heal, ApplyStatus, Shield, Stun,
        DamageOverTime, HealOverTime, Drain, Execute, PercentHpDamage, Taunt, Silence, Cleanse, Dispel, Revive, Thorns, Dodge, CooldownShift, Mark,
    }

    /// <summary>What a live status does. <see cref="StatChange"/> is the classic stat buff/debuff.</summary>
    public enum StatusKind { StatChange, DamageOverTime, HealOverTime, Taunt, Silence, Thorns, Dodge, Mark }

    /// <summary>When a passive skill fires. <see cref="None"/> = a normal basic/active skill.</summary>
    public enum PassiveTrigger { None, BattleStart, TurnStart, AfterBasic, AfterActive, OnHitTaken, OnCrit, OnKill, OnAllyDeath, OnLowHp }

    /// <summary>Who an effect lands on: the skill's selected targets or the caster.</summary>
    public enum EffectTarget { SkillTargets, Self }

    public enum StackingRule { Refresh, Stack, Ignore }

    public enum AiTargeting { LowestHp, Random, First }

    public enum DropType { Currency, HeroShard, RandomHeroShardByRarity, Equipment, RandomEquipment }

    public enum UnlockConditionType { ClearFight, MinPartyPower, HeroOwned }

    /// <summary>
    /// Enemy rank, weakest first. A higher rank has more HP (rank multiplier) and more skills, and gives more player XP.
    /// <see cref="Boss"/> is the top rank: it also has special attacks. Append only: assets store the numeric value.
    /// </summary>
    public enum EnemyRank { Minion, Soldier, Elite, Champion, Boss }
}
