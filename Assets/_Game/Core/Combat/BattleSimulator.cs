using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Deterministic turn-based auto battle. Each <see cref="Step"/> performs one combatant's turn (plus any round/wave
    /// transitions) and appends <see cref="BattleEvent"/>s. Same setup + seed + queued inputs ⇒ identical events.
    /// Rounds: every living unit acts once, ordered by speed (desc), then slot, then id. Timeout after MaxRounds = defeat.
    /// A turn: TurnStart passive → DoT/HoT ticks → action (unless stunned) → AfterBasic/AfterActive passive → statuses tick down.
    /// Passives never trigger other passives (no chains).
    /// </summary>
    public sealed class BattleSimulator
    {
        private readonly BattleSetup _setup;
        private readonly IRandom _rng;
        private readonly CombatRules _rules;
        private readonly List<Combatant> _all = new List<Combatant>();
        private readonly List<Combatant> _players = new List<Combatant>();
        private readonly List<Combatant> _enemies = new List<Combatant>();
        private readonly Queue<Combatant> _order = new Queue<Combatant>();
        private readonly HashSet<int> _queuedSkills = new HashSet<int>();
        private readonly List<BattleEvent> _events = new List<BattleEvent>();
        private bool _started;
        private int _actions;

        public event Action<BattleEvent> EventEmitted;

        /// <summary>Player heroes use their active skill whenever ready. When false, only queued skills are used.</summary>
        public bool AutoSkills { get; set; } = true;

        public BattleOutcome Outcome { get; private set; }
        public bool IsOver => Outcome != BattleOutcome.None;
        public int Round { get; private set; }
        public int WaveIndex { get; private set; }
        public int WaveCount => _setup.EnemyWaves.Count;
        public IReadOnlyList<BattleEvent> Events => _events;
        public IReadOnlyList<Combatant> Players => _players;
        /// <summary>Enemies of the current wave.</summary>
        public IReadOnlyList<Combatant> Enemies => _enemies;
        /// <summary>Every combatant created so far (index = <see cref="Combatant.Index"/>).</summary>
        public IReadOnlyList<Combatant> All => _all;

        public BattleSimulator(BattleSetup setup, IRandom rng, CombatRules rules = null)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _rules = rules ?? setup.Rules ?? throw new ArgumentException("Combat rules missing.");
            if (setup.Players.Count == 0) throw new ArgumentException("Battle needs at least one player unit.");
            if (setup.EnemyWaves.Count == 0 || setup.EnemyWaves.Any(w => w.Count == 0)) throw new ArgumentException("Every wave needs at least one enemy.");
            foreach (var p in setup.Players) _players.Add(Add(p, 0));
        }

        private Combatant Add(CombatantSetup s, int wave)
        {
            var c = new Combatant(_all.Count, s, wave, _rules);
            _all.Add(c);
            return c;
        }

        public Combatant Get(int index) => index >= 0 && index < _all.Count ? _all[index] : null;

        /// <summary>Asks the hero to use its active skill on its next turn (if ready by then; otherwise it stays queued).</summary>
        public bool QueuePlayerSkill(int combatantIndex)
        {
            var c = Get(combatantIndex);
            if (c == null || c.Team != Team.Player || !c.Alive || c.Active == null || IsOver) return false;
            _queuedSkills.Add(combatantIndex);
            return true;
        }

        public bool IsSkillQueued(int combatantIndex) => _queuedSkills.Contains(combatantIndex);

        /// <summary>Units yet to act this round, in order (for the turn-order bar).</summary>
        public IReadOnlyList<Combatant> UpcomingThisRound => _order.Where(c => c.Alive).ToList();

        public BattleResult RunToEnd()
        {
            while (Step()) { }
            return BuildResult();
        }

        /// <summary>Runs one combatant turn. Returns false once the battle is over.</summary>
        public bool Step()
        {
            if (IsOver) return false;
            if (!_started)
            {
                _started = true;
                Emit(new BattleEvent { Type = BattleEventType.BattleStarted });
                StartWave(0);
                CheckEnd();
                if (IsOver || !StartRound()) return false;
            }

            Combatant actor = null;
            while (actor == null)
            {
                if (_order.Count == 0)
                {
                    if (!StartRound()) return false;
                    continue;
                }
                var next = _order.Dequeue();
                if (next.Alive && (next.Team == Team.Player || next.Wave == WaveIndex)) actor = next;
            }

            TakeTurn(actor);
            _actions++;
            CheckEnd();
            return !IsOver;
        }

        private void StartWave(int index)
        {
            WaveIndex = index;
            _enemies.Clear();
            foreach (var s in _setup.EnemyWaves[index]) _enemies.Add(Add(s, index));
            Emit(new BattleEvent { Type = BattleEventType.WaveStarted, Wave = index });
            // Heroes enter on the first wave (so their openers see enemies), every enemy when its wave spawns.
            if (index == 0) foreach (var p in _players) Trigger(p, PassiveTrigger.BattleStart);
            foreach (var e in _enemies.ToList()) Trigger(e, PassiveTrigger.BattleStart);
        }

        private bool StartRound()
        {
            if (Round >= _rules.MaxRounds)
            {
                Finish(BattleOutcome.Timeout);
                return false;
            }
            Round++;
            _order.Clear();
            var living = _players.Concat(_enemies).Where(c => c.Alive).ToList();
            living.Sort(CompareTurnOrder);
            foreach (var c in living) _order.Enqueue(c);
            Emit(new BattleEvent { Type = BattleEventType.RoundStarted });
            return true;
        }

        private int CompareTurnOrder(Combatant a, Combatant b)
        {
            var bySpeed = b.Stat(StatType.Speed).CompareTo(a.Stat(StatType.Speed));
            if (bySpeed != 0) return bySpeed;
            var bySlot = a.Slot.CompareTo(b.Slot);
            if (bySlot != 0) return bySlot;
            var byId = string.CompareOrdinal(a.DefId, b.DefId);
            if (byId != 0) return byId;
            return a.Index.CompareTo(b.Index);
        }

        private void TakeTurn(Combatant actor)
        {
            Emit(new BattleEvent { Type = BattleEventType.TurnStarted, Actor = actor.Index });
            Trigger(actor, PassiveTrigger.TurnStart);
            TickOverTime(actor);
            if (!actor.Alive) return;
            if (actor.StunTurns > 0)
            {
                actor.StunTurns--;
                Emit(new BattleEvent { Type = BattleEventType.Stunned, Actor = actor.Index, Duration = actor.StunTurns });
            }
            else
            {
                var skill = ChooseSkill(actor);
                var targets = SelectTargets(actor, skill);
                if (skill == actor.Active)
                {
                    actor.ActiveCooldown = skill.Cooldown;
                    _queuedSkills.Remove(actor.Index);
                }
                Emit(new BattleEvent { Type = BattleEventType.ActionChosen, Actor = actor.Index, SkillId = skill.Id, Targets = targets.Select(t => t.Index).ToArray() });
                Execute(actor, skill, targets);
                Trigger(actor, skill == actor.Active ? PassiveTrigger.AfterActive : PassiveTrigger.AfterBasic);
            }
            EndTurn(actor);
        }

        /// <summary>Damage and healing over time land at the start of the holder's turn (DoTs first; they ignore shields and dodge).</summary>
        private void TickOverTime(Combatant c)
        {
            foreach (var kind in new[] { StatusKind.DamageOverTime, StatusKind.HealOverTime })
            {
                foreach (var group in c.Statuses.Where(x => x.Kind == kind).GroupBy(x => x.StatusId).ToList())
                {
                    if (!c.Alive) return;
                    var amount = group.Sum(x => x.TickAmount);
                    var source = Get(group.First().Source);
                    if (kind == StatusKind.DamageOverTime)
                    {
                        var loss = Math.Min(c.Hp, Math.Max(1, amount));
                        c.Hp -= loss;
                        c.Totals.DamageTaken += loss;
                        if (source != null) source.Totals.DamageDealt += loss;
                        Emit(new BattleEvent { Type = BattleEventType.DotTick, Actor = source?.Index ?? -1, Target = c.Index, Amount = loss, HpAfter = c.Hp, StatusId = group.Key });
                        if (c.Hp <= 0) Die(c, source);
                        else Trigger(c, PassiveTrigger.OnLowHp);
                    }
                    else
                    {
                        var heal = Math.Min(amount, c.MaxHp - c.Hp);
                        c.Hp += heal;
                        if (source != null) source.Totals.HealingDone += heal;
                        Emit(new BattleEvent { Type = BattleEventType.HotTick, Actor = source?.Index ?? -1, Target = c.Index, Amount = heal, HpAfter = c.Hp, StatusId = group.Key });
                    }
                }
            }
        }

        private SkillDef ChooseSkill(Combatant actor)
        {
            if (!actor.ActiveReady) return actor.Basic;
            var wants = actor.Team == Team.Enemy || AutoSkills || _queuedSkills.Contains(actor.Index);
            if (!wants) return actor.Basic;
            if (actor.Has(StatusKind.Silence))
            {
                Emit(new BattleEvent { Type = BattleEventType.Silenced, Actor = actor.Index, SkillId = actor.Active.Id });
                return actor.Basic;
            }
            return actor.Active;
        }

        private List<Combatant> Opponents(Combatant c) => (c.Team == Team.Player ? _enemies : _players).Where(x => x.Alive).ToList();
        private List<Combatant> Allies(Combatant c) => (c.Team == Team.Player ? _players : _enemies).Where(x => x.Alive).ToList();
        private List<Combatant> Side(Combatant c) => c.Team == Team.Player ? _players : _enemies;

        private List<Combatant> SelectTargets(Combatant actor, SkillDef skill)
        {
            switch (skill.Targeting)
            {
                case TargetingRule.SingleEnemy:
                case TargetingRule.LowestHpEnemy:
                case TargetingRule.HighestAttackEnemy:
                {
                    var foes = Opponents(actor);
                    if (foes.Count == 0) return foes;
                    var taunting = foes.Where(f => f.Has(StatusKind.Taunt)).OrderBy(f => f.Slot).ThenBy(f => f.Index).FirstOrDefault();
                    if (taunting != null) return new List<Combatant> { taunting };
                    if (skill.Targeting == TargetingRule.LowestHpEnemy) return new List<Combatant> { foes.OrderBy(f => f.HpRatio).ThenBy(f => f.Slot).ThenBy(f => f.Index).First() };
                    if (skill.Targeting == TargetingRule.HighestAttackEnemy) return new List<Combatant> { foes.OrderByDescending(f => f.Stat(StatType.Attack)).ThenBy(f => f.Slot).ThenBy(f => f.Index).First() };
                    return new List<Combatant> { PickSingle(foes) };
                }
                case TargetingRule.AllEnemies:
                    return Opponents(actor);
                case TargetingRule.RandomEnemies:
                {
                    var foes = Opponents(actor);
                    var picks = new List<Combatant>();
                    if (foes.Count == 0) return picks;
                    for (var i = 0; i < Math.Max(1, skill.TargetCount); i++) picks.Add(foes[_rng.NextInt(0, foes.Count)]);
                    return picks;
                }
                case TargetingRule.SingleAlly:
                {
                    var allies = Allies(actor);
                    return new List<Combatant> { allies[_rng.NextInt(0, allies.Count)] };
                }
                case TargetingRule.AllAllies:
                    return Allies(actor);
                case TargetingRule.AllOtherAllies:
                    return Allies(actor).Where(a => a != actor).ToList();
                case TargetingRule.RandomAllies:
                {
                    // Distinct allies, up to targetCount.
                    var pool = Allies(actor);
                    var picks = new List<Combatant>();
                    for (var i = 0; i < Math.Max(1, skill.TargetCount) && pool.Count > 0; i++)
                    {
                        var k = _rng.NextInt(0, pool.Count);
                        picks.Add(pool[k]);
                        pool.RemoveAt(k);
                    }
                    return picks;
                }
                case TargetingRule.LowestHpAlly:
                {
                    var allies = Allies(actor);
                    return new List<Combatant> { allies.OrderBy(a => a.HpRatio).ThenBy(a => a.Slot).ThenBy(a => a.Index).First() };
                }
                default:
                    return new List<Combatant> { actor };
            }
        }

        private Combatant PickSingle(List<Combatant> foes)
        {
            switch (_rules.AiTargeting)
            {
                case AiTargeting.Random: return foes[_rng.NextInt(0, foes.Count)];
                case AiTargeting.First: return foes.OrderBy(f => f.Slot).ThenBy(f => f.Index).First();
                default: return foes.OrderBy(f => f.Hp).ThenBy(f => f.Slot).ThenBy(f => f.Index).First();
            }
        }

        private static bool IsDirectDamage(EffectType t) => t == EffectType.Damage || t == EffectType.Drain || t == EffectType.Execute || t == EffectType.PercentHpDamage;

        private void Execute(Combatant actor, SkillDef skill, List<Combatant> targets)
        {
            for (var i = 0; i < skill.Effects.Count; i++)
            {
                var effect = skill.Effects[i];
                if (effect.Type == EffectType.Revive)
                {
                    // Revives one fallen ally of the caster's side, whatever the skill targets.
                    if (actor.Alive) Revive(actor, skill, effect);
                    continue;
                }
                if (effect.Target == EffectTarget.Self)
                {
                    if (actor.Alive) Apply(actor, actor, skill, effect, i);
                    continue;
                }
                foreach (var t in targets)
                {
                    var target = t;
                    if (!target.Alive)
                    {
                        // A multi-hit skill whose target already died moves on to another living opponent.
                        if (!IsDirectDamage(effect.Type) || skill.Targeting != TargetingRule.RandomEnemies) continue;
                        var foes = Opponents(actor);
                        if (foes.Count == 0) break;
                        target = foes[_rng.NextInt(0, foes.Count)];
                    }
                    if (!actor.Alive && IsDirectDamage(effect.Type)) break; // killed by thorns mid-skill
                    Apply(actor, target, skill, effect, i);
                }
            }
        }

        private bool RollChance(float chance)
        {
            if (chance <= 0 || chance >= 1) return true;
            return _rng.NextDouble() < chance;
        }

        private void Apply(Combatant src, Combatant dst, SkillDef skill, EffectDef e, int effectIndex)
        {
            switch (e.Type)
            {
                case EffectType.Damage:
                    Strike(src, dst, skill, e.Power);
                    break;
                case EffectType.Execute:
                    Strike(src, dst, skill, dst.HpRatio <= e.Value ? e.Power * Math.Max(1f, e.Value2) : e.Power);
                    break;
                case EffectType.Drain:
                {
                    var dealt = Strike(src, dst, skill, e.Power);
                    if (dealt > 0 && src.Alive) HealUnit(src, src, skill, DamageCalculator.RoundToInt(dealt * e.Value));
                    break;
                }
                case EffectType.PercentHpDamage:
                {
                    if (TryDodge(src, dst, skill)) break;
                    var raw = Math.Min(e.Value * dst.MaxHp, Math.Max(0.1f, e.Value2) * src.Stat(StatType.Attack));
                    Land(src, dst, skill, Math.Max(1, DamageCalculator.RoundToInt(raw)), false);
                    break;
                }
                case EffectType.Heal:
                    HealUnit(src, dst, skill, DamageCalculator.Heal(src.Stat(StatType.Attack), e.Power));
                    break;
                case EffectType.Shield:
                {
                    var amount = DamageCalculator.Heal(src.Stat(StatType.Attack), e.Power);
                    dst.Shield += amount;
                    dst.ShieldTurns = Math.Max(dst.ShieldTurns, e.Duration);
                    dst.ShieldSkipNextTick = IsCurrentActor(dst);
                    Emit(new BattleEvent { Type = BattleEventType.ShieldApplied, Actor = src.Index, Target = dst.Index, Amount = amount, Duration = e.Duration, SkillId = skill.Id });
                    break;
                }
                case EffectType.Stun:
                {
                    if (!RollChance(e.Chance)) break;
                    dst.StunTurns = Math.Max(dst.StunTurns, e.Duration);
                    Emit(new BattleEvent { Type = BattleEventType.StatusApplied, Actor = src.Index, Target = dst.Index, StatusId = "stun", Duration = e.Duration, Stacks = 1, SkillId = skill.Id });
                    break;
                }
                case EffectType.ApplyStatus:
                case EffectType.DamageOverTime:
                case EffectType.HealOverTime:
                case EffectType.Taunt:
                case EffectType.Silence:
                case EffectType.Thorns:
                case EffectType.Dodge:
                case EffectType.Mark:
                {
                    if (!RollChance(e.Chance)) break;
                    var kind = EffectSpec.StatusKindOf(e.Type).Value;
                    var id = !string.IsNullOrEmpty(e.StatusId) ? e.StatusId : kind == StatusKind.StatChange ? skill.Id + "#" + effectIndex : kind.ToString().ToLowerInvariant();
                    var tick = kind == StatusKind.DamageOverTime || kind == StatusKind.HealOverTime ? Math.Max(1, DamageCalculator.Heal(src.Stat(StatType.Attack), e.Power)) : 0;
                    AddStatus(src, dst, skill, e, id, kind, tick);
                    break;
                }
                case EffectType.Cleanse:
                    RemoveStatuses(src, dst, skill, harmful: true, max: (int)e.Value);
                    break;
                case EffectType.Dispel:
                    RemoveStatuses(src, dst, skill, harmful: false, max: (int)e.Value);
                    break;
                case EffectType.CooldownShift:
                {
                    if (dst.Active == null) break;
                    dst.ActiveCooldown = Math.Max(0, dst.ActiveCooldown + (int)Math.Round(e.Value));
                    Emit(new BattleEvent { Type = BattleEventType.CooldownChanged, Actor = src.Index, Target = dst.Index, Amount = dst.ActiveCooldown, Value = e.Value, SkillId = skill.Id });
                    break;
                }
            }
        }

        private void AddStatus(Combatant src, Combatant dst, SkillDef skill, EffectDef e, string id, StatusKind kind, int tick)
        {
            var existing = dst.Statuses.Where(s => s.StatusId == id).ToList();
            var skip = IsCurrentActor(dst);
            if (existing.Count > 0 && e.Stacking == StackingRule.Ignore) return;
            if (existing.Count > 0 && (e.Stacking == StackingRule.Refresh || existing.Count >= Math.Max(1, e.MaxStacks)))
            {
                foreach (var s in existing) { s.Remaining = e.Duration; s.SkipNextTick = skip; }
                if (e.Stacking == StackingRule.Refresh)
                {
                    // Refresh keeps the strongest value/tick so a weaker re-application never downgrades it.
                    var top = existing[0];
                    if (Math.Abs(e.Value) > Math.Abs(top.Value)) top.Value = e.Value;
                    if (tick > top.TickAmount) { top.TickAmount = tick; top.Source = src.Index; }
                }
            }
            else
            {
                var value = kind == StatusKind.StatChange || kind == StatusKind.Thorns || kind == StatusKind.Dodge || kind == StatusKind.Mark ? e.Value : 0f;
                dst.Statuses.Add(new StatusEffect { StatusId = id, Kind = kind, Stat = e.Stat, Value = value, TickAmount = tick, Source = src.Index, Remaining = e.Duration, SkipNextTick = skip });
                foreach (var s in existing) { s.Remaining = e.Duration; s.SkipNextTick = skip; }
            }
            Emit(new BattleEvent { Type = BattleEventType.StatusApplied, Actor = src.Index, Target = dst.Index, StatusId = id, Stat = e.Stat, Value = kind == StatusKind.StatChange ? e.Value : tick != 0 ? tick : e.Value, Duration = e.Duration, Stacks = dst.Statuses.Count(s => s.StatusId == id), SkillId = skill.Id });
        }

        /// <summary>Cleanse (harmful = true) removes debuffs and stun; Dispel removes buffs and shields. Newest first; max 0 = all.</summary>
        private void RemoveStatuses(Combatant src, Combatant dst, SkillDef skill, bool harmful, int max)
        {
            var removed = 0;
            for (var i = dst.Statuses.Count - 1; i >= 0 && (max <= 0 || removed < max); i--)
            {
                var s = dst.Statuses[i];
                if (Combatant.IsHarmful(s) != harmful) continue;
                dst.Statuses.RemoveAt(i);
                removed++;
                Emit(new BattleEvent { Type = BattleEventType.StatusExpired, Target = dst.Index, StatusId = s.StatusId, Stat = s.Stat, Value = s.Value });
            }
            if (harmful && dst.StunTurns > 0)
            {
                dst.StunTurns = 0;
                removed++;
                Emit(new BattleEvent { Type = BattleEventType.StatusExpired, Target = dst.Index, StatusId = "stun" });
            }
            if (!harmful && dst.Shield > 0)
            {
                dst.Shield = 0;
                dst.ShieldTurns = 0;
                removed++;
                Emit(new BattleEvent { Type = BattleEventType.ShieldExpired, Target = dst.Index });
            }
            if (removed > 0)
                Emit(new BattleEvent { Type = harmful ? BattleEventType.Cleansed : BattleEventType.Dispelled, Actor = src.Index, Target = dst.Index, Amount = removed, SkillId = skill.Id });
        }

        private void Revive(Combatant src, SkillDef skill, EffectDef e)
        {
            var fallen = Side(src).Where(c => !c.Alive && !c.WasRevived).OrderBy(c => c.Slot).ThenBy(c => c.Index).FirstOrDefault();
            if (fallen == null) return;
            fallen.WasRevived = true;
            fallen.Hp = Math.Max(1, Math.Min(fallen.MaxHp, DamageCalculator.RoundToInt(fallen.MaxHp * e.Value)));
            fallen.ActiveCooldown = fallen.Active?.Cooldown ?? 0;
            Emit(new BattleEvent { Type = BattleEventType.Revived, Actor = src.Index, Target = fallen.Index, Amount = fallen.Hp, HpAfter = fallen.Hp, SkillId = skill.Id });
        }

        private void HealUnit(Combatant src, Combatant dst, SkillDef skill, int amount)
        {
            if (!dst.Alive) return;
            var heal = Math.Min(Math.Max(0, amount), dst.MaxHp - dst.Hp);
            dst.Hp += heal;
            src.Totals.HealingDone += heal;
            Emit(new BattleEvent { Type = BattleEventType.HealApplied, Actor = src.Index, Target = dst.Index, Amount = heal, HpAfter = dst.Hp, SkillId = skill.Id });
        }

        private bool TryDodge(Combatant src, Combatant dst, SkillDef skill)
        {
            if (!dst.Has(StatusKind.Dodge)) return false; // no roll at all without Dodge: older battles replay unchanged
            var chance = Math.Min(0.9f, dst.Total(StatusKind.Dodge));
            if (_rng.NextDouble() >= chance) return false;
            Emit(new BattleEvent { Type = BattleEventType.Dodged, Actor = src.Index, Target = dst.Index, SkillId = skill.Id });
            return true;
        }

        /// <summary>Direct hit: dodge → damage formula (crit) → <see cref="Land"/>. Returns the damage dealt (0 on dodge).</summary>
        private int Strike(Combatant src, Combatant dst, SkillDef skill, float power)
        {
            if (TryDodge(src, dst, skill)) return 0;
            var dmg = DamageCalculator.Damage(src.Stat(StatType.Attack), power, dst.Stat(StatType.Defense),
                src.Stat(StatType.CritChance), src.Stat(StatType.CritDamage), _rules, _rng, out var crit);
            return Land(src, dst, skill, dmg, crit);
        }

        /// <summary>Applies Mark, shields and HP loss, then thorns and the hit/crit/kill/low-HP passives.</summary>
        private int Land(Combatant src, Combatant dst, SkillDef skill, int dmg, bool crit)
        {
            var mark = dst.Total(StatusKind.Mark);
            if (mark != 0) dmg = Math.Max(1, DamageCalculator.RoundToInt(dmg * (1f + mark)));
            var absorbed = Math.Min(dst.Shield, dmg);
            dst.Shield -= absorbed;
            var hpLoss = Math.Min(dst.Hp, dmg - absorbed);
            dst.Hp -= hpLoss;
            src.Totals.DamageDealt += hpLoss + absorbed;
            dst.Totals.DamageTaken += hpLoss;
            Emit(new BattleEvent { Type = BattleEventType.DamageDealt, Actor = src.Index, Target = dst.Index, Amount = dmg, Absorbed = absorbed, HpAfter = dst.Hp, Crit = crit, SkillId = skill.Id });
            if (dst.Hp <= 0) Die(dst, src);

            var thorns = dst.Total(StatusKind.Thorns);
            if (thorns > 0 && src.Alive && src != dst && hpLoss + absorbed > 0)
            {
                var back = Math.Min(src.Hp, Math.Max(1, DamageCalculator.RoundToInt((hpLoss + absorbed) * thorns)));
                src.Hp -= back;
                src.Totals.DamageTaken += back;
                dst.Totals.DamageDealt += back;
                Emit(new BattleEvent { Type = BattleEventType.ThornsReflected, Actor = dst.Index, Target = src.Index, Amount = back, HpAfter = src.Hp });
                if (src.Hp <= 0) Die(src, dst);
            }

            if (crit && src.Alive) Trigger(src, PassiveTrigger.OnCrit);
            if (dst.Alive)
            {
                Trigger(dst, PassiveTrigger.OnHitTaken);
                Trigger(dst, PassiveTrigger.OnLowHp);
            }
            return hpLoss + absorbed;
        }

        private int _passiveDepth;

        /// <summary>Fires the unit's passive if it listens to <paramref name="trigger"/> and is ready (cooldown, once-per-battle, chance).</summary>
        private void Trigger(Combatant c, PassiveTrigger trigger)
        {
            var p = c.Passive;
            if (p == null || p.Trigger != trigger || !c.Alive || _passiveDepth > 0) return;
            if (p.OncePerBattle && c.PassiveSpent) return;
            if (c.PassiveCooldown > 0) return;
            if (trigger == PassiveTrigger.OnLowHp)
            {
                if (c.LowHpTriggered || c.HpRatio > p.Threshold) return;
                c.LowHpTriggered = true;
            }
            if (!RollChance(p.Chance)) return;
            c.PassiveSpent = true;
            c.PassiveCooldown = p.Cooldown;
            _passiveDepth++;
            try
            {
                var targets = SelectTargets(c, p);
                Emit(new BattleEvent { Type = BattleEventType.PassiveTriggered, Actor = c.Index, SkillId = p.Id, Targets = targets.Select(t => t.Index).ToArray() });
                Execute(c, p, targets);
            }
            finally { _passiveDepth--; }
        }

        private Combatant _currentActor;
        private bool IsCurrentActor(Combatant c) => ReferenceEquals(c, _currentActor);

        private void Die(Combatant c, Combatant killer)
        {
            c.Hp = 0;
            c.Shield = 0;
            c.ShieldTurns = 0;
            c.Statuses.Clear();
            c.StunTurns = 0;
            _queuedSkills.Remove(c.Index);
            Emit(new BattleEvent { Type = BattleEventType.CombatantDied, Actor = killer?.Index ?? -1, Target = c.Index });
            if (killer != null && killer != c && killer.Alive) Trigger(killer, PassiveTrigger.OnKill);
            foreach (var ally in Side(c).Where(a => a.Alive && a != c).ToList()) Trigger(ally, PassiveTrigger.OnAllyDeath);
        }

        private void EndTurn(Combatant actor)
        {
            if (!actor.Alive) return;
            if (actor.ActiveCooldown > 0) actor.ActiveCooldown--;
            if (actor.PassiveCooldown > 0) actor.PassiveCooldown--;
            for (var i = actor.Statuses.Count - 1; i >= 0; i--)
            {
                var s = actor.Statuses[i];
                if (s.SkipNextTick) { s.SkipNextTick = false; continue; }
                if (--s.Remaining > 0) continue;
                actor.Statuses.RemoveAt(i);
                Emit(new BattleEvent { Type = BattleEventType.StatusExpired, Target = actor.Index, StatusId = s.StatusId, Stat = s.Stat, Value = s.Value });
            }
            if (actor.ShieldTurns > 0)
            {
                if (actor.ShieldSkipNextTick) actor.ShieldSkipNextTick = false;
                else if (--actor.ShieldTurns == 0 && actor.Shield > 0)
                {
                    actor.Shield = 0;
                    Emit(new BattleEvent { Type = BattleEventType.ShieldExpired, Target = actor.Index });
                }
            }
        }

        private void CheckEnd()
        {
            if (_players.All(p => !p.Alive))
            {
                Finish(BattleOutcome.Defeat);
                return;
            }
            if (_enemies.Any(e => e.Alive)) return;
            Emit(new BattleEvent { Type = BattleEventType.WaveCleared, Wave = WaveIndex });
            if (WaveIndex + 1 >= _setup.EnemyWaves.Count)
            {
                Finish(BattleOutcome.Victory);
                return;
            }
            StartWave(WaveIndex + 1);
            _order.Clear(); // a new wave starts a new round
        }

        private void Finish(BattleOutcome outcome)
        {
            Outcome = outcome;
            Emit(new BattleEvent { Type = BattleEventType.BattleEnded, Outcome = outcome });
        }

        private void Emit(BattleEvent e)
        {
            e.Round = Round;
            e.Wave = e.Type == BattleEventType.WaveStarted || e.Type == BattleEventType.WaveCleared ? e.Wave : WaveIndex;
            if (e.Type == BattleEventType.TurnStarted) _currentActor = Get(e.Actor);
            _events.Add(e);
            EventEmitted?.Invoke(e);
        }

        public BattleResult BuildResult()
        {
            var r = new BattleResult { Outcome = Outcome, Rounds = Round, Actions = _actions };
            foreach (var p in _players)
            {
                p.Totals.Survived = p.Alive;
                if (p.Alive) r.Survivors.Add(p.DefId);
                else r.PlayerDeaths++;
            }
            foreach (var c in _all)
            {
                if (c.Team == Team.Enemy) c.Totals.Survived = c.Alive;
                r.Combatants.Add(c.Totals);
            }
            return r;
        }

        /// <summary>Stable hash of the event stream (determinism checks).</summary>
        public static string Hash(IEnumerable<BattleEvent> events)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                foreach (var e in events)
                    foreach (var ch in e.ToString())
                    {
                        h ^= ch;
                        h *= 1099511628211UL;
                    }
                return h.ToString("x16");
            }
        }
    }
}
