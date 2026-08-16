namespace Alexandria.Formats.Scripts;

/// <summary>
/// One script object. Index is the engine's sid - in battle, btl_scrp.FindBattleUnit
/// maps it to a combatant. Blob layout: type byte, function count, then a table of
/// {u16 tag, u16 offset} pairs with offsets relative to byte 2 of the blob.
/// </summary>
public sealed record EbEntry(
    Int32 Index,
    Byte Type,
    Byte VarCount,
    Byte Flags,
    IReadOnlyList<EbFunction> Functions)
{
    public static EbEntry Read(BinaryReader reader, Int32 index,
        UInt16 size, Byte varCount, Byte flags)
    {
        var blob = reader.ReadBytes(size);
        if (blob.Length < 2)
            return new EbEntry(index, 0, varCount, flags, []);
        
        var type = blob[0];
        var funcCount = blob[1];

        var tags = new UInt16[funcCount];
        var offsets = new UInt16[funcCount];
        for (var i = 0; i < funcCount; i++)
        {
            tags[i] = (UInt16)(blob[2 + i * 4] | blob[3 + i * 4] << 8);
            offsets[i] = (UInt16)(blob[4 + i * 4] | blob[5 + i * 4] << 8);
        }
        
        // bodies are boundless in the format: function i ends where i+1 begins,
        // the last at the entry's end (mirror: BinaryScript's funcPos[i+1] assumption)
        var functions = new List<EbFunction>(funcCount);
        for (var i = 0; i < funcCount; i++)
        {
            var start = 2 + offsets[i];
            var end = i + 1 < funcCount ? 2 + offsets[i + 1] : size;
            functions.Add(new EbFunction(tags[i], start, blob[start..end]));
        }
        
        return new EbEntry(index, type, varCount, flags, functions);
    }
}

