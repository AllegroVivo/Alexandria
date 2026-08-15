namespace Alexandria.Rules;

public sealed record StatusSet(String Name, Int32 ID, UInt32 StatusMask)
{
    public static IReadOnlyDictionary<Int32, StatusSet> Load(String path)
        => MemoriaCSV.ReadRows(path)
            .Select(f => new StatusSet(
                f[0],
                Int32.Parse(f[1]),
                ParseMask(f.Length > 2 ? f[2] : "")))
            .ToDictionary(s => s.ID);

    private static UInt32 ParseMask(String field)
    {
        UInt32 mask = 0;
        foreach (var part in field.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bit = Int32.Parse(part[(part.IndexOf('(') + 1)..part.IndexOf(')')]);
            mask |= 1u << bit;
        }
        return mask;
    }
}

