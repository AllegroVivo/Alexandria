namespace Alexandria.Cards;

/// <summary>Everything one placement did (mirror: BattleResult), kept whole so a runner can
/// narrate it move by move.</summary>
public sealed record PlayOutcome(
    IReadOnlyList<PlayedCard> Flipped,
    IReadOnlyList<CardBattle> Battles,
    Boolean AttackerTaken);

