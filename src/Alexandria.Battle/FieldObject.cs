namespace Alexandria.Battle;

/// <summary>Everything a script can observe about a field object</summary>
public sealed class FieldObject
{
    public Int32 X { get; set; }
    public Int32 Y { get; set; }
    public Int32 Z { get; set; }
    
    public Int32 Angle { get; set; }
    
    public Int32 Flags { get; set; }
    public Int32 Level { get; set; }
    public Int32 AnimFrame { get; set; }
}

