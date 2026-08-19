using Alexandria.Rules;

namespace Alexandria.Battle;

/// <summary>One entry in an enemy's move roster, resolved from scene AA_DATA:
/// ScriptId picks the formula (8 physical, 9 magic+rider, 11 pure status).</summary>
public sealed record EnemyAttack(
    String Name,
    AbilityScript Script,
    Int32 Power,
    Element Elements,
    Int32 Rate,
    UInt32 StatusMask,
    Int32 MP,
    Int32 ID = -1,
    Int32 TargetType = 0,
    Boolean ForDead = false);

