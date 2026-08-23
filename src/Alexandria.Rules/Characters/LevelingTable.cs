namespace Alexandria.Rules;

public sealed record LevelBonus(UInt32 ExperienceToReach, UInt16 BonusHP, UInt16 BonusMP);

public class LevelingTable
{
    public const Int32 MaxLevel = 99;

    private readonly LevelBonus[] _levels;  // index 0 == level 1

    private LevelingTable(LevelBonus[] levels) => _levels = levels;

    public LevelBonus ForLevel(Int32 level) => _levels[level - 1];

    public static LevelingTable Load(String path)
    {
        var levels = MemoriaCSV.ReadRows(path)
            .Select(f => new LevelBonus(
                UInt32.Parse(f[0]),
                UInt16.Parse(f[1]),
                UInt16.Parse(f[2])))
            .ToArray();
        return new LevelingTable(levels);
    }
}

