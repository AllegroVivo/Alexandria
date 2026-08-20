using Alexandria.Formats;
using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

public class StatusDataTests
{
    private static BattleUnit Dummy() => new()
    {
        Name = "Dummy", Level = 14, MaxHP = 899, MaxMP = 0, CurrentHP = 899, CurrentMP = 0,
        Speed = 21, Strength = 11, Magic = 11, Spirit = 16,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
    };
    
        [Fact]
    public void Sleep_landing_strips_defend_and_freeze_blocks_vanish()
    {
        var table = new Dictionary<BattleStatus, StatusTiming>
        {
            [BattleStatus.Sleep] = new(0, 25, BattleStatus.Defend, 0),
            [BattleStatus.Freeze] = new(0, 20, BattleStatus.Heat, BattleStatus.Vanish),
        };
        var unit = Dummy();
        unit.Statuses = BattleStatus.Defend | BattleStatus.Freeze;

        var landed = StatusApplication.Apply(unit,
            BattleStatus.Sleep | BattleStatus.Vanish, table);

        Assert.Equal(BattleStatus.Sleep, landed);          // Vanish blocked by Freeze
        Assert.False(unit.Has(BattleStatus.Defend));       // stripped by Sleep
    }

    [Fact]
    public void A_woken_sleeper_starts_a_fresh_countdown()
    {
        var unit = Dummy();
        unit.Statuses = BattleStatus.Sleep;
        for (var i = 0; i < 400; i++)
            StatusTicker.Tick(unit, 10);                   // burn half the 8800
        StatusApplication.Remove(unit, BattleStatus.Sleep);

        unit.Statuses = BattleStatus.Sleep;                // re-applied later
        for (var i = 0; i < 500; i++)
            StatusTicker.Tick(unit, 10);                   // stale timer would expire at 481

        Assert.True(unit.Has(BattleStatus.Sleep));         // fresh 8800 survives 5000
    }

    [Fact]
    public void Stop_has_no_timer_and_mini_does()
    {
        var table = new Dictionary<BattleStatus, StatusTiming>
        {
            [BattleStatus.Stop] = new(0, 0, 0, 0),         // vanilla: permanent by timer
            [BattleStatus.Mini] = new(0, 20, 0, 0),
        };
        var unit = Dummy();
        unit.Statuses = BattleStatus.Stop | BattleStatus.Mini;

        // Mini: 20 × ((60−16)<<3) = 7040 → expires on tick 705 at coefficient 10
        for (var i = 0; i < 800; i++)
            StatusTicker.Tick(unit, 10, table);

        Assert.True(unit.Has(BattleStatus.Stop));
        Assert.False(unit.Has(BattleStatus.Mini));
    }
    
    [Fact]
    public void Status_data_loads_the_sleep_and_stop_rows()
    {
        var data = StatusEntry.Load(Path.Combine(FF9Data.Data, @"Battle\StatusData.csv"));

        Assert.Equal(25, data[17].ContiCount);          // Sleep's duration count
        Assert.Equal(1u << 15, data[17].ClearOnApply);  // Sleep strips Defend
        Assert.Equal(0, data[12].ContiCount);           // Stop: genuinely timerless
    }
}

