namespace Alexandria.Rules;

/// <summary>
/// Simulates leveling from 1 to a target level, accumulating growth bonus
/// points the way the game does (mirror: ff9level.cs lvup branches).
/// Faithfully includes the "capa bug": the flat per-level bonuses were meant
/// to be conditional on spent magic stones, but a reset in ff9play makes them
/// unconditional - canon since 1999.
/// </summary>
public static class GrowthSimulator
{
    public static GrowthBonuses LevelUpTo(Int32 targetLevel, GrowthBonuses start)
    {
        var bonus = start;
        for (var level = 2; level <= targetLevel; level++)
        {
            bonus = bonus with
            {
                Strength    = (UInt16)(bonus.Strength + 3),
                Magic       = (UInt16)(bonus.Magic + 3),
                Spirit      = (UInt16)(bonus.Spirit + 1),
                MagicStones = (UInt16)(bonus.MagicStones + 5)
            };
        }
        return bonus;
    }

    public static GrowthBonuses LevelUpTo(
        Int32 targetLevel, GrowthBonuses start, GrowthBonuses equipPerLevel)
    {
        var bonus = start;
        for (var level = 2; level <= targetLevel; level++)
        {
            bonus = bonus with
            {
                Speed = (UInt16)(bonus.Speed + equipPerLevel.Speed),
                Strength = (UInt16)(bonus.Strength + 3 + equipPerLevel.Strength),
                Magic = (UInt16)(bonus.Magic + 3 + equipPerLevel.Magic),
                Spirit = (UInt16)(bonus.Spirit + 1 + equipPerLevel.Spirit),
                MagicStones = (UInt16)(bonus.MagicStones + 5)
            };
        }
        return bonus;
    }
}

