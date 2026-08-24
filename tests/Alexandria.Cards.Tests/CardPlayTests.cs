using Alexandria.Cards;

namespace Alexandria.Cards.Tests;

public class CardPlayTests
{
    private static PlayedCard Card(CardArrows arrows, CardSide side, Byte atk = 0x50, Byte def = 0x50) =>
        new(new TetraCard(0, "Test", atk, CardType.Physical, def, def, CardIcon.Monster)
            { Arrows = arrows }, side);

    private static PlayedCard? Decline(IReadOnlyList<PlayedCard> _) => null;
    private static PlayedCard? First(IReadOnlyList<PlayedCard> options) => options[0];

    [Fact]
    public void Undefended_neighbours_just_turn_over()
    {
        var board = new CardBoard();
        var victim = Card(CardArrows.Up, CardSide.Opponent);
        board[1, 0] = victim;

        var outcome = CardPlay.Play(board, 0, 0, Card(CardArrows.Right, CardSide.Player),
            new Random(1), Decline);

        Assert.Equal(CardSide.Player, victim.Side);
        Assert.Empty(outcome.Battles);
        Assert.False(outcome.AttackerTaken);
    }

    [Fact]
    public void Winning_a_fight_turns_the_defender_and_sweeps_what_it_points_at()
    {
        var board = new CardBoard();
        var defender = Card(CardArrows.Left | CardArrows.Right, CardSide.Opponent, def: 0x10);
        var bystander = Card(CardArrows.None, CardSide.Opponent);
        board[1, 0] = defender;
        board[2, 0] = bystander;                     // defender points Right at it

        // attack 0x50 rolls 0x40, defence 0x10 rolls 0x00 — the attacker wins
        var outcome = CardPlay.Play(board, 0, 0, Card(CardArrows.Right, CardSide.Player),
            new RiggedRandom(0x40, 0x00), First);

        Assert.Equal(CardSide.Player, defender.Side);
        Assert.Equal(CardSide.Player, bystander.Side);   // taken by the combo, with no fight
        Assert.Single(outcome.Battles);
    }

    [Fact]
    public void Losing_forfeits_the_free_flips_and_your_own_neighbours_with_them()
    {
        var board = new CardBoard();
        var defender = Card(CardArrows.Left, CardSide.Opponent, def: 0xF0);
        var freebie = Card(CardArrows.None, CardSide.Opponent);
        var mine = Card(CardArrows.None, CardSide.Player);
        board[1, 0] = defender;      // points back — a challenge
        board[0, 1] = freebie;       // would have flipped for free
        board[1, 1] = mine;          // already mine, and the attacker points at it

        var attacker = Card(CardArrows.Right | CardArrows.Down | CardArrows.DownRight, CardSide.Player);

        // attack 0x50 rolls 0x00, defence 0xF0 rolls 0x80 — the attacker loses
        var outcome = CardPlay.Play(board, 0, 0, attacker, new RiggedRandom(0x00, 0x80), First);

        Assert.True(outcome.AttackerTaken);
        Assert.Equal(CardSide.Opponent, attacker.Side);
        Assert.Equal(CardSide.Opponent, mine.Side);      // dragged over by the loss
        Assert.Equal(CardSide.Opponent, freebie.Side);   // never flipped at all
    }

    [Fact]
    public void A_win_earns_another_fight()
    {
        var board = new CardBoard();
        var first = Card(CardArrows.Left, CardSide.Opponent, def: 0x10);
        var second = Card(CardArrows.Up, CardSide.Opponent, def: 0x10);
        board[1, 0] = first;
        board[0, 1] = second;

        var outcome = CardPlay.Play(board, 0, 0,
            Card(CardArrows.Right | CardArrows.Down, CardSide.Player),
            new RiggedRandom(0x40, 0x00, 0x40, 0x00), First);

        Assert.Equal(2, outcome.Battles.Count);
        Assert.Equal(CardSide.Player, first.Side);
        Assert.Equal(CardSide.Player, second.Side);
    }
}
