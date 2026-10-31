using System;
using System.Text;

namespace Game.Core
{
    /// <summary>Human-readable text log of a battle (debug tool, tests).</summary>
    public static class BattleLog
    {
        public static string Format(BattleSimulator sim, Func<string, string> displayName = null)
        {
            displayName ??= id => id;
            string N(int i)
            {
                var c = sim.Get(i);
                return c == null ? "?" : $"{displayName(c.DefId)}[{(c.Team == Team.Player ? "P" : "E")}{c.Slot}]";
            }
            var sb = new StringBuilder();
            foreach (var e in sim.Events)
            {
                switch (e.Type)
                {
                    case BattleEventType.BattleStarted: sb.AppendLine("=== Battle started ==="); break;
                    case BattleEventType.WaveStarted: sb.AppendLine($"--- Wave {e.Wave + 1} ---"); break;
                    case BattleEventType.RoundStarted: sb.AppendLine($"Round {e.Round}"); break;
                    case BattleEventType.ActionChosen: sb.AppendLine($"  {N(e.Actor)} uses {e.SkillId}"); break;
                    case BattleEventType.Stunned: sb.AppendLine($"  {N(e.Actor)} is stunned"); break;
                    case BattleEventType.DamageDealt:
                        sb.AppendLine($"    {N(e.Target)} takes {e.Amount}{(e.Crit ? " CRIT" : "")}{(e.Absorbed > 0 ? $" ({e.Absorbed} absorbed)" : "")} → {e.HpAfter} HP");
                        break;
                    case BattleEventType.HealApplied: sb.AppendLine($"    {N(e.Target)} heals {e.Amount} → {e.HpAfter} HP"); break;
                    case BattleEventType.ShieldApplied: sb.AppendLine($"    {N(e.Target)} gains a {e.Amount} shield ({e.Duration} turns)"); break;
                    case BattleEventType.StatusApplied:
                        sb.AppendLine(e.StatusId == "stun" ? $"    {N(e.Target)} is stunned for {e.Duration}"
                            : e.StatusId != null && e.StatusId.Contains("#") ? $"    {N(e.Target)} {e.Stat} {e.Value:+0%;-0%} for {e.Duration} (x{e.Stacks})"
                            : $"    {N(e.Target)} gains {e.StatusId} for {e.Duration} (x{e.Stacks})");
                        break;
                    case BattleEventType.StatusExpired:
                        sb.AppendLine(e.StatusId != null && e.StatusId.Contains("#") ? $"    {N(e.Target)} {e.Stat} effect wears off" : $"    {N(e.Target)} {e.StatusId} wears off");
                        break;
                    case BattleEventType.DotTick: sb.AppendLine($"    {N(e.Target)} suffers {e.Amount} {e.StatusId} → {e.HpAfter} HP"); break;
                    case BattleEventType.HotTick: sb.AppendLine($"    {N(e.Target)} regenerates {e.Amount} → {e.HpAfter} HP"); break;
                    case BattleEventType.Dodged: sb.AppendLine($"    {N(e.Target)} dodges"); break;
                    case BattleEventType.Revived: sb.AppendLine($"    {N(e.Target)} is revived with {e.Amount} HP"); break;
                    case BattleEventType.Cleansed: sb.AppendLine($"    {N(e.Target)} is cleansed ({e.Amount})"); break;
                    case BattleEventType.Dispelled: sb.AppendLine($"    {N(e.Target)} loses {e.Amount} buff(s)"); break;
                    case BattleEventType.Silenced: sb.AppendLine($"  {N(e.Actor)} is silenced"); break;
                    case BattleEventType.PassiveTriggered: sb.AppendLine($"  {N(e.Actor)} passive {e.SkillId}"); break;
                    case BattleEventType.CooldownChanged: sb.AppendLine($"    {N(e.Target)} cooldown → {e.Amount}"); break;
                    case BattleEventType.ThornsReflected: sb.AppendLine($"    {N(e.Target)} takes {e.Amount} thorns → {e.HpAfter} HP"); break;
                    case BattleEventType.ShieldExpired: sb.AppendLine($"    {N(e.Target)} shield fades"); break;
                    case BattleEventType.CombatantDied: sb.AppendLine($"    {N(e.Target)} is defeated"); break;
                    case BattleEventType.WaveCleared: sb.AppendLine($"--- Wave {e.Wave + 1} cleared ---"); break;
                    case BattleEventType.BattleEnded: sb.AppendLine($"=== {e.Outcome} after {e.Round} round(s) ==="); break;
                }
            }
            return sb.ToString();
        }
    }
}
