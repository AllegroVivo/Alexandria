namespace Alexandria.Cards;

/// <summary>A card actually in play.</summary>
public sealed class PlayedCard(TetraCard card, CardSide side)
{
    public TetraCard Card { get; } = card;
    public CardSide Side { get; set; } = side;
    public CardArrows Arrows => Card.Arrows;

    public override String ToString() => $"{Card.Face} {Card.Name} ({Side})";
}

