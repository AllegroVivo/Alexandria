namespace Alexandria.Rules;

public sealed record SynthesisRecipe(
    String Name,
    Int32 ID,
    IReadOnlyList<Int32> ShopIDs,
    UInt32 Price,
    Int32 ResultItemID,
    IReadOnlyList<Int32> IngredientItemIDs)
{
    public static IReadOnlyList<SynthesisRecipe> Load(String path) =>
    [
        .. MemoriaCsv.ReadRows(path)
            .Select(f => new SynthesisRecipe(
                f[0],
                Int32.Parse(f[1]),
                MemoriaCsv.ParseIntArray(f[2]),
                UInt32.Parse(f[3]),
                Int32.Parse(f[4]),
                MemoriaCsv.ParseIntArray(f[5])))
    ];
}
