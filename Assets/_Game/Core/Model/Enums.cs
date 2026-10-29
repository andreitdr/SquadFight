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

    public enum TargetingRule { SingleEnemy, AllEnemies, SingleAlly, AllAllies, Self, LowestHpAlly, RandomEnemies }

    public enum EffectType { Damage, Heal, ApplyStatus, Shield, Stun }

    /// <summary>Who an effect lands on: the skill's selected targets or the caster.</summary>
    public enum EffectTarget { SkillTargets, Self }

    public enum StackingRule { Refresh, Stack, Ignore }

    public enum AiTargeting { LowestHp, Random, First }

    public enum DropType { Currency, HeroShard, RandomHeroShardByRarity, Equipment, RandomEquipment }

    public enum UnlockConditionType { ClearFight, MinPartyPower, HeroOwned }
}
