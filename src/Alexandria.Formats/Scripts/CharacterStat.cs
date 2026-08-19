namespace Alexandria.Formats.Scripts;

/// <summary>The character variables a script can read or write (mirror:
/// btl_scrp.GetCharacterData / SetCharacterData).</summary>
public enum CharacterStat
{
    Appear = 32,             // write-only: renderers on + btl_sys.AddCharacter
    Disappear = 33,          // write-only: renderers off + DelCharacter + kill commands
    KillCommands = 34,       // write-only: unit.KillStandardCommands()
    MaxHP = 35,
    HP = 36,
    MaxMP = 37,
    MP = 38,
    MaxATB = 39,             // read-only
    ATB = 40,
    Level = 41,              // read-only
    StatusImmuneB = 42, StatusImmuneA = 43,   // stat.invalid, high 24 / low 24
    StatusAutoB = 44, StatusAutoA = 45,       // stat.permanent
    StatusCurrentB = 46, StatusCurrentA = 47, // stat.cur
    ElementImmune = 48, ElementAbsorb = 49, ElementHalf = 50, ElementWeak = 51,
    Targetable = 52,
    Vanished = 53,           // bi.disappear — a priority byte, not a bool
    Shadow = 54,             // write-only
    ModelScale = 55,         // write-only: geo.geoScaleSet (the tag-7 Mini handlers)
    ModelScaleReset = 56,    // write-only
    Model = 57,              // read-only: dms_geo_id
    ModelVisible = 58,       // read-only: mesh_current bitmask
    HideMesh = 59, ShowMesh = 60,             // write-only, maintain mesh_current
    TextureAnimPlay = 61, TextureAnimStop = 62, TextureAnimOnce = 63,  // write-only
    Row = 64,                // read-only: enemies are hardcoded row 2
    Column = 65,             // read-only: bi.line_no
    ModelType = 66,          // read-only: player serial_no
    Category = 67,           // read-only: player category bitmask
    Class = 68,              // read-only: enemy type category (SB2 Category)
    StandAnimation = 69,     // bi.def_idle — raised to 1 only by a sequence opcode
    EnemyID = 70,            // read-only: bi.slot_no, index within the formation
    SetBonus = 71,           // write-only trigger; the value is ignored
    Strength = 72, Magic = 73,
    Defence = 74, Evade = 75, MagicalDefence = 76, MagicalEvade = 77,
    SummonEiko = 78          // write-only: force-fill ATB and queue TerraHoming
}

