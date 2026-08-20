using Alexandria.Rules;

namespace Alexandria.Battle;

/// <summary>One resolved ability: scene AA_DATA for an enemy's roster entry, or an
/// Actions.csv row for a party member's spell.</summary>
public sealed record BattleAbility(
    String Name,
    AbilityScript Script,
    Int32 Power,
    Element Elements,
    Int32 Rate,
    BattleStatus StatusMask,
    Int32 MP,
    Int32 ID = -1,
    Int32 TargetType = 0,
    Boolean ForDead = false,
    Int32? SetsStandAnimation = null);
    