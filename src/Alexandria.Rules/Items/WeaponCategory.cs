namespace Alexandria.Rules;

/// <summary>The reach and handling bits on a weapon (mirror: Param.WPN_CATEGORY_*).</summary>
[Flags]
public enum WeaponCategory
{
    None       = 0,
    ShortRange = 1,
    LongRange  = 2,
    Throw      = 4,
    OffsetDim  = 8,
    Open2      = 16,
    Open3      = 32,
    Open4      = 64,
    Default    = 128
}

