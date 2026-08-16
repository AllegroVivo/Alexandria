namespace Alexandria.Battle;

public sealed record StatusTiming(
    Int32 OprCount,
    Int32 ContiCount,
    BattleStatus ClearOnApply,
    BattleStatus ImmunityProvided);

