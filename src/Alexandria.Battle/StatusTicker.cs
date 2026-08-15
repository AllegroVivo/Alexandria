namespace Alexandria.Battle;

/// <summary>
/// Periodic status engine (mirror: btl_stat.CheckStatusLoop/SetOprStatusCount + Poison/Regen scripts).
/// Counters count DOWN by the ATB coefficient each battle frame: opr fires periodic
/// effects and reloads; conti expires timed statuses when it goes negative.
/// Spirit shapes everything: slower poison, faster regen, shorter debuffs, longer buffs.
/// </summary>
public static class StatusTicker
{
    public static List<StatusTickEvent> Tick(BattleUnit unit, Int32 coefficient)
    {
        var events = new List<StatusTickEvent>();
        if (!unit.IsAlive)
            return events;
        
        // --- opr: periodic effects (amount = MaxHP >> 4, PoisonStatusScript/RegenStatusScript) ---
        if (unit.Has(BattleStatus.Poison))
            TickOpr(unit, BattleStatus.Poison, coefficient, 10 * (unit.Spirit << 2), events, healing: false);
        if (unit.Has(BattleStatus.Regen))
            TickOpr(unit, BattleStatus.Regen, coefficient, 10 * ((60 - unit.Spirit) << 2), events, healing: true);
        
        // --- conti: timed expiry (btl_stat.cs:100-104; negative ×8 vs positive ×8 factors) ---
        TickConti(unit, BattleStatus.Sleep, coefficient, 25 * ((60 - unit.Spirit) << 3), events);
        TickConti(unit, BattleStatus.Slow, coefficient, 40 * ((60 - unit.Spirit) << 3), events);
        TickConti(unit, BattleStatus.Haste, coefficient, 40 * (unit.Spirit << 3), events);

        return events;
    }

    private static void TickOpr(BattleUnit unit, BattleStatus status, Int32 coefficient,
        Int32 reload, List<StatusTickEvent> events, Boolean healing)
    {
        var counter = unit.OprCounters.GetValueOrDefault(status, reload) - coefficient;
        if (counter <= 0)
        {
            counter = reload;
            var amount = Math.Max(1, unit.MaxHP >> 4);
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
        if (!unit.Has(status))
            return;
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