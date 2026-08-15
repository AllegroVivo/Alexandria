using Alexandria.Rules;

namespace Alexandria.Rules.Tests;

public class LoaderTests
{
    private const String DataRoot = @"C:\Dev\Memoria\FF9Data\Data";

    [Fact]
    public void Zidane_base_stats_load_correctly()
    {
        var stats = CharacterBaseStats.Load(Path.Combine(DataRoot, @"Characters\BaseStats.csv"));
        var zidane = stats.First(c => c.Name == "Zidane");
        Assert.Equal(23, zidane.Dexterity);
        Assert.Equal(21, zidane.Strength);
        Assert.Equal(18, zidane.Magic);
        Assert.Equal(23, zidane.Willpower);
        Assert.Equal(18u, zidane.Gems);
    }

    [Fact]
    public void Leveling_table_has_99_levels_with_rising_experience()
    {
        var table = LevelingTable.Load(Path.Combine(DataRoot, @"Characters\Leveling.csv"));
        Assert.Equal(0u, table.ForLevel(1).ExperienceToReach);
        for (var level = 2; level <= LevelingTable.MaxLevel; level++)
            Assert.True(table.ForLevel(level).ExperienceToReach > table.ForLevel(level - 1).ExperienceToReach);
    }
}
