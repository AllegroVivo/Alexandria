namespace Alexandria.Rules;

public sealed record Armor(
    String Name,
    Int32 ID,
    Int32 PhysicalDefense,
    Int32 PhysicalEvade,
    Int32 MagicalDefense,
    Int32 MagicalEvade)
{
    public static IReadOnlyDictionary<Int32, Armor> Load(String path)
        => MemoriaCSV.ReadRows(path)
            .Select(f => new Armor(
                f[0],
                Int32.Parse(f[1]),
                Int32.Parse(f[2]),
                Int32.Parse(f[3]),
                Int32.Parse(f[4]),
                Int32.Parse(f[5])))
            .ToDictionary(a => a.ID);
}

