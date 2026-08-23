namespace Alexandria.Rules;

public sealed record CharacterParameter(Int32 ID, Int32 DefaultRow, CharacterCategory Category, Int32 DefaultCommandSet)
{
    public static IReadOnlyDictionary<Int32, CharacterParameter> Load(String path)
        => MemoriaCSV.ReadRows(path)
            .Select(f => new CharacterParameter(
                Int32.Parse(f[0]),
                Int32.Parse(f[1]),
                (CharacterCategory)Byte.Parse(f[3]),
                Int32.Parse(f[4])))
            .ToDictionary(p => p.ID);
}

