using Alexandria.Cards;

namespace Alexandria.Cards.Tests;

public class CardMatchTests
{
    private static TetraCard Card(CardArrows arrows = CardArrows.None) =>
        new(0, "Test", 0x50, CardType.Physical, 0x50, 0x50, CardIcon.Monster) { Arrows = arrows };

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
}

