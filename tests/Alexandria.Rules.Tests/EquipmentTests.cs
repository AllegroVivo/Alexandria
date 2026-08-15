using Alexandria.Rules;

namespace Alexandria.Rules.Tests;

public class EquipmentTests
{
    private const String DataRoot = @"C:\Dev\Memoria\FF9Data\Data";

    private static IReadOnlyDictionary<Int32, ItemStats> Stats()
        => ItemStats.Load(Path.Combine(DataRoot, @"Items\Stats.csv"));

    [Fact]
    public void Known_bonus_rows_load_correctly()
    {
        var stats = Stats();
        Assert.Equal("Thief Gloves", stats[11].DisplayName);
        Assert.Equal(1, stats[11].Dexterity);
        Assert.Equal(2, stats[6].Magic);          // Magic Armlet
        Assert.Equal(1, stats[2].Willpower);           // Leather Wrist
        Assert.Equal(0, stats[0].Strength);       // Empty
    }

    [Fact]
    public void A_lifetime_in_Thief_Gloves_makes_Zidane_faster()
    {
        // Dex +1 per level-up × 98 levels = 98 points; 98 >> 5 = 3 extra Speed.
        var perLevel = GrowthBonuses.None with { Speed = 1 };
        var bonus = GrowthSimulator.LevelUpTo(99, GrowthBonuses.None, perLevel);
        Assert.Equal(98, bonus.Speed);

        var z = CharacterBaseStats.Load(Path.Combine(DataRoot, @"Characters\BaseStats.csv"))
            .First(c => c.Name == "Zidane");
        Assert.Equal(35, StatGrowth.Speed(z, 99, bonus));   // 23 + 9 + 3, vs 32 naked
    }
}
