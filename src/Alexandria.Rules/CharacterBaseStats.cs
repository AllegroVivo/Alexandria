namespace Alexandria.Rules;

public sealed record CharacterBaseStats(
    String Name,
    Int32 Id,
    Byte Dexterity,
    Byte Strength,
    Byte Magic,
    Byte Willpower,
    UInt32 Gems)
{
    public static IReadOnlyList<CharacterBaseStats> Load(String path)
        => MemoriaCsv.ReadRows(path)
            .Select(f => new CharacterBaseStats(
                f[0],
                Int32.Parse(f[1]),
                Byte.Parse(f[2]),
                Byte.Parse(f[3]),
                Byte.Parse(f[4]),
                Byte.Parse(f[5]),
                UInt32.Parse(f[6])))
            .ToList();
}

