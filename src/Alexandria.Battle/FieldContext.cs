namespace Alexandria.Battle;

public sealed class FieldContext
{
    public Int32 UserControl { get; set; }
    public Int32 EncounterRatio { get; set; }
    public Int32 TwistArrow { get; set; }
    public Int32 TwistStick { get; set; }
    public Int32 IdleTimer { get; set; }
    public Int32 DashInhibited { get; set; }
    public Int32 ControlUID { get; set; }
    public Int32[] PartyUID { get; } = new Int32[4];
    public HashSet<Int32> Party { get; } = [0, 1, 2, 3];
    public Int32 LastChoice { get; set; }
    public Boolean MenuEnabled { get; set; } = true;
}

