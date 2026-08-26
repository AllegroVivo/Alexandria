namespace Alexandria.Battle;

/// <summary>
/// Periodic status engine, now data-driven (mirror: btl_stat.CheckStatusLoop +
/// SetOprStatusCount). Spirit shapes all four levers: opr reload factor is
/// Spirit×4 for Poison/Venom and (60−Spirit)×4 otherwise; conti duration factor
/// is (60−Spirit)×8 for negative statuses, Spirit×8 for positive, (60−Spirit)×4
/// for the unaligned (Jump). ContiCount 0 = no timer. Tick amounts are MaxHP>>4.
/// </summary>
public static class StatusTicker
{
    /// <summary>The pre-data-driven five, kept as the no-table default so direct
    /// callers (and their tests) keep exact legacy semantics.</summary>
    public static readonly IReadOnlyDictionary<BattleStatus, StatusTiming> DefaultTable =
        new Dictionary<BattleStatus, StatusTiming>
        {
            [BattleStatus.Poison] = new(10, 30, 0, 0),
            [BattleStatus.Regen] = new(10, 45, 0, 0),
            [BattleStatus.Sleep] = new(0, 25, BattleStatus.Defend, 0),
            [BattleStatus.Haste] = new(0, 40, 0, 0),
            [BattleStatus.Slow] = new(0, 40, 0, 0)
        };

    public static List<StatusTickEvent> Tick(BattleUnit unit, Int32 coefficient)
        => Tick(unit, coefficient, DefaultTable);

    public static List<StatusTickEvent> Tick(BattleUnit unit, Int32 coefficient,
        IReadOnlyDictionary<BattleStatus, StatusTiming> table)
    {
        var events = new List<StatusTickEvent>();
        if (!unit.IsAlive)
            return events;

        foreach (var (status, timing) in table)
        {
            if (!unit.Has(status))
                continue;

            if (timing.OprCount > 0)
            {
                var factor = (status & (BattleStatus.Poison | BattleStatus.Venom)) != 0
                    ? unit.Spirit << 2
                    : (60 - unit.Spirit) << 2;
                TickOpr(unit, status, coefficient, timing.OprCount * factor, events);
            }

            if (timing.ContiCount > 0)
            {
                var factor = (status & BattleStatus.AnyNegative) != 0 ? (60 - unit.Spirit) << 3
                    : (status & BattleStatus.AnyPositive) != 0 ? unit.Spirit << 3
                    : (60 - unit.Spirit) << 2;
                TickConti(unit, status, coefficient, timing.ContiCount * factor, events);
            }
        }
        return events;
    }

    private static void TickOpr(BattleUnit unit, BattleStatus status,
        Int32 coefficient, Int32 reload, List<StatusTickEvent> events)
    {
        var counter = unit.OprCounters.GetValueOrDefault(status, reload) - coefficient;
        if (counter <= 0)
        {
            counter = reload;
            var amount = Math.Max(1, unit.MaxHP >> 4);
            var healing = status == BattleStatus.Regen && !unit.Has(BattleStatus.Zombie);
            if (healing)
                unit.CurrentHP = Math.Min(unit.MaxHP, unit.CurrentHP + amount);
            else
                unit.CurrentHP = unit.CurrentHP > amount ? unit.CurrentHP - amount : 0;
            events.Add(new StatusTickEvent(unit, status, healing ? amount : -amount, Expired: false));
        }
        unit.OprCounters[status] = counter;
    }

    private static void TickConti(BattleUnit unit, BattleStatus status,
        Int32 coefficient, Int32 duration, List<StatusTickEvent> events)
    {
        var counter = unit.ContiCounters.GetValueOrDefault(status, duration) - coefficient;
        if (counter < 0)
        {
            unit.Statuses &= ~status;
            unit.ContiCounters.Remove(status);
            unit.OprCounters.Remove(status);
            events.Add(new StatusTickEvent(unit, status, 0, Expired: true));
            return;
        }
        unit.ContiCounters[status] = counter;
    }
}

public sealed record StatusTickEvent(
    BattleUnit Unit,
    BattleStatus Status,
    Int32 HPDelta,
    Boolean Expired);
