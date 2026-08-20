using Alexandria.Formats.Scripts;

namespace Alexandria.Battle;

public sealed class WorldState
{
    public EbState Scripts { get; } = new();

    public KillCounts Kills { get; } = new();

    public Int32 Tonberries
    {
        get => Scripts.Global[192];
        set => Scripts.Global[192] = (Byte)Math.Clamp(value, 0, 255);
    }
}

