using Alexandria.Cards;

namespace Alexandria.Cards.Tests;

public class CardLevelTests
{
    private static TetraCard Card(Byte atk, CardType type = CardType.Physical) =>
        new(0, "Test", atk, type, 0x50, 0x50, CardIcon.Monster);

    [Fact]
    public void A_stat_creeps_up_but_never_past_the_pool_ceiling()
    {
        var max = Card(0x51);
        Assert.Equal(0x51, CardLevel.LevelUp(Card(0x50), max, new RiggedRandom(0)).Attack);
        Assert.Equal(0x51, CardLevel.LevelUp(Card(0x51), max, new RiggedRandom(0)).Attack);
    }

    [Fact]
    public void Promotion_climbs_one_rung_at_a_time_and_stops_at_assault()
    {
        Assert.Equal(CardType.Flexible, CardLevel.Promote(Card(0x50), new RiggedRandom(0)).Type);
        Assert.Equal(CardType.Physical, CardLevel.Promote(Card(0x50), new RiggedRandom(1)).Type);

        var flexible = Card(0x50, CardType.Flexible);
        Assert.Equal(CardType.Assault, CardLevel.Promote(flexible, new RiggedRandom(0)).Type);

        // Assault rolls nothing at all — an empty queue proves it
        Assert.Equal(CardType.Assault,
            CardLevel.Promote(Card(0x50, CardType.Assault), new RiggedRandom()).Type);
    }
}
