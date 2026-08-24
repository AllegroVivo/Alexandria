using Alexandria.Cards;

namespace Alexandria.Cards.Tests;

public class CardBattleTests
{
    private static TetraCard Card(Byte atk, CardType type, Byte pdef, Byte mdef) =>
        new(0, "Test", atk, type, pdef, mdef, CardIcon.Monster);

    [Fact]
    public void Type_chooses_which_guard_the_attacker_has_to_get_through()
    {
        var armoured = Card(0x10, CardType.Physical, pdef: 0xF0, mdef: 0x20);

        Assert.Equal(0xF0, CardBattle.Stats(Card(0x70, CardType.Physical, 0, 0), armoured).Defense);
        Assert.Equal(0x20, CardBattle.Stats(Card(0x70, CardType.Magic, 0, 0), armoured).Defense);
        Assert.Equal(0x20, CardBattle.Stats(Card(0x70, CardType.Flexible, 0, 0), armoured).Defense);
    }

    [Fact]
    public void Assault_swings_its_best_stat_at_the_defenders_worst()
    {
        var brute = Card(0x30, CardType.Assault, pdef: 0x90, mdef: 0x10);
        var target = Card(0x40, CardType.Physical, pdef: 0x80, mdef: 0x20);

        var (attack, defence) = CardBattle.Stats(brute, target);

        Assert.Equal(0x90, attack);    // its own physical defence is its biggest number
        Assert.Equal(0x20, defence);   // the target's magical defence is its smallest
    }

    [Fact]
    public void A_zero_stat_still_gets_a_roll_of_exactly_zero()
    {
        var nothing = Card(0x00, CardType.Physical, 0x00, 0x00);
        var (attack, defence) = CardBattle.Stats(nothing, nothing);

        Assert.Equal(1, attack);
        Assert.Equal(1, defence);

        var battle = CardBattle.Fight(nothing, nothing, new RiggedRandom(0, 0));
        Assert.False(battle.AttackerWins);
    }

    [Fact]
    public void A_tie_goes_to_the_defender()
    {
        var card = Card(0x50, CardType.Physical, 0x50, 0x50);

        Assert.False(CardBattle.Fight(card, card, new RiggedRandom(0x30, 0x30)).AttackerWins);
        Assert.True(CardBattle.Fight(card, card, new RiggedRandom(0x31, 0x30)).AttackerWins);
    }

    [Fact]
    public void The_hidden_nibble_decides_a_fight_the_card_faces_call_even()
    {
        // both read "5" on the face; only the low nibble separates them
        var weak = Card(0x50, CardType.Physical, 0, 0);
        var strong = Card(0x5F, CardType.Physical, 0, 0);
        var wall = Card(0, CardType.Physical, pdef: 0x50, mdef: 0);

        Assert.Equal(0x50, CardBattle.Stats(weak, wall).Attack);
        Assert.Equal(0x5F, CardBattle.Stats(strong, wall).Attack);
    }
}

