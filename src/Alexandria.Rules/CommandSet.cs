namespace Alexandria.Rules;

public sealed record CommandSet(
    Int32 ID,
    Int32 Regular1,
    Int32 Regular2,
    Int32 Trance1,
    Int32 Trance2)
{
    public static IReadOnlyDictionary<Int32, CommandSet> Load(String path)
        => MemoriaCSV.ReadRows(path)
            .Select(f => new CommandSet(
                Int32.Parse(f[0]),
                Int32.Parse(f[3]),
                Int32.Parse(f[4]),
                Int32.Parse(f[9]),
                Int32.Parse(f[10])))
            .ToDictionary(s => s.ID);
}

