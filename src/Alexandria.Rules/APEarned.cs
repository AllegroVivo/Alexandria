namespace Alexandria.Rules;

/// <summary>
/// Sim-side MODEL, not a mirrored rule: the real game banks per-ability AP from
/// battle history, which a synthetic character lacks. Heuristic: by level N a
/// character has earned ApPerLevel × N AP and learned their cheapest abilities
/// first (cheap spells ride early gear).
/// </summary>
public static class APEarned
{
    public const Int32 APPerLevel = 12;

    public static IReadOnlySet<Int32> LearnedActives(
        IReadOnlyList<LearnableAbility> learnables, Int32 level)
    {
        var budget = APPerLevel * level;
        var learned = new HashSet<Int32>();
        foreach (var a in learnables
                     .Where(a => a.Ability.Kind == AbilityKind.Active)
                     .OrderBy(a => a.APCost))
        {
            if (a.APCost > budget)
                break;
            budget -= a.APCost;
            learned.Add(a.Ability.ID);
        }

        return learned;
    }
}
