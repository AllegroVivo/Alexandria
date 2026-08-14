namespace Alexandria.Formats.Scenes.BattleScene;

public sealed record BattleScenePut
{
    public Byte TypeNumber;
    public Byte Flags;
    public Byte Pease;
    public Byte Pad;

    public Int16 XPosition;
    public Int16 YPosition;
    public Int16 ZPosition;
    public Int16 Rotation;
}

