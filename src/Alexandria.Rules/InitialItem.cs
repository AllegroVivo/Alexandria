namespace Alexandria.Rules;

/// <summary>A row of Items\InitialItems.csv — what the party carries
/// at a NEW GAME. </summary>
public sealed record InitialItem(Int32 ID, Int32 Count)
{
    public static IReadOnlyList<InitialItem> Load(String path) =>
    [
        .. MemoriaCSV.ReadRows(path)
            .Select(f => new InitialItem(
                Int32.Parse(f[0]),
                Int32.Parse(f[1])))
    ];
}

