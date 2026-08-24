namespace Alexandria.Cards;

/// <summary>The eight directions a card can point, as a bitmask.</summary>
[Flags]
public enum CardArrows : Byte
{
    None      = 0,
    Up        = 1 << 0,
    UpRight   = 1 << 1,
    Right     = 1 << 2,
    DownRight = 1 << 3,
    Down      = 1 << 4,
    DownLeft  = 1 << 5,
    Left      = 1 << 6,
    UpLeft    = 1 << 7
}

