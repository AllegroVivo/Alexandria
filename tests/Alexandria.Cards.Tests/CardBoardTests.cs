using Alexandria.Cards;

namespace Alexandria.Cards.Tests;

public class CardBoardTests
{
    private static PlayedCard Card(CardArrows arrows, CardSide side = CardSide.Player) =>
        new(new TetraCard(0, "Test", 0x50, CardType.Physical, 0x50, 0x50, CardIcon.Monster)
            { Arrows = arrows }, side);

    [Fact]
    public void A_corner_square_has_three_neighbours_and_the_middle_has_eight()
    {
        var board = new CardBoard();
        for (var y = 0; y < CardBoard.Height; y++)
            for (var x = 0; x < CardBoard.Width; x++)
                board[x, y] = Card(CardArrows.None);

        Assert.Equal(3, board.Neighbors(0, 0).Count());
        Assert.Equal(5, board.Neighbors(1, 0).Count());
        Assert.Equal(8, board.Neighbors(1, 1).Count());
    }

    [Fact]
    public void Blocked_squares_are_invisible_to_everything()
    {
        var board = new CardBoard((1, 0))
        {
            [1, 0] = Card(CardArrows.None) // nothing should be placeable there anyway
        };

        Assert.True(board.IsBlocked(1, 0));
        Assert.False(board.IsFree(1, 0));
        Assert.Null(board[1, 0]);
        Assert.Empty(board.Neighbors(0, 0));
    }

    [Fact]
    public void Pointing_at_a_neighbour_that_does_not_point_back_is_a_free_flip()
    {
        var board = new CardBoard
        {
            [1, 0] = Card(CardArrows.Up, CardSide.Opponent) // points away, not back at us
        };

        var (flips, challenges) = board.Survey(0, 0, Card(CardArrows.Right));

        Assert.Single(flips);
        Assert.Empty(challenges);
    }

    [Fact]
    public void A_neighbour_pointing_back_has_to_be_fought_for()
    {
        var board = new CardBoard
        {
            [1, 0] = Card(CardArrows.Left, CardSide.Opponent)
        };

        var (flips, challenges) = board.Survey(0, 0, Card(CardArrows.Right));

        Assert.Empty(flips);
        Assert.Single(challenges);
    }

    [Fact]
    public void A_card_that_points_at_nothing_touches_nothing_even_when_surrounded()
    {
        var board = new CardBoard
        {
            [1, 0] = Card(CardArrows.Left, CardSide.Opponent),
            [0, 1] = Card(CardArrows.Up, CardSide.Opponent)
        };

        var (flips, challenges) = board.Survey(0, 0, Card(CardArrows.None));

        Assert.Empty(flips);
        Assert.Empty(challenges);
        Assert.Equal(2, board.Score(CardSide.Opponent));
    }
}