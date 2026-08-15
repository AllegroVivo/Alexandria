namespace Alexandria.Rules;

public sealed record Item(
    Int32 ID,
    Int32 WeaponID,
    Int32 ArmorID,
    UInt32 Price,
    Int32 BonusID,
    IReadOnlyList<AbilityRef> TaughtAbilities)
{
    public static IReadOnlyDictionary<Int32, Item> Load(String path)
        => MemoriaCSV.ReadRows(path)
            .Select(f => new Item(
                Int32.Parse(f[0]),
                Int32.Parse(f[1]),
                Int32.Parse(f[2]),
                UInt32.Parse(f[4]),
                Int32.Parse(f[9]),
                AbilityRef.ParseArray(f[10])))
            .ToDictionary(i => i.ID);
}

