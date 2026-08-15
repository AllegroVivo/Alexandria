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
    
    public IReadOnlyList<BattleUnit> Players { get; }
    public IReadOnlyList<BattleUnit> Enemies { get; }

    public BattleEngine(IReadOnlyList<BattleUnit> players, IReadOnlyList<BattleUnit> enemies,
        EncounterKind kind, Random rng, Int32 coefficient = ATBClock.NormalCoefficient)
    {
        Players = players;
        Enemies = enemies;
        _rng = rng;
        _coefficient = coefficient;
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
                ATBClock.Tick(unit, _coefficient);
        }
        return null;
    }

    public Boolean IsEnemy(BattleUnit unit) => Enemies.Contains(unit);

    /// <summary>Basic attack + damage application + gauge reset: one resolved turn.</summary>
    public BattleEvent ExecuteAttack(BattleUnit attacker, BattleUnit target)
    {
        var result = PhysicalAttack.Execute(
            attacker, target, attacker.AttackPower, _rng, enemyStyle: IsEnemy(attacker));
        PhysicalAttack.Apply(target, result.Damage);
        if (result.Damage > 0 && target.Has(BattleStatus.Sleep))
            target.Statuses &= ~BattleStatus.Sleep;
        ATBClock.ResetAfterAction(attacker);
        return new BattleEvent(attacker, target, result);
    }

    /// <summary>Cast + MP spend + damage + gauge reset: one resolved magic turn.</summary>
    public BattleEvent ExecuteSpell(BattleUnit caster, BattleUnit target, Int32 spellPower, Int32 mpCost)
    {
        caster.CurrentMP -= mpCost;
        var result = MagicAttack.Execute(caster, target, spellPower, _rng);
        PhysicalAttack.Apply(target, result.Damage);
        ATBClock.ResetAfterAction(caster);
        return new BattleEvent(caster, target, result);
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
        if (hitRate <= _rng.Next(0, UInt16.MaxValue) % 100)
            result = new AttackResult(AttackOutcome.Miss, 0);
        else if (target.MagicalEvade > _rng.Next(0, UInt16.MaxValue) % 100)
            result = new AttackResult(AttackOutcome.Evaded, 0);
        else
        {
            target.Statuses |= (BattleStatus)statusMask;
            result = new AttackResult(AttackOutcome.Hit, 0);
        }

        ATBClock.ResetAfterAction(caster);
        return new BattleEvent(caster, target, result);
    }

    /// <summary>Enemy AI, 1999 grade: smack a random living player.</summary>
    public BattleEvent ExecuteEnemyTurn(BattleUnit enemy)
    {
        var living = Players.Where(p => p.IsAlive).ToList();
        return ExecuteAttack(enemy, living[_rng.Next(0, UInt16.MaxValue) % living.Count]);
    }
}

public sealed record BattleEvent(BattleUnit Actor, BattleUnit Target, AttackResult Result);
