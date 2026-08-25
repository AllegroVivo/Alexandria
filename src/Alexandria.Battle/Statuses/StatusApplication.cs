namespace Alexandria.Battle;

/// <summary>Status landing gate (mirror: btl_stat.AlterStatus's immunity loop +
/// RemoveStatuses(ClearOnApply)). Current statuses can block incoming ones;
/// landing statuses can clear others. Cleared statuses drop their timers so a
/// re-application starts fresh.</summary>
public static class StatusApplication
{
    public static BattleStatus Apply(BattleUnit target, BattleStatus statuses,
        IReadOnlyDictionary<BattleStatus, StatusTiming> table)
    {
        statuses &= ~target.ImmuneStatuses;
        
        foreach(var (current, timing) in table)
            if (target.Has(current))
                statuses &= ~timing.ImmunityProvided;
        
        foreach(var(incoming, timing) in table)
            if ((statuses & incoming) != 0 && timing.ClearOnApply != 0)
                Remove(target, timing.ClearOnApply);

        target.Statuses |= statuses;
        return statuses;
    }

    public static void Remove(BattleUnit target, BattleStatus statuses)
    {
        target.Statuses &= ~statuses;
        foreach (var s in target.ContiCounters.Keys.Where(k => (k & statuses) != 0).ToList())
            target.ContiCounters.Remove(s);
        foreach (var s in target.OprCounters.Keys.Where(k => (k & statuses) != 0).ToList())
            target.OprCounters.Remove(s);
    }
}

