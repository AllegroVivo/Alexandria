namespace Alexandria.Rules;

public sealed record ItemStats(
    String Name,
    Int32 ID,
    Byte Dexterity,
    Byte Strength,
    Byte Magic,
    Byte Willpower,
    Byte AttackElement,
    Byte GuardElement,
    Byte AbsorbElement,
    Byte HalfElement,
    Byte WeakElement)
{
    public String DisplayName => Name[(Name.IndexOf('#') + 1)..].Trim();

    public static IReadOnlyDictionary<Int32, ItemStats> Load(String path)
        => MemoriaCsv.ReadRows(path)
            .Select(f => new ItemStats(
                f[0],
                Int32.Parse(f[1]),
                Byte.Parse(f[2]),
                Byte.Parse(f[3]),
                Byte.Parse(f[4]),
                Byte.Parse(f[5]),
                Byte.Parse(f[6]),
                Byte.Parse(f[7]),
                Byte.Parse(f[8]),
                Byte.Parse(f[9]),
                Byte.Parse(f[10])))
            .ToDictionary(s => s.ID);
}

