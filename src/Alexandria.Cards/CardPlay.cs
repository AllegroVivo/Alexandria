namespace Alexandria.Cards;

public static class CardPlay
{
    /// <summary>Put a card down and settle everything it touches (mirror: QuadMistGame's
    /// CALCULATE_BATTLE / ANIMATE_BATTLE pair).</summary>
    public static PlayOutcome Play(CardBoard board, Int32 x, Int32 y, PlayedCard card,
        Random rng, Func<IReadOnlyList<PlayedCard>, PlayedCard?> choose)
    {
        board[x, y] = card;
        var (flips, challenges) = board.Survey(x, y, card);

        List<PlayedCard> flipped = [];
        List<CardBattle> battles = [];
        var taken = false;

        while (challenges.Count > 0 && choose(challenges) is { } defender)
        {
            var battle = CardBattle.Fight(card.Card, defender.Card, rng);
            battles.Add(battle);

            if (battle.AttackerWins)
            {
                card.Promote(rng);
                
                // computed BEFORE the flip, while the sides still say who owned what
                var combo = Combo(board, defender, card.Side);

                Turn(defender, card.Side, flipped);
                foreach (var swept in combo)
                {
                    Turn(swept, card.Side, flipped);
                    challenges.Remove(swept);
                    flips.Remove(swept);
                }
                challenges.Remove(defender);
                continue;
            }

            defender.Promote(rng);
            
            // the placed card is taken, and its OWN neighbors go with it
            var loss = Combo(board, card, defender.Side);
            Turn(card, defender.Side, flipped);
            foreach (var swept in loss)
                Turn(swept, defender.Side, flipped);
            
            flips.Clear();

            taken = true;
            break;
        }

        foreach (var free in flips)
            Turn(free, card.Side, flipped);

        return new PlayOutcome(flipped, battles, taken);
    }

    private static void Turn(PlayedCard card, CardSide side, List<PlayedCard> flipped)
    {
        card.Side = side;
        flipped.Add(card);
    }

    /// <summary>The cascade (mirror: GenerateCombo). A card that has just changed hands
    /// turns every neighbor it POINTS AT that is not already on the winning side.</summary>
    private static List<PlayedCard> Combo(CardBoard board, PlayedCard card, CardSide winner)
    {
        if (board.Locate(card) is not { } at)
            return [];

        return
        [
            .. board.Neighbors(at.X, at.Y)
                .Where(n => n.Card.Side != winner
                            && Arrows.Clash(card.Arrows, n.Card.Arrows, n.Direction)
                                is ArrowClash.Flip or ArrowClash.Battle)
                .Select(n => n.Card)
        ];
    }
}

