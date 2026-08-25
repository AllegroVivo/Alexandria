namespace Alexandria.Rules;

public sealed record Weapon(
    String Name,
    Int32 ID,
    Int32 Power,
    Byte Elements,
    Int32 StatusIndex,
    Int32 StatusRate,
    WeaponCategory Category)
{
    public static IReadOnlyDictionary<Int32, Weapon> Load(String path)
        => MemoriaCSV.ReadRows(path)
            .Select(f => new Weapon(
                f[0],
                Int32.Parse(f[1]),
                Int32.Parse(f[6]),
                Byte.Parse(f[7]),
                Int32.Parse(f[3]),
                Int32.Parse(f[8]),
                (WeaponCategory)Byte.Parse(f[2])))
            .ToDictionary(w => w.ID);
}
