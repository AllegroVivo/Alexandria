namespace Alexandria.Rules;

public sealed record DefaultEquipmentSet(
    String Name,
    Int32 ID,
    Int32 Weapon,
    Int32 Head,
    Int32 Wrist,
    Int32 Armor,
    Int32 Accessory)
{
    public static IReadOnlyList<DefaultEquipmentSet> Load(String path) =>
    [
        .. MemoriaCSV.ReadRows(path)
            .Select(f => new DefaultEquipmentSet(
                f[0],
                Int32.Parse(f[1]),
                Int32.Parse(f[2]),
                Int32.Parse(f[3]),
                Int32.Parse(f[4]),
                Int32.Parse(f[5]),
                Int32.Parse(f[6])))
    ];
}

