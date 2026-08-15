namespace Alexandria.Battle;

/// <summary>One entry in an enemy's move roster, resolved from scene AA_DATA:
/// ScriptId picks the formula (8 physical, 9 magic+rider, 11 pure status).</summary>
public sealed record EnemyAttack(
    String Name,
    Int32 ScriptID,
    Int32 Power,
    Int32 Rate,
    UInt32 StatusMask,
    Int32 MP);

