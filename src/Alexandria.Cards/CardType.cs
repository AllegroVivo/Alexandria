namespace Alexandria.Cards;

/// <summary>What a card's attack is rolled against (mirror: QuadMistCard.Type).
/// - PHYSICAL against physical defense
/// - MAGIC against magical
/// - FLEXIBLE takes whichever is weaker
/// - ASSAULT attacks with the card's BEST stat against the defender's worst</summary>
public enum CardType
{
    Physical,
    Magic,
    Flexible,
    Assault
}

public static class CardTypes
{
    extension(CardType type)
    {
        public Char Letter => type switch
        {
            CardType.Magic => 'm',
            CardType.Flexible => 'x',
            CardType.Assault => 'a',
            _ => 'p'
        };
    }
}

