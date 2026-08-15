using Alexandria.Battle;

namespace Alexandria.Battle.Tests;

public class AtbClockTests
{
    private static BattleUnit Unit(Int32 speed, Int32 hp = 100) => new()
    {
        Name = "Test", Level = 1, MaxHP = hp, MaxMP = 0,
        CurrentHP = hp, CurrentMP = 0,
        Speed = speed, Strength = 10, Magic = 10, Spirit = 10,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
    };

    [Fact]
    public void Type_C_needs_156_ticks_for_a_turn()
    {
        // Speed 21 → gauge (60-21)*40 = 1560; 1560 / 10 per tick = 156 exactly.
        var u = Unit(21);
        Assert.Equal(1560, u.ATBMax);

        for (var t = 0; t < 155; t++)
            ATBClock.Tick(u);
        Assert.False(u.ATBReady);

        ATBClock.Tick(u);
        Assert.True(u.ATBReady);
    }

    [Fact]
    public void Faster_units_have_shorter_gauges()
    {
        Assert.Equal(400, Unit(50).ATBMax);
        Assert.Equal(2000, Unit(10).ATBMax);
    }

    [Fact]
    public void Overshoot_clamps_and_dead_units_never_charge()
    {
        var u = Unit(21);
        for (var t = 0; t < 200; t++)
            ATBClock.Tick(u, ATBClock.FastCoefficient);
        Assert.Equal(u.ATBMax, u.ATBGauge);   // clamped, no carry

        var dead = Unit(21, hp: 0);
        ATBClock.Tick(dead);
        Assert.Equal(0, dead.ATBGauge);
    }

    [Fact]
    public void Preemptive_strike_places_gauges_like_the_game()
    {
        var players = new[] { Unit(23) };
        var enemies = new[] { Unit(21) };
        ATBClock.PlaceInitialGauges(players, enemies, EncounterKind.Preemptive, new Random(9));

        Assert.Equal(players[0].ATBMax - 1, players[0].ATBGauge);   // one tick from acting
        Assert.Equal(0, enemies[0].ATBGauge);
    }

    [Fact]
    public void Normal_encounters_roll_everyone_somewhere_on_the_gauge()
    {
        var players = new[] { Unit(23) };
        var enemies = new[] { Unit(21) };
        ATBClock.PlaceInitialGauges(players, enemies, EncounterKind.Normal, new Random(9));

        Assert.InRange(players[0].ATBGauge, 0, players[0].ATBMax - 1);
        Assert.InRange(enemies[0].ATBGauge, 0, enemies[0].ATBMax - 1);
    }
}