namespace Alexandria.Rules;

/// <summary>Equipment slots in EquipmentKit's own order (Weapon, Head, Wrist, Armor,
/// Accessory) - note Items.csv lists Armlet before Helmet, so the columns cross over.
/// </summary>
public enum EquipSlot
{
    Weapon = 0,
    Helmet = 1,
    Wrist = 2,
    Armor = 3,
    Accessory = 4
}

public sealed record Item(
    Int32 ID,
    Int32 WeaponID,
    Int32 ArmorID,
    UInt32 Price,
    Int32 BonusID,
    Int32 EffectID,
    IReadOnlyList<AbilityRef> TaughtAbilities,
    EquipSlot? Slot = null,
    IReadOnlySet<String>? Wearers = null)
{
    public static IReadOnlyDictionary<Int32, Item> Load(String path)
        => MemoriaCSV.ReadRows(path)
            .Select(f => new Item(
                Int32.Parse(f[0]),
                Int32.Parse(f[1]),
                Int32.Parse(f[2]),
                UInt32.Parse(f[4]),
                Int32.Parse(f[9]),
                Int32.Parse(f[3]),
                AbilityRef.ParseArray(f[10]),
                ReadSlot(f),
                ReadWearers(f)))
            .ToDictionary(i => i.ID);

    /// <summary>Items.csv columns 20-31, in file order.</summary>
    private static readonly String[] CharacterOrder =
    [
        "Zidane", "Vivi", "Garnet", "Steiner", "Freya", "Quina",
        "Eiko", "Amarant", "Cinna", "Marcus", "Blank", "Beatrix"
    ];

    private static EquipSlot? ReadSlot(String[] f) =>
        f[11] == "1" ? EquipSlot.Weapon
        : f[13] == "1" ? EquipSlot.Helmet
        : f[12] == "1" ? EquipSlot.Wrist
        : f[14] == "1" ? EquipSlot.Armor
        : f[15] == "1" ? EquipSlot.Accessory
        : null;

    private static IReadOnlySet<String> ReadWearers(String[] f) =>
        CharacterOrder.Where((_, i) => f[20 + i] == "1").ToHashSet();
}

