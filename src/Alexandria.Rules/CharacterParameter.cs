namespace Alexandria.Rules;

public sealed record CharacterParameter(Int32 ID, CharacterCategory Category)
{
    public static IReadOnlyDictionary<Int32, CharacterParameter> Load(String path)
        => MemoriaCSV.ReadRows(path)
            .Select(f => new CharacterParameter(
                Int32.Parse(f[0]),
                (CharacterCategory)Byte.Parse(f[3])))
            .ToDictionary(p => p.ID);
}

