namespace Alexandria.Formats.Scenes.BattleScene;

public sealed record BattleStats(
    Byte Speed,
    Byte Strength,
    Byte Magic,
    Byte Spirit,
    Byte trans,
    Byte cur_capa,
    Byte max_capa)
{
    public static BattleStats Read(BinaryReader reader)
    {
        var speed = reader.ReadByte();
        var strength = reader.ReadByte();
        var magic = reader.ReadByte();
        var spirit = reader.ReadByte();
        reader.ReadByte();  // pad
        var trans = reader.ReadByte();
        var cur_capa = reader.ReadByte();
        var max_capa = reader.ReadByte();
        
        return new BattleStats(
            speed, strength, magic, spirit, trans, cur_capa, max_capa);
    }
}

