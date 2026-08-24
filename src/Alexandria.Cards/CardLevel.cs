namespace Alexandria.Cards;

public static class CardLevel
{
    public static TetraCard LevelUp(TetraCard card, TetraCard max, Random rng)
        => rng.Next(0, 3) switch
        {
            0 when card.Attack < max.Attack => card with { Attack = (Byte)(card.Attack + 1) },
            1 when card.PhysicalDefense < max.PhysicalDefense
                => card with { PhysicalDefense = (Byte)(card.PhysicalDefense + 1) },
            2 when card.MagicalDefense < max.MagicalDefense
                => card with { MagicalDefense = (Byte)(card.MagicalDefense + 1) },
            _ => card
        };

    public static TetraCard Promote(TetraCard card, Random rng) => card.Type switch
    {
        CardType.Physical or CardType.Magic when rng.Next(0, 64) == 0 => card with { Type = CardType.Flexible },
        CardType.Flexible when rng.Next(0, 128) == 0 => card with { Type = CardType.Assault },
        _ => card
    };
}

