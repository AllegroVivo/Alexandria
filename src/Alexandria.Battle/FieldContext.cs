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
}

