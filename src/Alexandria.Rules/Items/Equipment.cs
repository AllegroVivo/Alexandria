namespace Alexandria.Rules;

public static class Equipment
{
    public const Int32 NoItem = 255;

    /// <summary>Growth points granted per level-up by a set of equipped items.</summary>
    public static GrowthBonuses GrowthPerLevel(
        IEnumerable<Int32> equippedItemIDs,
        IReadOnlyDictionary<Int32, Item> items,
        IReadOnlyDictionary<Int32, ItemStats> stats)
    {
        Int32 dex = 0, str = 0, mgc = 0, wpr = 0;
        foreach (var id in equippedItemIDs)
        {
            if (id == NoItem || !items.TryGetValue(id, out var item))
                continue;
            var s = stats[item.BonusID];
            dex += s.Dexterity;
            str = s.Strength;
            mgc = s.Magic;
            wpr = s.Willpower;
        }
        return new GrowthBonuses(
            (UInt16)dex, (UInt16)str, (UInt16)mgc, (UInt16)wpr, 0);
    }
}

