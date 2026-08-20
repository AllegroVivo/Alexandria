using Alexandria.Formats;
using Alexandria.Rules;

namespace Alexandria.Rules.Tests;

public class EquipmentKitTests
{
    private static readonly RulesDB Db = RulesDB.Load(FF9Data.Data);

    private static EquipmentKit KitFor(String name)
        => EquipmentKit.Resolve(Db.DefaultEquipment.First(e => e.Name == name),
            Db.Items, Db.Weapons, Db.Armors, Db.GrowthStats);

    [Fact]
    public void Zidane_starts_with_a_dagger_and_leathers()
    {
        // Dagger 12 | Wrist 0/5/0/3 + Leather Hat 0/0/6/0 + Leather Shirt 6/0/0/0
        var kit = KitFor("Zidane");
        Assert.Equal(12, kit.WeaponPower);
        Assert.Equal(6, kit.PhysicalDefense);
        Assert.Equal(5, kit.PhysicalEvade);
        Assert.Equal(6, kit.MagicalDefense);
        Assert.Equal(3, kit.MagicalEvade);
    }

    [Fact]
    public void Steiner_starts_in_bronze()
    {
        // Broadsword 12 | Bronze Helm 0/0/6/0 + Bronze Armour 9/0/0/0
        var kit = KitFor("Steiner");
        Assert.Equal(12, kit.WeaponPower);
        Assert.Equal(9, kit.PhysicalDefense);
        Assert.Equal(0, kit.PhysicalEvade);
        Assert.Equal(6, kit.MagicalDefense);
        Assert.Equal(0, kit.MagicalEvade);
    }
}
