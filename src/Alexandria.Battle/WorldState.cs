using Alexandria.Formats.Scripts;

namespace Alexandria.Battle;

public sealed class WorldState
{
    public EbState Scripts { get; } = new();
    
    public Int32 Frogs { get; set; }
    public KillCounts Kills { get; } = new();
    
    
    public Int32 Gil
    {
        get;
        set => field = Math.Clamp(value, 0, 9_999_999);
    }

    public Int32 Thefts
    {
        get;
        set => field = Math.Clamp(value, 0, 9999);
    }

    public Int32 Tonberries
    {
        get => Scripts.Global[192];
        set => Scripts.Global[192] = (Byte)Math.Clamp(value, 0, 255);
    }
}

