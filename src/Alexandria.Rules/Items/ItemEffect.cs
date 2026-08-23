namespace Alexandria.Rules;

/// <summary>A row of Items\ItemEffects.csv - what an item DOES when used.</summary>
public sealed record ItemEffect(
    Int32 ID,
    Int32 TargetType,
    Boolean DefaultAlly,
    Boolean ForDead,
    AbilityScript Script,
    Int32 Power,
    Int32 Rate,
    Byte Elements,
    UInt32 Status)
{
    public static IReadOnlyDictionary<Int32, ItemEffect> Load(String path)
        => MemoriaCSV.ReadRows(path)
            .Select(f => new ItemEffect(
                Int32.Parse(f[0]),
                Int32.Parse(f[1]),
                f[2] == "1",
                f[5] == "1",
                (AbilityScript)Int32.Parse(f[7]),
                Int32.Parse(f[8]),
                Int32.Parse(f[9]),
                Byte.Parse(f[10]),
                UInt32.Parse(f[11])))
            .ToDictionary(e => e.ID);
}

