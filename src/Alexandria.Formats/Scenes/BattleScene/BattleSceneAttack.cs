namespace Alexandria.Formats.Scenes.BattleScene;

public sealed record BattleSceneAttack(
    Byte Target,        // low 4 bits of the packed targeting byte (TargetType)
    Byte ScriptID,
    Byte Power,
    Byte Elements,
    Byte Rate,
    Byte Category,
    Byte AddStatusNo,
    Byte MP,
    Byte Type)
{
    public static BattleSceneAttack Read(BinaryReader reader)
    {
        var packedTargeting = reader.ReadByte();  // Target(4) | DefaultAlly(1) | DisplayStats(3)
        reader.ReadUInt16();                           // packed VfxIndex(9) + unused — presentation
        reader.ReadByte();                             // packed ForDead/DefaultCamera/DefaultOnDead flags
        var scriptID = reader.ReadByte();
        var power = reader.ReadByte();
        var elements = reader.ReadByte();
        var rate = reader.ReadByte();
        var category = reader.ReadByte();
        var addStatusNo = reader.ReadByte();
        var mp = reader.ReadByte();
        var type = reader.ReadByte();
        reader.ReadUInt16();                           // Vfx2 — presentation
        reader.ReadUInt16();                           // pad

        return new BattleSceneAttack((Byte)(packedTargeting & 0x0F),
            scriptID, power, elements, rate, category, addStatusNo, mp, type);
    }
}

