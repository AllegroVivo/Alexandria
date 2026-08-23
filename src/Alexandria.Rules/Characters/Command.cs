namespace Alexandria.Rules;

public sealed record Command(
    Int32 ID,
    Int32 Type,
    Int32 Ability,
    IReadOnlyList<Int32> Abilities)
{
    public static IReadOnlyDictionary<Int32, Command> Load(String path)
        => MemoriaCSV.ReadRows(path)
            .Select(f => new Command(
                Int32.Parse(f[0]),
                Int32.Parse(f[1]),
                Int32.Parse(f[2]),
                f.Length > 3 ? MemoriaCSV.ParseIntArray(f[3]) : []))
            .ToDictionary(c => c.ID);
}
