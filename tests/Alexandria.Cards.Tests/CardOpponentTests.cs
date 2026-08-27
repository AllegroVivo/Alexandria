namespace Alexandria.Cards.Tests;

public class CardOpponentTests
{
    private static TetraCard Card(String name, CardArrows arrows, Int32 stat = 1) =>
        new(0, name, (Byte)stat, CardType.Physical, (Byte)stat, (Byte)stat, CardIcon.Summon)
        { Arrows = arrows };

    /// <summary>A board offering exactly two things: a FIGHT at 0,0 (the player's card there
    /// points back) and a free FLIP at 3,0 (the player's card there has no arrows at all).</summary>
    private static CardBoard TwoChoices()
    {
        var board = new CardBoard();
        board[0, 1] = new PlayedCard(Card("Fighter", CardArrows.Up), CardSide.Player);
        board[3, 1] = new PlayedCard(Card("Bystander", 0), CardSide.Player);
        return board;
    }

    [Fact]
    public void Winning_makes_it_pick_the_fight()
    {
        // ahead: fightFactor 5, so an even fight scores 50 * 5 = 250 against a flip's 1
        var brain = new CardOpponent(wise: 2);
        var hand = new[] { Card("Theirs", CardArrows.Down) };

        var (_, x, y) = brain.Choose(TwoChoices(), hand, ownScore: 5, theirScore: 3, new RiggedDice(0));

        Assert.Equal((0, 0), (x, y));
    }

    [Fact]
    public void Losing_makes_it_take_the_safe_flip_instead()
    {
        // behind: flipFactor 67 against a fight now worth only 50 * 1 — same board, opposite call
        var brain = new CardOpponent(wise: 2);
        var hand = new[] { Card("Theirs", CardArrows.Down) };

        var (_, x, y) = brain.Choose(TwoChoices(), hand, ownScore: 3, theirScore: 5, new RiggedDice(0));

        Assert.Equal((3, 0), (x, y));
    }

    [Fact]
    public void A_blocked_square_is_never_chosen()
    {
        var blocked = (from cx in Enumerable.Range(0, 4)
            from cy in Enumerable.Range(0, 4)
            where (cx, cy) != (0, 0)
            select (cx, cy)).ToArray();
        var brain = new CardOpponent();
        var hand = new[] { Card("Theirs", CardArrows.Down) };

        var (_, x, y) = brain.Choose(new CardBoard(blocked), hand, 0, 0, new RiggedDice(99));

        Assert.Equal((0, 0), (x, y));
    }

    [Fact]
    public void An_empty_board_keeps_the_first_placement_when_every_roll_fails()
    {
        // sixteen squares all scoring 0, so 1000/16 = 62 and a roll of 999 never clears it
        var brain = new CardOpponent();
        var hand = new[] { Card("Theirs", 0) };
        var dice = new RiggedDice([99, .. Enumerable.Repeat(999, 15)]);

        var (_, x, y) = brain.Choose(new CardBoard(), hand, 0, 0, dice);

        Assert.Equal((0, 0), (x, y));
    }

    [Fact]
    public void The_last_successful_roll_takes_the_square()
    {
        // squares are visited card, then COLUMN, then row — so the second one is 0,1.
        // It clears its roll; everything after it fails; it keeps the placement.
        var brain = new CardOpponent();
        var hand = new[] { Card("Theirs", 0) };
        var dice = new RiggedDice([99, 0, .. Enumerable.Repeat(999, 14)]);

        var (_, x, y) = brain.Choose(new CardBoard(), hand, 0, 0, dice);

        Assert.Equal((0, 1), (x, y));
    }
}
