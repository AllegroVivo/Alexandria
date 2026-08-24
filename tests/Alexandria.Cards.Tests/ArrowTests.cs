using Alexandria.Cards;

namespace Alexandria.Cards.Tests;

public class ArrowTests
{
    [Theory]
    [InlineData(CardArrows.Up, CardArrows.Down)]
    [InlineData(CardArrows.UpRight, CardArrows.DownLeft)]
    [InlineData(CardArrows.Right, CardArrows.Left)]
    [InlineData(CardArrows.DownRight, CardArrows.UpLeft)]
    public void Reversing_turns_a_direction_into_its_opposite(CardArrows from, CardArrows to)
    {
        Assert.Equal(to, from.Reversed);
        Assert.Equal(from, to.Reversed);
    }

    [Fact]
    public void Reversing_twice_returns_every_possible_card_to_itself()
    {
        for (var mask = 0; mask <= Byte.MaxValue; mask++)
        {
            var arrows = (CardArrows)mask;
            Assert.Equal(arrows, arrows.Reversed.Reversed);
        }
    }

    [Fact]
    public void Opposite_directions_step_back_to_where_they_started()
    {
        foreach (var direction in Arrows.Ring)
        {
            var (x, y) = Arrows.Offset(direction);
            var (rx, ry) = Arrows.Offset(direction.Reversed);
            Assert.Equal((0, 0), (x + rx, y + ry));
        }
    }

    [Fact]
    public void An_edge_is_a_battle_only_when_both_cards_point_at_each_other()
    {
        // the attacker is playing from the LEFT, so it points Right and the defender
        // defends by pointing Left — back at it
        Assert.Equal(ArrowClash.Battle,
            Arrows.Clash(CardArrows.Right, CardArrows.Left, CardArrows.Right));
        Assert.Equal(ArrowClash.Flip,
            Arrows.Clash(CardArrows.Right, CardArrows.Up, CardArrows.Right));
        Assert.Equal(ArrowClash.Blocked,
            Arrows.Clash(CardArrows.Up, CardArrows.Left, CardArrows.Right));
        Assert.Equal(ArrowClash.None,
            Arrows.Clash(CardArrows.Up, CardArrows.Up, CardArrows.Right));
    }

    [Fact]
    public void The_arrow_count_comes_off_the_shipped_distribution()
    {
        // Roll always draws NINE dice: the count, then eight shuffle swaps that happen
        // whatever the count turned out to be. 0 lands in the 1% bucket that gets nothing,
        // 99 in the 1% bucket that gets all eight.
        Assert.Equal(CardArrows.None, Arrows.Roll(new RiggedRandom(0, 0, 0, 0, 0, 0, 0, 0, 0)));
        Assert.Equal(8, Arrows.Roll(new RiggedRandom(99, 0, 1, 2, 3, 4, 5, 6, 7)).Count);
    }
}