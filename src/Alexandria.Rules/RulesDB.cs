namespace Alexandria.Rules;

public sealed record RulesDB(
    IReadOnlyList<CharacterBaseStats> BaseStats,
    LevelingTable Leveling,
    IReadOnlyDictionary<Int32, Item> Items,
    IReadOnlyDictionary<Int32, Weapon> Weapons,
    IReadOnlyDictionary<Int32, Armor> Armors,
    IReadOnlyDictionary<Int32, ItemStats> GrowthStats,
    IReadOnlyList<DefaultEquipmentSet> DefaultEquipment,
    IReadOnlyDictionary<Int32, BattleAction> Actions)
{
    public static RulesDB Load(String dataRoot) => new(
        CharacterBaseStats.Load(Path.Combine(dataRoot, @"Characters\BaseStats.csv")),
        LevelingTable.Load(Path.Combine(dataRoot, @"Characters\Leveling.csv")),
        Item.Load(Path.Combine(dataRoot, @"Items\Items.csv")),
        Weapon.Load(Path.Combine(dataRoot, @"Items\Weapons.csv")),
        Armor.Load(Path.Combine(dataRoot, @"Items\Armors.csv")),
        ItemStats.Load(Path.Combine(dataRoot, @"Items\Stats.csv")),
        DefaultEquipmentSet.Load(Path.Combine(dataRoot, @"Characters\DefaultEquipment.csv")),
        BattleAction.Load(Path.Combine(dataRoot, @"Battle\Actions.csv")));
}

