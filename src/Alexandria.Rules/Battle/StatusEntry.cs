namespace Alexandria.Rules;

/// <summary>One row of StatusData.csv: Id is the status BIT INDEX (the file's
/// #! UnshiftStatuses directive), OprCount drives periodic effects, ContiCount
/// drives duration (0 = lasts until cured or battle end — Stop, Petrify, Defend).</summary>
public sealed record StatusEntry(
    String Name,
    Int32 Bit,
    Int32 OprCount,
    Int32 ContiCount,
    UInt32 ClearOnApply,
    UInt32 ImmunityProvided)
{
    public static IReadOnlyDictionary<Int32, StatusEntry> Load(String path)
        => MemoriaCSV.ReadRows(path)
            .Select(f => new StatusEntry(
                f[0],
                Int32.Parse(f[1]),
                Int32.Parse(f[3]),
                Int32.Parse(f[4]),
                StatusSet.ParseMask(f[5]),
                StatusSet.ParseMask(f[6])))
            .ToDictionary(s => s.Bit);
}

