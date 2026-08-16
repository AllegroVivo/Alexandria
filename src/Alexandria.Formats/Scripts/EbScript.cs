using System.Drawing;

namespace Alexandria.Formats.Scripts;

/// <summary>
/// A compiled .eb event-script container (mirror: EventEngine.SetupCodeParam/GetIP +
/// EventEngineUtils.BinaryScript). Layout: 'EV' magic, entry count at byte 3, a 128-byte
/// prefix (44-byte header + 84-byte PSX filename fossil), then an 8-byte-per-entry
/// table whose offsets are relative to byte 128.
/// </summary>
public sealed record EbScript(Byte EntryCount, IReadOnlyList<EbEntry> Entries)
{
    public static EbScript Read(BinaryReader reader)
    {
        var magic0 = reader.ReadByte();
        var magic1 = reader.ReadByte();
        if(magic0!= 'E'||magic1 != 'V')
            throw new InvalidDataException($"Not an event binary: magic {magic0:X2} {magic1:X2}");

        reader.ReadByte();      // unknown header byte
        var entryCount = reader.ReadByte();

        reader.BaseStream.Seek(128, SeekOrigin.Begin);
        var table = new (UInt16 Offset, UInt16 Size, Byte VarCount, Byte Flags) [entryCount];
        for (var i = 0; i < entryCount; i++)
        {
            var ofs = reader.ReadUInt16();
            var size = reader.ReadUInt16();
            var varn = reader.ReadByte();
            var flags = reader.ReadByte();
            reader.ReadUInt16();        // pad
            table[i] = (ofs, size, varn, flags);
        }

        var entries = new List<EbEntry>(entryCount);
        for (var i = 0; i < entryCount; i++)
        {
            reader.BaseStream.Seek(128 + table[i].Offset, SeekOrigin.Begin);
            entries.Add(EbEntry.Read(reader, i, table[i].Size, table[i].VarCount, table[i].Flags));
        }

        return new EbScript(entryCount, entries);
    }
}

