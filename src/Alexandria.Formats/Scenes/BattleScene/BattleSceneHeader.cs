namespace Alexandria.Formats.Scenes.BattleScene;

public sealed record BattleSceneHeader(
    Byte Version,
    Byte FormationCount,
    Byte EnemyTypeCount,
    Byte AttackCount,
    UInt16 Flags)
{
    public static BattleSceneHeader Read(BinaryReader reader)
    {
        var version = reader.ReadByte();
        if (version < 7)
            throw new FormatException("Invalid Battle Scene Header");
        var formationCount = reader.ReadByte();
        var enemyTypeCount = reader.ReadByte();
        var attackCount = reader.ReadByte();
        var flags = reader.ReadUInt16();
        reader.ReadInt16();  // Pad 1 - unused
        return new BattleSceneHeader(version, formationCount, enemyTypeCount, attackCount, flags);
    }
}
