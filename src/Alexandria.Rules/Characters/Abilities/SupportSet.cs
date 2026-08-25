namespace Alexandria.Rules;

public static class SupportSet
{
    public static SupportAbility Equip(IReadOnlyList<AbilityRef> taught,
        IReadOnlyDictionary<Int32, AbilityGem> gems, Int32 stones)
    {
        var set = SupportAbility.None;
        foreach (var (id, cost) in taught
                     .Where(a => a is { Kind: AbilityKind.Support, ID: >= 0 and < 64 })
                     .Select(a => a.ID)
                     .Distinct()
                     .Select(id => (ItemID: id, Cost: gems.TryGetValue(id, out var g) ? g.Gems : Int32.MaxValue))
                     .OrderBy(a => a.Cost))
        {
            if (cost > stones)
                break;
            stones -= cost;
            set |= (SupportAbility)(1UL << id);
        }
        return set;
    }
}

