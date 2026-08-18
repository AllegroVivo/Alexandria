using Alexandria.Formats.Scripts;

namespace Alexandria.Battle;

/// <summary>
/// The headless battle loop: tick gauges until someone is ready, let them act, reset, repeat.
/// Turn order is not a list - it emerges from the clock, exactly like the original.
/// v1 simplifications: ready units act immediately (no command queue or animation delay);
/// simultaneous fills resolve players-first in slot order.
/// </summary>
public sealed class BattleEngine
{
    private readonly Random _rng;
    private readonly Int32 _coefficient;
    private readonly EngineHost? _host;

    private const Int32 OpeningBudget = 600; // 40s

    private IReadOnlyDictionary<BattleStatus, StatusTiming> _statusTable;
    private Boolean _reacting;
    private readonly EbVM? _director;
    private Int32 _openingFrames;

    public List<BattleUnit> Players { get; }
    public List<BattleUnit> Enemies { get; }

    public List<StatusTickEvent> PendingStatusEvents { get; } = [];

    /// <summary>Reactions fired by the last resolved action, for the caller to narrate
    /// (a counter's retaliation resolves as a normal enemy action).</summary>
    public List<BattleEvent> PendingReactions { get; } = [];

    public BattleEngine(List<BattleUnit> players, List<BattleUnit> enemies,
        EncounterKind kind, Random rng, Int32 coefficient = ATBClock.NormalCoefficient,
        IReadOnlyDictionary<BattleStatus, StatusTiming>? statusTable = null, EngineHost? host = null,
        EbVM? director = null)
    {
        Players = players;
        Enemies = enemies;
        _rng = rng;
        _coefficient = coefficient;
        _statusTable = statusTable ?? StatusTicker.DefaultTable;
        _host = host;
        _director = director;
        ATBClock.PlaceInitialGauges(players, enemies, kind, rng);
    }

    public Boolean Victory => Enemies.All(e => !e.IsAlive);
    public Boolean Defeat => Players.All(p => !p.IsAlive);
    public Boolean IsOver => Victory || Defeat || _host?.Result is not null;

    private IEnumerable<BattleUnit> AllUnits => Players.Concat(Enemies);

    /// <summary>Advances the clock until a living unit is ready; null when the battle is over.</summary>
    public BattleUnit? TickUntilReady()
    {
        while (!IsOver)
        {
            var ready = AllUnits.FirstOrDefault(u => u is { IsAlive: true, ATBReady: true }
                                                     && !u.Has(BattleStatus.PreventATBConfirm | BattleStatus.Stop | BattleStatus.Petrify));
            if (ready is not null)
                return ready;

            TickScripts();
            if (_host is not null && _host.Phase != EngineHost.PhaseNormal)
            {
                if (++_openingFrames <= OpeningBudget)
                    continue;
                _host.Phase = EngineHost.PhaseNormal;
            }

            foreach (var unit in AllUnits)
            {
                ATBClock.Tick(unit, _coefficient);
                PendingStatusEvents.AddRange(StatusTicker.Tick(unit, _coefficient, _statusTable));
            }
        }
        return null;
    }

    /// <summary>One frame of every script thread (mirror: ProcessEvents running each
    /// battle object per frame).</summary>
    private void TickScripts()
    {
        if (_host is null)
            return;
        if (_director is not null)
        {
            _host.BeginSlice(8);
            _director.Tick();
        }
        foreach (var unit in AllUnits)
            (unit.Brain as ScriptedBrain)?.Tick();
    }

    public Boolean IsEnemy(BattleUnit unit) => Enemies.Contains(unit);

    /// <summary>Basic attack + damage application + gauge reset: one resolved turn.</summary>
    public BattleEvent ExecuteAttack(BattleUnit attacker, BattleUnit target,
        Int32? power = null, Element? element = null, Boolean precise = false)
    {
        var result = PhysicalAttack.Execute(
            attacker, target, power ?? attacker.AttackPower, _rng,
            enemyStyle: IsEnemy(attacker), element: element ?? attacker.WeaponElement,
            precise: precise);
        PhysicalAttack.Apply(target, result);
        if (result.Damage > 0 && target.Has(BattleStatus.Sleep))
            StatusApplication.Remove(target, BattleStatus.Sleep);
        ATBClock.ResetAfterAction(attacker);
        Provoke(attacker, target, 1);
        return new BattleEvent(attacker, target, result);
    }

    /// <summary>Cast + MP spend + damage + gauge reset: one resolved magic turn.</summary>
    public BattleEvent ExecuteSpell(BattleUnit caster, BattleUnit target, Int32 spellPower,
        Int32 mpCost, UInt32 riderStatuses = 0, Int32 riderRate = 0,
        Element element = Element.None)
    {
        caster.CurrentMP -= mpCost;
        var result = MagicAttack.Execute(caster, target, spellPower, _rng, element: element);
        var added = result.Outcome is AttackOutcome.Hit or AttackOutcome.Absorbed
                    && riderStatuses != 0 && riderRate > _rng.Rand16() % 100
            ? StatusApplication.Apply(target, (BattleStatus)riderStatuses, _statusTable)
            : 0;
        PhysicalAttack.Apply(target, result);
        ATBClock.ResetAfterAction(caster);
        Provoke(caster, target, 22);
        return new BattleEvent(caster, target, result, added);
    }

    /// <summary>Status magic (mirror: 0011_MagicApplyNegativeStatusScript): one accuracy
    /// roll folding in spell rate, caster Magic/4, and level difference; Shell and
    /// multi-targeting halve it; no damage - on a hit the statuses simply land.</summary>
    public BattleEvent ExecuteStatusSpell(BattleUnit caster, BattleUnit target,
        UInt32 statusMask, Int32 baseRate, Int32 mpCost, Boolean multiTarget = false)
    {
        caster.CurrentMP -= mpCost;

        var hitRate = Math.Clamp(baseRate + (caster.Magic >> 2) + caster.Level - target.Level, 1, 100);
        if (target.Has(BattleStatus.Shell))
            hitRate /= 2;
        if (multiTarget)
            hitRate /= 2;

        AttackResult result;
        BattleStatus landed = 0;
        if (hitRate <= _rng.Rand16() % 100)
            result = new AttackResult(AttackOutcome.Miss, 0);
        else if (target.MagicalEvade > _rng.Rand16() % 100)
            result = new AttackResult(AttackOutcome.Evaded, 0);
        else
        {
            landed = StatusApplication.Apply(target, (BattleStatus)statusMask, _statusTable);
            result = new AttackResult(AttackOutcome.Hit, 0);
        }

        ATBClock.ResetAfterAction(caster);
        Provoke(caster, target, 22);
        return new BattleEvent(caster, target, result, landed);
    }

    /// <summary>Enemy turn. Target: uniform among living players (mirror: the B_SELECT
    /// operator, EventEngine.OperatorSelect). Attack choice: uniform among affordable
    /// roster entries.</summary>
    public BattleEvent ExecuteEnemyTurn(BattleUnit enemy)
    {
        if (enemy.Brain is not null
            && enemy.Brain.Decide(enemy, Players, _rng) is var (attackId, chosen)
            && enemy.Attacks.FirstOrDefault(a => a.ID == attackId) is { } scripted)
            return Resolve(enemy, chosen, scripted);

        var living = Players.Where(p => p.IsAlive).ToList();
        var target = living[_rng.Rand16() % living.Count];

        var usable = enemy.Attacks.Where(a => a.MP <= enemy.CurrentMP).ToList();
        if (usable.Count == 0)
            return ExecuteAttack(enemy, target);
        return Resolve(enemy, target, usable[_rng.Rand16() % usable.Count]);
    }

    /// <summary>Mirror: SBattleCalculator.CalcResult's tail — every resolved
    /// (command, target) pair provokes the target's script. Enemy targets only,
    /// player attackers only, blocked by PreventCounter; HP-zero fires the death
    /// function (tag 9), otherwise the counter (tag 6). Counters can't chain:
    /// vanilla gates on IsCommandDeclarable, and a retaliation isn't declarable.</summary>
    private void Provoke(BattleUnit attacker, BattleUnit target, Int32 commandId)
    {
        if (_reacting || !IsEnemy(target) || IsEnemy(attacker))
            return;
        if (target.Has(BattleStatus.CannotAct | BattleStatus.Death | BattleStatus.Sleep))
            return;
        if (target.Brain is not ScriptedBrain brain)
            return;

        var tag = (UInt16)(target.IsAlive ? 6 : 9);
        if (tag == 9 && !target.DiesFighting)
            return;

        _reacting = true;
        try
        {
            if (brain.React(tag, 1 << Players.IndexOf(attacker), commandId, 0) is not { } attackId ||
                target.Attacks.FirstOrDefault(a => a.ID == attackId) is not { } retaliation)
                return;
            var chosen = _host?.UnitFromMask(_host.SysList(0)) ?? attacker;
            PendingReactions.Add(Resolve(target, chosen, retaliation));
        }
        finally
        {
            _reacting = false;
        }
    }

    /// <summary>Resolve an enemy attack through its ability-script id, the way vanilla
    /// dispatches via ScriptsLoader.GetBattleScript — ids we implement do their work,
    /// and an id with no script resolves to nothing.</summary>
    private BattleEvent Resolve(BattleUnit actor, BattleUnit target, EnemyAttack attack)
    {
        actor.CurrentMP -= attack.MP;
        var ev = attack.ScriptID switch
        {
            8 => ExecuteAttack(actor, target, attack.Power, attack.Elements),
            9 => ExecuteSpell(actor, target, attack.Power, 0, attack.StatusMask, attack.Rate, attack.Elements),
            10 => ExecuteHeal(actor, target, attack.Power),
            11 => ExecuteStatusSpell(actor, target, attack.StatusMask, attack.Rate, 0),
            12 => ExecuteCure(actor, target, attack.StatusMask),
            16 => ExecuteDrainHP(actor, target, attack.Power, attack.StatusMask, attack.Rate, attack.Elements),
            17 => ExecuteGravity(actor, target, attack.Power, attack.Rate),
            25 => ExecuteDirectHP(actor, target, attack.Power, attack.Rate, precise: true),
            27 => ExecuteDirectHP(actor, target, attack.Power, attack.Rate, precise: false),
            30 => ExecuteWhiteWind(actor, target, attack.Power),
            100 => ExecuteAttack(actor, target, attack.Power, attack.Elements, precise: true),
            103 => ExecuteBuff(actor, target, attack.StatusMask),
            109 => ExecuteMini(actor, target, attack.StatusMask, attack.Rate),
            _ => Choreography(actor, target)
        };
        return ev with { Attack = attack };
    }

    /// <summary>An attack whose script we do not run: the animation plays, nothing else
    /// happens, and the actor still spends its turn.</summary>
    private BattleEvent Choreography(BattleUnit actor, BattleUnit target)
    {
        ATBClock.ResetAfterAction(actor);
        return new BattleEvent(actor, target, new AttackResult(AttackOutcome.NoEffect, 0));
    }

    /// <summary>Mirror: BattleCalculator.MagicAccuracy + TryMagicHit — the ability's own
    /// rate plus Magic/4 plus the level difference, clamped to 1..100 and halved by Shell.
    /// </summary>
    private AttackOutcome MagicRoll(BattleUnit caster, BattleUnit target, Int32 baseRate)
    {
        var hitRate = Math.Clamp(baseRate + (caster.Magic >> 2) + caster.Level - target.Level, 1, 100);
        if (target.Has(BattleStatus.Shell))
            hitRate /= 2;
        if (hitRate <= _rng.Rand16() % 100)
            return AttackOutcome.Miss;
        if (target.MagicalEvade > _rng.Rand16() % 100)
            return AttackOutcome.Evaded;
        return AttackOutcome.Hit;
    }

    /// <summary>Proportional damage (mirror: 0017_MagicGravityDamageScript →
    /// CalcProportionDamage): a fixed percentage of the target's MAXIMUM HP, capped at
    /// 100%.</summary>
    public BattleEvent ExecuteGravity(BattleUnit caster, BattleUnit target, Int32 power, Int32 rate)
    {
        var outcome = MagicRoll(caster, target, rate);
        var damage = 0;
        if (outcome == AttackOutcome.Hit)
        {
            damage = target.MaxHP * Math.Min(power, 100) / 100;
            target.CurrentHP = Math.Max(0, target.CurrentHP - damage);
        }
        ATBClock.ResetAfterAction(caster);
        Provoke(caster, target, 22);
        return new BattleEvent(caster, target, new AttackResult(outcome, damage));
    }

    /// <summary>Direct HP (mirror: BattleCalculator.TryDirectHPDamage) — the target's HP
    /// is SET to the ability's power, not reduced by it.</summary>
    public BattleEvent ExecuteDirectHP(BattleUnit caster, BattleUnit target, Int32 power, Int32 rate, Boolean precise)
    {
        var outcome = precise ? AttackOutcome.Hit : MagicRoll(caster, target, rate);
        var damage = 0;
        if (outcome == AttackOutcome.Hit)
        {
            if (target.Has(BattleStatus.Death))
                outcome = AttackOutcome.Miss;
            else
            {
                damage = Math.Max(0, target.CurrentHP - power);
                target.CurrentHP = power;
            }
        }
        ATBClock.ResetAfterAction(caster);
        Provoke(caster, target, 22);
        return new BattleEvent(caster, target, new AttackResult(outcome, damage));
    }

    /// <summary>Positive statuses (mirror: 0103 → TryAlterCommandStatuses): buffs are
    /// applied outright, with no accuracy roll and no evade.</summary>
    public BattleEvent ExecuteBuff(BattleUnit caster, BattleUnit target, UInt32 statusMask)
    {
        var landed = StatusApplication.Apply(target, (BattleStatus)statusMask, _statusTable);
        ATBClock.ResetAfterAction(caster);
        Provoke(caster, target, 22);
        return new BattleEvent(caster, target, new AttackResult(AttackOutcome.Hit, 0), landed);
    }

    /// <summary>Drain HP (mirror: 0016_DrainHpScript): ordinary magic damage, then the
    /// caster recovers exactly what the target lost. Divergence: that flow is inverted
    /// against a Zombie target.</summary>
    public BattleEvent ExecuteDrainHP(BattleUnit caster, BattleUnit target, Int32 power, UInt32 statusMask, Int32 rate, Element element)
    {
        var ev = ExecuteSpell(caster, target, power, 0, statusMask, rate, element);
        if (ev.Result is { Outcome: AttackOutcome.Hit, Damage: > 0 })
            caster.CurrentHP = Math.Min(caster.MaxHP, caster.CurrentHP + ev.Result.Damage);
        return ev;
    }

    /// <summary>Magic healing (mirror: 0010_MagicRecoveryScript → CalcHpMagicRecovery):
    /// the ability's power times the caster's magic-attack roll.</summary>
    public BattleEvent ExecuteHeal(BattleUnit caster, BattleUnit target, Int32 power)
    {
        var attack = caster.MaxHP + _rng.Rand16() % (1 + ((caster.Level + caster.Magic) >> 3));
        if (caster.Has(BattleStatus.Mini))
            attack >>= 1;
        var healed = Math.Min(Math.Min(power * attack, MagicAttack.DamageCap), target.MaxHP - target.CurrentHP);
        target.CurrentHP += healed;
        ATBClock.ResetAfterAction(caster);
        return new BattleEvent(caster, target, new AttackResult(AttackOutcome.Healed, healed));
    }

    /// <summary>White Wind (mirror: 0030_WhiteWindScript): a share of the CASTER's maximum
    /// HP — a third of it when the ability's power is 0, which is what the vanilla data
    /// carries.</summary>
    public BattleEvent ExecuteWhiteWind(BattleUnit caster, BattleUnit target, Int32 power)
    {
        var amount = power == 0 ? caster.MaxHP / 2 : caster.MaxHP * power / 100;
        var healed = Math.Min(amount, target.MaxHP - target.CurrentHP);
        target.CurrentHP += healed;
        ATBClock.ResetAfterAction(caster);
        return new BattleEvent(caster, target, new AttackResult(AttackOutcome.Healed, healed));
    }

    /// <summary>Status cure (mirror: 0012_MagicCureStatusScript → TryRemoveAbilityStatuses):
    /// the listed statuses are lifted outright, and if the target carried none of them the
    /// ability registers a miss.</summary>
    public BattleEvent ExecuteCure(BattleUnit caster, BattleUnit target, UInt32 statusMask)
    {
        var present = target.Statuses & (BattleStatus)statusMask;
        if (present != 0)
            StatusApplication.Remove(target, present);
        ATBClock.ResetAfterAction(caster);
        return new BattleEvent(caster, target,
            new AttackResult(present != 0 ? AttackOutcome.Healed : AttackOutcome.Miss, 0));
    }

    /// <summary>Mini (mirror: 0109_MiniScript): a target already shrunk is re-hit with no
    /// roll at all — that is how the spell reliably un-shrinks.</summary>
    public BattleEvent ExecuteMini(BattleUnit caster, BattleUnit target, UInt32 statusMask, Int32 rate)
    {
        var outcome = target.Has(BattleStatus.Mini)
            ? AttackOutcome.Hit
            : MagicRoll(caster, target, rate);
        BattleStatus landed = 0;
        if (outcome == AttackOutcome.Hit)
            landed = StatusApplication.Apply(target, (BattleStatus)statusMask, _statusTable);
        ATBClock.ResetAfterAction(caster);
        Provoke(caster, target, 22);
        return new BattleEvent(caster, target, new AttackResult(outcome, 0), landed);
    }
}

public sealed record BattleEvent(
    BattleUnit Actor,
    BattleUnit Target,
    AttackResult Result,
    BattleStatus StatusesAdded = 0,
    EnemyAttack? Attack = null);
