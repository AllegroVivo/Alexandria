namespace Alexandria.Cards;

public static class CardDeal
{
    /// <summary>A fresh copy of a pool card (mirror: CardPool.CreateQuadMistCard). Each stat
    /// lands between HALF the pool maximum and the maximum, and the arrows are rolled.</summary>
    public static TetraCard Create(TetraCard max, Random rng) => max with
    {
        Attack = Half(max.Attack, rng),
        PhysicalDefense = Half(max.PhysicalDefense, rng),
        MagicalDefense = Half(max.MagicalDefense, rng),
        Type = rng.Next(0, 127) == 0 ? CardType.Flexible : max.Type,
        Arrows = Arrows.Roll(rng)
    };

    private static Byte Half(Byte max, Random rng) => (Byte)(max / 2 + (max / 2 > 0 ? rng.Next(0, max / 2) : 0));

    public static List<TetraCard> Deal(IReadOnlyList<TetraCard> pool, Int32 count, Random rng) 
        => [.. Enumerable.Range(0, count).Select(_ => Create(pool[rng.Next(0, pool.Count)], rng))];
}

