namespace Alexandria.Rules;

/// <summary>
/// FF9's vanilla stat-growth formulas (mirror: ff9level.cs). Accumulated bonus
/// points buy +1 stat per 32 points (>> 5); all math is deliberately integer.
/// </summary>
public static class StatGrowth
{
    public const Int32 SpeedCap = 50;
    public const Int32 StrengthCap = 99;
    public const Int32 MagicCap = 99;
    public const Int32 SpiritCap = 50;

    public static Int32 Speed(CharacterBaseStats b, Int32 level, GrowthBonuses bonus)
        => Math.Min(SpeedCap, b.Dexterity + level / 10 + (bonus.Speed >> 5));

    public static Int32 Strength(CharacterBaseStats b, Int32 level, GrowthBonuses bonus)
        => Math.Min(StrengthCap, b.Strength + level * 3 / 10 + (bonus.Strength >> 5));

    public static Int32 Magic(CharacterBaseStats b, Int32 level, GrowthBonuses bonus)
        => Math.Min(MagicCap, b.Magic + level * 3 / 10 + (bonus.Magic >> 5));

    public static Int32 Spirit(CharacterBaseStats b, Int32 level, GrowthBonuses bonus)
        => Math.Min(SpiritCap, b.Willpower + level * 3 / 20 + (bonus.Spirit >> 5));

    public static Int32 MagicStones(CharacterBaseStats b, Int32 level, GrowthBonuses bonus)
        => (Int32)b.Gems + level * 4 / 10 + (bonus.MagicStones >> 5);

    public static Int32 MaxHP(LevelingTable table, Int32 level, Int32 strength, Int32 limit = 9999)
        => Math.Min(limit, table.ForLevel(level).BonusHP * strength / 50);

    public static Int32 MaxMP(LevelingTable table, Int32 level, Int32 magic, Int32 limit = 9999)
        => Math.Min(limit, table.ForLevel(level).BonusMP * magic / 100);
    
    public static StatBlock AtLevel(
        CharacterBaseStats b, LevelingTable table, Int32 level, GrowthBonuses bonus)
    {
        var str = Strength(b, level, bonus);
        var mgc = Magic(b, level, bonus);
        return new StatBlock(
            level,
            Speed(b, level, bonus),
            str, mgc,
            Spirit(b, level, bonus),
            MagicStones(b, level, bonus),
            MaxHP(table, level, str),
            MaxMP(table, level, mgc));
    }
}

public sealed record GrowthBonuses(
    UInt16 Speed,
    UInt16 Strength,
    UInt16 Magic,
    UInt16 Spirit,
    UInt16 MagicStones)
{
    public static readonly GrowthBonuses None = new(0, 0, 0, 0, 0);
}

public sealed record StatBlock(
    Int32 Level,
    Int32 Speed,
    Int32 Strength,
    Int32 Magic,
    Int32 Spirit,
    Int32 MagicStones,
    Int32 MaxHP,
    Int32 MaxMP);