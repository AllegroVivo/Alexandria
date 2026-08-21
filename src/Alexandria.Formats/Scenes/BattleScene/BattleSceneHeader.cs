namespace Alexandria.Formats.Scenes.BattleScene;

public sealed record BattleSceneHeader(
    Byte Version,
    Byte FormationCount,
    Byte EnemyTypeCount,
    Byte AttackCount,
    UInt16 Flags)
{
    // mirror: BTL_SCENE.SetupSceneInfo
    public Boolean SpecialStart => (Flags & 1) != 0;
    public Boolean BackAttack => (Flags & 2) != 0;
    public Boolean NoGameOver => (Flags & 4) != 0;
    public Boolean NoExp => (Flags & 8) != 0;
    public Boolean WinPose => (Flags & 16) == 0;
    public Boolean Runaway => (Flags & 32) == 0;
    public Boolean NoNeighboring => (Flags & 64) != 0;
    public Boolean NoMagical => (Flags & 128) != 0;
    public Boolean ReverseAttack => (Flags & 256) != 0;
    public Boolean FixedCamera1 => (Flags & 512) != 0;
    public Boolean FixedCamera2 => (Flags & 1024) != 0;
    public Boolean AfterEvent => (Flags & 2048) != 0;
    
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
