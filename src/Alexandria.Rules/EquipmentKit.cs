namespace Alexandria.Rules;

public sealed record EquipmentKit(
    Int32 WeaponPower,
    Int32 PhysicalDefense,
    Int32 PhysicalEvade,
    Int32 MagicalDefense,
    Int32 MagicalEvade,
    GrowthBonuses GrowthPerLevel,
    IReadOnlyList<AbilityRef> TaughtAbilities,
    Byte WeaponElement,
    Byte BonusElements,
    Byte GuardElements,
    Byte AbsorbElements,
    Byte HalfElements,
    Byte WeakElements)
{
    public static EquipmentKit Resolve(DefaultEquipmentSet set,
        IReadOnlyDictionary<Int32, Item> items,
        IReadOnlyDictionary<Int32, Weapon> weapons,
        IReadOnlyDictionary<Int32, Armor> armors,
        IReadOnlyDictionary<Int32, ItemStats> growthStats)
    {
        Int32[] slots = [set.Weapon, set.Head, set.Wrist, set.Armor, set.Accessory];
        Int32 power = 0, pd = 0, pe = 0, md = 0, me = 0;
        Byte weaponElement = 0, boost = 0, guard = 0, absorb = 0, half = 0, weak = 0;
        List<AbilityRef> taught = [];

        foreach (var id in slots)
        {
            if (id < 0 || !items.TryGetValue(id, out var item))
                continue;
            taught.AddRange(item.TaughtAbilities);
            if (growthStats.TryGetValue(item.BonusID, out var stats))
            {
                boost |= stats.AttackElement;
                guard |= stats.GuardElement;
                absorb |= stats.AbsorbElement;
                half |= stats.HalfElement;
                weak |= stats.WeakElement;
            }
            if (item.WeaponID >= 0 && weapons.TryGetValue(item.WeaponID, out var w))
            {
                power = w.Power;
                weaponElement = w.Elements;
            }
            if (item.ArmorID >= 0 && armors.TryGetValue(item.ArmorID, out var a))
            {
                pd += a.PhysicalDefense;
                pe += a.PhysicalEvade;
                md += a.MagicalDefense;
                me += a.MagicalEvade;
            }
        }

        var growth = Equipment.GrowthPerLevel(slots.Where(id => id >= 0), items, growthStats);
        return new EquipmentKit(power, pd, pe, md, me, growth, taught,
            weaponElement, boost, guard, absorb, half, weak);
    }
}

