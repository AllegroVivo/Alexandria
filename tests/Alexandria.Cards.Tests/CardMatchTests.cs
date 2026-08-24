using Alexandria.Cards;

namespace Alexandria.Cards.Tests;

public class CardMatchTests
{
    private static TetraCard Card(CardArrows arrows = CardArrows.None) =>
        new(0, "Test", 0x50, CardType.Physical, 0x50, 0x50, CardIcon.Monster) { Arrows = arrows };
    
    private static TetraCard Named(Int32 id, String name, CardArrows arrows = CardArrows.None) =>
        new(id, name, 0x50, CardType.Physical, 0x50, 0x50, CardIcon.Monster) { Arrows = arrows };

    /// <summary>Plays a fixed ten-move game the player wins 6–4. Only one card has an arrow,
    /// so exactly one capture happens and no fight is ever fought — which means no dice are
    /// drawn after the constructor's two, and the rigged queue proves it.</summary>
    private static CardMatch WonMatch()
    {
        List<TetraCard> mine =
        [
            Named(0, "Mine A"), Named(1, "Mine B", CardArrows.Left), Named(2, "Mine C"),
            Named(3, "Mine D"), Named(4, "Mine E")
        ];
        List<TetraCard> theirs =
        [
            Named(5, "Theirs A"), Named(6, "Theirs B"), Named(7, "Theirs C"),
            Named(8, "Theirs D"), Named(9, "Theirs E")
        ];

        // roll 0 → no blocks; toss 0 → the player opens
        var match = new CardMatch(mine, theirs, new RiggedRandom(0, 0));

        // Mine B lands third, at (2,0), pointing Left at the Theirs A already on (1,0)
        (Int32 X, Int32 Y)[] squares =
            [(0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1), (3, 0), (3, 1), (0, 2), (1, 2)];

        foreach (var (x, y) in squares)
            match.Play(x, y, match.Hand(match.Turn)[0], _ => null);

        return match;
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(26, 4)]
    [InlineData(27, 5)]
    [InlineData(96, 5)]
    [InlineData(97, 6)]
    public void The_block_count_follows_the_shipped_distribution(Int32 roll, Int32 expected)
        => Assert.Equal(expected, CardMatch.RollBlockCount(new RiggedRandom(roll)));

    [Fact]
    public void A_match_ends_when_both_hands_are_empty_and_scores_sum_to_ten()
    {
        var hand = Enumerable.Range(0, 5).Select(_ => Card()).ToList();
        // roll 0 blocks, then the coin toss
        var match = new CardMatch(hand, [.. hand], new RiggedRandom([0, 0, .. new Int32[64]]));

        var free = new Queue<(Int32 X, Int32 Y)>(
            from y in Enumerable.Range(0, CardBoard.Height)
            from x in Enumerable.Range(0, CardBoard.Width)
            select (x, y));

        while (!match.IsOver)
        {
            var card = match.Hand(match.Turn)[0];
            var (x, y) = free.Dequeue();
            match.Play(x, y, card, _ => null);
        }

        Assert.Equal(10, match.Board.Score(CardSide.Player) + match.Board.Score(CardSide.Opponent));
        Assert.Equal(MatchResult.Draw, match.Result);
    }
    
    [Fact]
    public void Spoils_are_the_losers_own_cards_and_never_your_own()
    {
        var match = WonMatch();

        Assert.Equal(MatchResult.Win, match.Result);
        Assert.Equal(6, match.Board.Score(CardSide.Player));

        // the player also ends up holding all five of their OWN cards — none of which count
        Assert.Single(match.Spoils);
        Assert.Equal("Theirs A", match.Spoils[0].Card.Name);
        Assert.DoesNotContain(match.Spoils, c => c.Card.Name.StartsWith("Mine"));
    }

    [Fact]
    public void Winning_nudges_a_stat_on_every_card_the_player_ends_up_holding()
    {
        var match = WonMatch();
        var pool = Enumerable.Range(0, 10)
            .Select(id => Named(id, $"Max {id}") with { Attack = 0x60 })
            .ToList();

        // six cards on the player's side, each drawing one stat pick — 0 is always attack
        match.AwardLevels(pool, new RiggedRandom([.. Enumerable.Repeat(0, 6)]));

        Assert.Equal(0x51, match.Spoils[0].Card.Attack);
    }
}

