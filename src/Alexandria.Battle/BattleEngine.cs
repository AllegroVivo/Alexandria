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
    private IReadOnlyDictionary<BattleStatus, StatusTiming> _statusTable;
    private Boolean _reacting;
    
    public List<BattleUnit> Players { get; }
    public List<BattleUnit> Enemies { get; }
    
    public List<StatusTickEvent> PendingStatusEvents { get; } = [];

    /// <summary>Reactions fired by the last resolved action, for the caller to narrate
    /// (a counter's retaliation resolves as a normal enemy action).</summary>
    public List<BattleEvent> PendingReactions { get; } = [];

    public BattleEngine(List<BattleUnit> players, List<BattleUnit> enemies,
        EncounterKind kind, Random rng, Int32 coefficient = ATBClock.NormalCoefficient,
        IReadOnlyDictionary<BattleStatus, StatusTiming>? statusTable = null, EngineHost? host = null)
    {
        Players = players;
        Enemies = enemies;
        _rng = rng;
        _coefficient = coefficient;
        _statusTable = statusTable ?? StatusTicker.DefaultTable;
        _host = host;
        ATBClock.PlaceInitialGauges(players, enemies, kind, rng);
    }

    public Boolean Victory => Enemies.All(e => !e.IsAlive);
    public Boolean Defeat => Players.All(p => !p.IsAlive);
    public Boolean IsOver => Victory || Defeat;

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

            foreach (var unit in AllUnits)
            {
                ATBClock.Tick(unit, _coefficient);
                PendingStatusEvents.AddRange(StatusTicker.Tick(unit, _coefficient, _statusTable));
            }
        }
        return null;
    }

    public Boolean IsEnemy(BattleUnit unit) => Enemies.Contains(unit);

    /// <summary>Basic attack + damage application + gauge reset: one resolved turn.</summary>
    public BattleEvent ExecuteAttack(BattleUnit attacker, BattleUnit target, 
        Int32? power = null, Element? element = null)
    {
        var result = PhysicalAttack.Execute(
            attacker, target, power ?? attacker.AttackPower, _rng,
            enemyStyle: IsEnemy(attacker), element: element ?? attacker.WeaponElement);
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
            &&enemy.Brain.Decide(enemy, Players, _rng) is var(attackId, chosen)
            && enemy.Attacks.FirstOrDefault(a=>a.ID==attackId) is {} scripted)
        {
            enemy.CurrentMP-=scripted.MP;
            var ev = scripted.ScriptID switch
            {
                9 => ExecuteSpell(enemy, chosen, scripted.Power, 0, scripted.StatusMask, scripted.Rate, scripted.Elements),
                11 => ExecuteStatusSpell(enemy, chosen, scripted.StatusMask, scripted.Rate, 0),
                _ => ExecuteAttack(enemy, chosen, scripted.Power, scripted.Elements)
            };
            return ev with { Attack = scripted };
        }
        
        var living = Players.Where(p => p.IsAlive).ToList();
        var target = living[_rng.Rand16() % living.Count];

        var usable = enemy.Attacks.Where(a => a.MP <= enemy.CurrentMP).ToList();
        if (usable.Count == 0)
            return ExecuteAttack(enemy, target);

        var attack = usable[_rng.Rand16() % usable.Count];
        enemy.CurrentMP -= attack.MP;
        var e = attack.ScriptID switch
        {
            9 => ExecuteSpell(enemy, target, attack.Power, 0, attack.StatusMask, attack.Rate, attack.Elements),
            11 => ExecuteStatusSpell(enemy, target, attack.StatusMask, attack.Rate, 0),
            _ => ExecuteAttack(enemy, target, attack.Power, attack.Elements)
        };
        return e with { Attack = attack };
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
            PendingReactions.Add(ExecuteAttack(target, chosen, retaliation.Power, retaliation.Elements) with { Attack = retaliation });
        }
        finally
        {
            _reacting = false;
        }
    }
}

public sealed record BattleEvent(
    BattleUnit Actor,
    BattleUnit Target,
    AttackResult Result,
    BattleStatus StatusesAdded = 0,
    EnemyAttack? Attack = null);
