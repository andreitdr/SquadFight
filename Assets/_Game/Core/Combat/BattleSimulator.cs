using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Deterministic turn-based auto battle. Each <see cref="Step"/> performs one combatant's turn (plus any round/wave
    /// transitions) and appends <see cref="BattleEvent"/>s. Same setup + seed + queued inputs ⇒ identical events.
    /// Rounds: every living unit acts once, ordered by speed (desc), then slot, then id. Timeout after MaxRounds = defeat.
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
                if (!StartRound()) return false;
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
            }
            EndTurn(actor);
        }

        private SkillDef ChooseSkill(Combatant actor)
        {
            if (!actor.ActiveReady) return actor.Basic;
            if (actor.Team == Team.Enemy) return actor.Active;
            if (AutoSkills || _queuedSkills.Contains(actor.Index)) return actor.Active;
            return actor.Basic;
        }

        private List<Combatant> Opponents(Combatant c) => (c.Team == Team.Player ? _enemies : _players).Where(x => x.Alive).ToList();
        private List<Combatant> Allies(Combatant c) => (c.Team == Team.Player ? _players : _enemies).Where(x => x.Alive).ToList();

        private List<Combatant> SelectTargets(Combatant actor, SkillDef skill)
        {
            switch (skill.Targeting)
            {
                case TargetingRule.SingleEnemy:
                {
                    var foes = Opponents(actor);
                    return foes.Count == 0 ? foes : new List<Combatant> { PickSingle(foes) };
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

        private void Execute(Combatant actor, SkillDef skill, List<Combatant> targets)
        {
            for (var i = 0; i < skill.Effects.Count; i++)
            {
                var effect = skill.Effects[i];
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
                        if (effect.Type != EffectType.Damage || skill.Targeting != TargetingRule.RandomEnemies) continue;
                        var foes = Opponents(actor);
                        if (foes.Count == 0) break;
                        target = foes[_rng.NextInt(0, foes.Count)];
                    }
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
                {
                    var dmg = DamageCalculator.Damage(src.Stat(StatType.Attack), e.Power, dst.Stat(StatType.Defense),
                        src.Stat(StatType.CritChance), src.Stat(StatType.CritDamage), _rules, _rng, out var crit);
                    var absorbed = Math.Min(dst.Shield, dmg);
                    dst.Shield -= absorbed;
                    var hpLoss = Math.Min(dst.Hp, dmg - absorbed);
                    dst.Hp -= hpLoss;
                    src.Totals.DamageDealt += hpLoss + absorbed;
                    dst.Totals.DamageTaken += hpLoss;
                    Emit(new BattleEvent { Type = BattleEventType.DamageDealt, Actor = src.Index, Target = dst.Index, Amount = dmg, Absorbed = absorbed, HpAfter = dst.Hp, Crit = crit, SkillId = skill.Id });
                    if (dst.Hp <= 0) Die(dst);
                    break;
                }
                case EffectType.Heal:
                {
                    var heal = Math.Min(DamageCalculator.Heal(src.Stat(StatType.Attack), e.Power), dst.MaxHp - dst.Hp);
                    dst.Hp += heal;
                    src.Totals.HealingDone += heal;
                    Emit(new BattleEvent { Type = BattleEventType.HealApplied, Actor = src.Index, Target = dst.Index, Amount = heal, HpAfter = dst.Hp, SkillId = skill.Id });
                    break;
                }
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
                {
                    if (!RollChance(e.Chance)) break;
                    var id = string.IsNullOrEmpty(e.StatusId) ? skill.Id + "#" + effectIndex : e.StatusId;
                    var existing = dst.Statuses.Where(s => s.StatusId == id).ToList();
                    var skip = IsCurrentActor(dst);
                    if (existing.Count > 0 && e.Stacking == StackingRule.Ignore) break;
                    if (existing.Count > 0 && (e.Stacking == StackingRule.Refresh || existing.Count >= Math.Max(1, e.MaxStacks)))
                    {
                        foreach (var s in existing) { s.Remaining = e.Duration; s.SkipNextTick = skip; }
                    }
                    else
                    {
                        dst.Statuses.Add(new StatusEffect { StatusId = id, Stat = e.Stat, Value = e.Value, Remaining = e.Duration, SkipNextTick = skip });
                        foreach (var s in existing) { s.Remaining = e.Duration; s.SkipNextTick = skip; }
                    }
                    Emit(new BattleEvent { Type = BattleEventType.StatusApplied, Actor = src.Index, Target = dst.Index, StatusId = id, Stat = e.Stat, Value = e.Value, Duration = e.Duration, Stacks = dst.Statuses.Count(s => s.StatusId == id), SkillId = skill.Id });
                    break;
                }
            }
        }

        private Combatant _currentActor;
        private bool IsCurrentActor(Combatant c) => ReferenceEquals(c, _currentActor);

        private void Die(Combatant c)
        {
            c.Hp = 0;
            c.Shield = 0;
            c.Statuses.Clear();
            c.StunTurns = 0;
            _queuedSkills.Remove(c.Index);
            Emit(new BattleEvent { Type = BattleEventType.CombatantDied, Target = c.Index });
        }

        private void EndTurn(Combatant actor)
        {
            if (!actor.Alive) return;
            if (actor.ActiveCooldown > 0) actor.ActiveCooldown--;
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
