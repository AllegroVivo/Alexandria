namespace Alexandria.Rules;

public sealed record BattleAction(
    String Name,
    Int32 ID,
    AbilityScript Script,
    Int32 Power,
    Byte Elements,
    Int32 Rate,
    Byte Category,
    Int32 StatusIndex,
    Int32 MP,
    Int32 TargetType = 0,
    Boolean DefaultAlly = false,
    Boolean ForDead = false)
{
    public static IReadOnlyDictionary<Int32, BattleAction> Load(String path)
        => MemoriaCSV.ReadRows(path)
            .Select(f => new BattleAction(
                f[0],
                Int32.Parse(f[1]),
                (AbilityScript)Int32.Parse(f[10]),
                Int32.Parse(f[11]),
                Byte.Parse(f[12]),
                Int32.Parse(f[13]),
                Byte.Parse(f[14]),
                Int32.Parse(f[15]),
                Int32.Parse(f[16]),
                Int32.Parse(f[3][(f[3].IndexOf('(') + 1)..^1]),
                f[4] == "1",
                f[5] == "1"))
            .ToDictionary(s => s.ID);
}

