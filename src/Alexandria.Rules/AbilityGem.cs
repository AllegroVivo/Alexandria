namespace Alexandria.Rules;

public sealed record AbilityGem(
    String Name,
    Int32 ID,
    Int32 Gems,
    IReadOnlyList<Int32> BoostedVersions)
{
    public static IReadOnlyDictionary<Int32, AbilityGem> Load(String path)
        => MemoriaCsv.ReadRows(path)
            .Select(f => new AbilityGem(
                f[0],
                Int32.Parse(f[1]),
                Int32.Parse(f[2]),
                MemoriaCsv.ParseIntArray(f[3])))
            .ToDictionary(g => g.ID);
}
