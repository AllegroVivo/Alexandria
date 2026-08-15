namespace Alexandria.Rules;

public sealed record BattleAction(
    String Name,
    Int32 ID,
    Int32 ScriptID,
    Int32 Power,
    Byte Elements,
    Int32 Rate,
    Byte Category,
    Int32 StatusIndex,
    Int32 MP)
{
    public static IReadOnlyDictionary<Int32, BattleAction> Load(String path)
        => MemoriaCSV.ReadRows(path)
            .Select(f => new BattleAction(
                f[0],
                Int32.Parse(f[1]),
                Int32.Parse(f[10]),
                Int32.Parse(f[11]),
                Byte.Parse(f[12]),
                Int32.Parse(f[13]),
                Byte.Parse(f[14]),
                Int32.Parse(f[15]),
                Int32.Parse(f[16])))
            .ToDictionary(s => s.ID);
}

