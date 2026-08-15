using Alexandria.Battle;

namespace Alexandria.Battle.Tests;

public class StatusTickerTests
{
    private static BattleUnit Wolf(BattleStatus statuses) => new()
    {
        Name = "Wolf", Level = 14, MaxHP = 899, MaxMP = 0, CurrentHP = 899, CurrentMP = 0,
        Speed = 21, Strength = 11, Magic = 11, Spirit = 16,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        Statuses = statuses,
    };

    [Fact]
    public void Poison_bites_on_schedule_for_a_sixteenth()
    {
        // reload = 10 × (16<<2) = 640; at coefficient 10 → fires on the 64th frame
        // damage = 899 >> 4 = 56
        var wolf = Wolf(BattleStatus.Poison);
        for (var frame = 0; frame < 63; frame++)
            StatusTicker.Tick(wolf, 10);
        Assert.Equal(899, wolf.CurrentHP);

        var events = StatusTicker.Tick(wolf, 10);
        Assert.Equal(843, wolf.CurrentHP);
        Assert.Equal(-56, events.Single().HPDelta);
    }

    [Fact]
    public void Sleep_wears_off_when_the_clock_runs_out()
    {
        // duration = 25 × ((60−16)<<3) = 8800; expires when the counter goes NEGATIVE:
        // 880 ticks reach exactly 0 (still asleep), the 881st goes below.
        var wolf = Wolf(BattleStatus.Sleep);
        for (var frame = 0; frame < 880; frame++)
            StatusTicker.Tick(wolf, 10);
        Assert.True(wolf.Has(BattleStatus.Sleep));

        var events = StatusTicker.Tick(wolf, 10);
        Assert.False(wolf.Has(BattleStatus.Sleep));
        Assert.True(events.Single().Expired);
    }

    [Fact]
    public void Regen_heals_but_never_overfills()
    {
        // reload = 10 × ((60−16)<<2) = 1760 → fires on the 176th frame; heal 56
        var wolf = Wolf(BattleStatus.Regen);
        wolf.CurrentHP = 880;
        for (var frame = 0; frame < 176; frame++)
            StatusTicker.Tick(wolf, 10);
        Assert.Equal(899, wolf.CurrentHP);   // 880 + 56 capped at max
    }
}
