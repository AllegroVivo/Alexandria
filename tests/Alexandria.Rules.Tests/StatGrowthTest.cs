using Alexandria.Rules;

namespace Alexandria.Rules.Tests;

public class StatGrowthTests
{
    private const String DataRoot = @"C:\Dev\Memoria\FF9Data\Data";

    private static CharacterBaseStats Zidane()
        => CharacterBaseStats.Load(Path.Combine(DataRoot, @"Characters\BaseStats.csv"))
            .First(c => c.Name == "Zidane");

    [Fact]
    public void Level_1_Zidane_has_his_famous_starting_numbers()
    {
        var z = Zidane();
        var table = LevelingTable.Load(Path.Combine(DataRoot, @"Characters\Leveling.csv"));

        Assert.Equal(23, StatGrowth.Speed(z, 1, GrowthBonuses.None));
        Assert.Equal(21, StatGrowth.Strength(z, 1, GrowthBonuses.None));
        Assert.Equal(18, StatGrowth.Magic(z, 1, GrowthBonuses.None));
        Assert.Equal(23, StatGrowth.Spirit(z, 1, GrowthBonuses.None));
        Assert.Equal(105, StatGrowth.MaxHP(table, 1, StatGrowth.Strength(z, 1, GrowthBonuses.None)));
        Assert.Equal(36, StatGrowth.MaxMP(table, 1, StatGrowth.Magic(z, 1, GrowthBonuses.None)));
    }

    [Fact]
    public void Thirty_two_bonus_points_buy_one_stat_point()
    {
        var z = Zidane();
        Assert.Equal(21, StatGrowth.Strength(z, 1, GrowthBonuses.None with { Strength = 31 }));
        Assert.Equal(22, StatGrowth.Strength(z, 1, GrowthBonuses.None with { Strength = 32 }));
    }
    
    [Fact]
    public void Naked_Zidane_at_99_grows_by_the_canon_bug()
    {
        // 98 level-ups: str/mgc +3 each, spirit +1, stones +5, speed nothing
        var bonus = GrowthSimulator.LevelUpTo(99, GrowthBonuses.None);
        Assert.Equal(294, bonus.Strength);
        Assert.Equal(294, bonus.Magic);
        Assert.Equal(98, bonus.Spirit);
        Assert.Equal(490, bonus.MagicStones);

        var z = Zidane();
        var table = LevelingTable.Load(Path.Combine(DataRoot, @"Characters\Leveling.csv"));
        var sheet = StatGrowth.AtLevel(z, table, 99, bonus);

        Assert.Equal(32, sheet.Speed);       // 23 + 99/10(=9)  + 0
        Assert.Equal(59, sheet.Strength);    // 21 + 29        + 294>>5(=9)
        Assert.Equal(56, sheet.Magic);       // 18 + 29        + 9
        Assert.Equal(40, sheet.Spirit);      // 23 + 14        + 98>>5(=3)
        Assert.Equal(72, sheet.MagicStones); // 18 + 39        + 490>>5(=15)
    }
}
