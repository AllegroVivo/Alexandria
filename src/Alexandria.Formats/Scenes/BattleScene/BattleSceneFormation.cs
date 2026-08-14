namespace Alexandria.Formats.Scenes.BattleScene;

public sealed record BattleSceneFormation(
    Byte Rate,
    Byte MonsterCount,
    Byte Camera,
    UInt32 AP,
    IReadOnlyList<BattleScenePut> Monsters)
{
    
    public static BattleSceneFormation Read(BinaryReader reader, BattleSceneHeader header)
    {
        var rate = reader.ReadByte();
        var monsterCount = reader.ReadByte();
        var camera = reader.ReadByte();
        reader.ReadByte();  // Pad0
        var ap = reader.ReadUInt32();

        List<BattleScenePut> monsters = [];
        for (var i = 0; i < 4; i++)
        {
            var placement = new BattleScenePut
            {
                TypeNumber = reader.ReadByte(),
                Flags = reader.ReadByte(),
                Pease = reader.ReadByte()
            };
            reader.ReadByte();  // Pad
            placement.XPosition = reader.ReadInt16();
            placement.YPosition = reader.ReadInt16();
            placement.ZPosition = reader.ReadInt16();
            placement.Rotation = reader.ReadInt16();
            monsters.Add(placement);
        }

        return new BattleSceneFormation(rate, monsterCount, camera, ap, monsters);
    }
}

