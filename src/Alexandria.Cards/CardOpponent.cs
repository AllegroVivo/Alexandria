namespace Alexandria.Cards;

/// <summary>The opponent's placement search (mirror: EnemyAI.SetCard). Scores every
/// (card, square) pair, then picks among the best with a deliberately uneven random
/// tie-break.</summary>
public sealed class CardOpponent(Int32 wise = 3)
{
    /// <summary>Below this roll the weights survive; above it, they are thrown away and the
    /// tie-break decides everything.</summary>
    private static readonly Int32[] ResetChance = [28, 62, 92, 100];

    private const Int32 MinPoints = -32767;

    private readonly Int32 _wise = Math.Clamp(wise, 0, 3);

    public (TetraCard Card, Int32 X, Int32 Y) Choose(CardBoard board,
        IReadOnlyList<TetraCard> hand, Int32 ownScore, Int32 theirScore, Random rng)
    {
        // winning makes it aggressive and losing makes it cautious
        var (flipFactor, fightFactor) =
            ownScore == theirScore ? (64, 1) :
            ownScore < theirScore ? (67, 1) : (1, 5);

        if (rng.Next(0, 100) > ResetChance[_wise])
            (flipFactor, fightFactor) = (0, 0);

        var scores = new Int32[hand.Count, CardBoard.Width, CardBoard.Height];
        var best = MinPoints;
        var ties = 0;
        
        // the loop order is card, then COLUMN, then row. It matters,
        // because it decides which placement the tie-break favors.
        for (var i = 0; i < hand.Count; i++)
        for (var x = 0; x < CardBoard.Width; x++)
        for (var y = 0; y < CardBoard.Height; y++)
        {
            var score = Score(board, hand[i], x, y, flipFactor, fightFactor);
            scores[i, x, y] = score;
            if (score == best)
                ties++;
            if (score > best)
                (best, ties) = (score, 1);
        }

        best = MinPoints;
        var pick = (Card: hand[0], X: 0, Y: 0);
        for (var i = 0; i < hand.Count; i++)
        for (var x = 0; x < CardBoard.Width; x++)
        for (var y = 0; y < CardBoard.Height; y++)
        {
            // a strictly better score always wins; an EQUAL one overrides on a
            // 1-in-ties roll, so the last successful roll takes the square.
            // The resulting distribution is P(1) = ((n-1)/n)^(n-1) — the
            // first best-scored placement keeps it more often than any other.
            if (best >= scores[i, x, y]
                && !(best == scores[i, x, y] && rng.Next(0, 1000) < 1000 / ties))
                continue;

            pick = (hand[i], x, y);
            best = scores[i, x, y];
        }

        return pick;
    }

    private Int32 Score(CardBoard board, TetraCard card, Int32 x, Int32 y, Int32 flipFactor, Int32 fightFactor)
    {
        if (!board.IsFree(x, y))
            return MinPoints;

        var (flips, challenges) = board.Survey(x, y, new PlayedCard(card, CardSide.Opponent));
        if (challenges.Count > 0)
            return FightScore(card, challenges[0].Card, fightFactor);

        return flips.Count * flipFactor;
    }

    private Int32 FightScore(TetraCard attacker, TetraCard defender, Int32 fightFactor)
    {
        var (a, d) = CardBattle.Stats(attacker, defender);
        var winChance = a * 100 / (a + d);

        if (_wise < 3)
            return winChance * fightFactor;
        // the sharpest setting INVERTS its preference above the threshold: 145 - chance falls
        // as the odds improve.
        return winChance < 60 ? winChance - 100 : 145 - winChance;
    }
}

