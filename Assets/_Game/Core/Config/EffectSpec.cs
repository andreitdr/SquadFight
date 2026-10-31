using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Parses the effect mini-syntax used by skills.csv and passives.csv (one parser for the importer and the CSV loader).
    /// Effects are joined by '|'; the prefix "self&gt;" lands an effect on the caster.
    /// <code>
    /// Damage:power · Heal:power · Shield:power:turns · Stun:turns[:chance]
    /// Status:Stat:value:turns[:Refresh|Stack|Ignore[:maxStacks[:chance]]]
    /// Dot:burn|poison|bleed:power:turns[:stacking[:maxStacks[:chance]]] (default Stack, 3) · Hot:power:turns
    /// Drain:power:ratio · Execute:power:threshold:multiplier · PctHp:ratio[:cap×attack, default 4]
    /// Taunt:turns · Silence:turns[:chance] · Cleanse[:count] · Dispel[:count] (0 = all) · Revive:hpRatio
    /// Thorns:ratio:turns · Dodge:chance:turns · Cooldown:delta · Mark:value:turns
    /// </code>
    /// </summary>
    public static class EffectSpec
    {
        public static readonly string[] DotKinds = { "burn", "poison", "bleed" };

        public static List<EffectDef> Parse(string spec)
        {
            var list = new List<EffectDef>();
            foreach (var raw in (spec ?? "").Split('|').Select(s => s.Trim()).Where(s => s.Length > 0)) list.Add(ParseOne(raw));
            if (list.Count == 0) throw new FormatException("no effects");
            return list;
        }

        private static EffectDef ParseOne(string raw)
        {
            var spec = raw;
            var e = new EffectDef { Target = EffectTarget.SkillTargets, MaxStacks = 1 };
            if (spec.StartsWith("self>", StringComparison.OrdinalIgnoreCase)) { e.Target = EffectTarget.Self; spec = spec.Substring(5); }
            var p = spec.Split(':').Select(s => s.Trim()).ToArray();

            float F(int i, float fallback = 0)
            {
                if (i >= p.Length || p[i].Length == 0) return fallback;
                if (!float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) throw new FormatException($"effect '{raw}': '{p[i]}' is not a number");
                return v;
            }
            int I(int i, int fallback = 0) => (int)F(i, fallback);
            void Need(int count, string usage) { if (p.Length < count) throw new FormatException($"effect '{raw}': expected {usage}"); }
            void Tail(int at, StackingRule stacking, int maxStacks)
            {
                e.Stacking = at < p.Length && p[at].Length > 0 ? ParseEnum<StackingRule>(p[at], raw) : stacking;
                e.MaxStacks = I(at + 1, maxStacks);
                e.Chance = F(at + 2);
            }

            switch (p[0].ToLowerInvariant())
            {
                case "damage": e.Type = EffectType.Damage; e.Power = F(1, 1); break;
                case "heal": e.Type = EffectType.Heal; e.Power = F(1, 1); break;
                case "shield": e.Type = EffectType.Shield; e.Power = F(1, 1); e.Duration = I(2, 2); break;
                case "stun": e.Type = EffectType.Stun; e.Duration = I(1, 1); e.Chance = F(2); break;
                case "status":
                    Need(4, "Status:Stat:value:duration");
                    e.Type = EffectType.ApplyStatus; e.Stat = ParseEnum<StatType>(p[1], raw); e.Value = F(2); e.Duration = I(3, 1);
                    Tail(4, StackingRule.Refresh, 1);
                    break;
                case "dot":
                    Need(4, "Dot:kind:power:duration");
                    e.Type = EffectType.DamageOverTime; e.StatusId = p[1].ToLowerInvariant(); e.Power = F(2); e.Duration = I(3, 1);
                    if (Array.IndexOf(DotKinds, e.StatusId) < 0) throw new FormatException($"effect '{raw}': DoT kind must be one of {string.Join("/", DotKinds)}");
                    Tail(4, StackingRule.Stack, 3);
                    break;
                case "hot": Need(3, "Hot:power:duration"); e.Type = EffectType.HealOverTime; e.StatusId = "regen"; e.Power = F(1); e.Duration = I(2, 1); break;
                case "drain": Need(3, "Drain:power:ratio"); e.Type = EffectType.Drain; e.Power = F(1, 1); e.Value = F(2); break;
                case "execute": Need(4, "Execute:power:threshold:multiplier"); e.Type = EffectType.Execute; e.Power = F(1, 1); e.Value = F(2); e.Value2 = F(3); break;
                case "pcthp": Need(2, "PctHp:ratio[:cap]"); e.Type = EffectType.PercentHpDamage; e.Value = F(1); e.Value2 = F(2, 4); break;
                case "taunt": e.Type = EffectType.Taunt; e.StatusId = "taunt"; e.Duration = I(1, 1); break;
                case "silence": e.Type = EffectType.Silence; e.StatusId = "silence"; e.Duration = I(1, 1); e.Chance = F(2); break;
                case "cleanse": e.Type = EffectType.Cleanse; e.Value = F(1); break;
                case "dispel": e.Type = EffectType.Dispel; e.Value = F(1); break;
                case "revive": Need(2, "Revive:hpRatio"); e.Type = EffectType.Revive; e.Value = F(1); break;
                case "thorns": Need(3, "Thorns:ratio:duration"); e.Type = EffectType.Thorns; e.StatusId = "thorns"; e.Value = F(1); e.Duration = I(2, 1); break;
                case "dodge": Need(3, "Dodge:chance:duration"); e.Type = EffectType.Dodge; e.StatusId = "dodge"; e.Value = F(1); e.Duration = I(2, 1); break;
                case "cooldown": Need(2, "Cooldown:delta"); e.Type = EffectType.CooldownShift; e.Value = F(1); break;
                case "mark": Need(3, "Mark:value:duration"); e.Type = EffectType.Mark; e.StatusId = "mark"; e.Value = F(1); e.Duration = I(2, 1); break;
                default: throw new FormatException($"unknown effect type '{p[0]}' in '{raw}'");
            }
            return e;
        }

        private static T ParseEnum<T>(string v, string raw) where T : struct
        {
            if (Enum.TryParse<T>(v.Trim(), true, out var r) && Enum.IsDefined(typeof(T), r)) return r;
            throw new FormatException($"effect '{raw}': '{v}' is not a valid {typeof(T).Name}");
        }

        /// <summary>Canonical text of one effect (used to compare skills for uniqueness).</summary>
        public static string Signature(EffectDef e) => string.Join(":", e.Target, e.Type, e.StatusId, e.Stat, R(e.Power), R(e.Value), R(e.Value2), e.Duration, e.Stacking, e.MaxStacks, R(e.Chance));

        /// <summary>Canonical text of a whole skill's behaviour (ids and names excluded).</summary>
        public static string Signature(SkillDef s) =>
            string.Join("|", s.Trigger, s.Targeting, s.TargetCount, s.Cooldown, s.InitialCooldown, R(s.Threshold), R(s.Chance), s.OncePerBattle) + "|" +
            string.Join("|", s.Effects.Where(e => e != null).Select(Signature));

        private static string R(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Status kind created by an effect type (null when the effect is instant).</summary>
        public static StatusKind? StatusKindOf(EffectType t)
        {
            switch (t)
            {
                case EffectType.ApplyStatus: return StatusKind.StatChange;
                case EffectType.DamageOverTime: return StatusKind.DamageOverTime;
                case EffectType.HealOverTime: return StatusKind.HealOverTime;
                case EffectType.Taunt: return StatusKind.Taunt;
                case EffectType.Silence: return StatusKind.Silence;
                case EffectType.Thorns: return StatusKind.Thorns;
                case EffectType.Dodge: return StatusKind.Dodge;
                case EffectType.Mark: return StatusKind.Mark;
                default: return null;
            }
        }
    }
}
