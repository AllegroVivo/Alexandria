using Alexandria.Formats.Scripts;

namespace Alexandria.Battle.Tests;

public class StatusGateTests
{
    private static BattleUnit Unit(String name) => new()
    {
        Name = name, Level = 10, MaxHP = 600, MaxMP = 0, CurrentHP = 600, CurrentMP = 0,
        Speed = 20, Strength = 10, Magic = 10, Spirit = 0,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        AttackPower = 10, HasTrance = false
    };

    private const Int32 FirstEnemy = 1 << 4;   // lines 0-3 are the party, 4-7 the formation

    // ---- a script writing statuses ----

    [Fact]
    public void A_script_write_cannot_land_a_status_the_target_resists()
    {
        var wolf = Unit("Wolf");
        wolf.ImmuneStatuses = BattleStatus.Silence;
        var host = new EngineHost([Unit("Zidane")], [wolf], new Random(1));

        host.WriteStat(FirstEnemy, CharacterStat.StatusCurrentA, (Int32)BattleStatus.Silence);

        Assert.False(wolf.Has(BattleStatus.Silence));
    }

    [Fact]
    public void A_script_write_clears_what_it_no_longer_names_and_drops_its_timers()
    {
        var wolf = Unit("Wolf");
        wolf.Statuses = BattleStatus.Poison | BattleStatus.Blind;
        wolf.OprCounters[BattleStatus.Poison] = 5;
        var host = new EngineHost([Unit("Zidane")], [wolf], new Random(1));

        host.WriteStat(FirstEnemy, CharacterStat.StatusCurrentA, (Int32)BattleStatus.Blind);

        Assert.True(wolf.Has(BattleStatus.Blind));
        Assert.False(wolf.Has(BattleStatus.Poison));
        Assert.Empty(wolf.OprCounters);      // a raw store would have left the timer behind
    }

    [Fact]
    public void A_permanent_status_the_target_resists_never_becomes_permanent()
    {
        // mirror: permanent |= statuses & cur — only what landed counts
        var wolf = Unit("Wolf");
        wolf.ImmuneStatuses = BattleStatus.Silence;
        var host = new EngineHost([Unit("Zidane")], [wolf], new Random(1));

        host.WriteStat(FirstEnemy, CharacterStat.StatusAutoA, (Int32)BattleStatus.Silence);

        Assert.Equal((BattleStatus)0, wolf.PermanentStatuses);
        Assert.False(wolf.Has(BattleStatus.Silence));
    }

    // ---- LowHP, recomputed rather than inflicted ----

    [Fact]
    public void LowHP_arrives_at_a_sixth_of_max_and_leaves_again_when_healed()
    {
        var zidane = Unit("Zidane");
        var engine = new BattleEngine([zidane], [Unit("Wolf")], EncounterKind.Normal, new Random(1));

        zidane.CurrentHP = zidane.MaxHP / 6;
        engine.TickUntilReady();
        Assert.True(zidane.Has(BattleStatus.LowHP));

        zidane.CurrentHP = zidane.MaxHP;
        engine.TickUntilReady();
        Assert.False(zidane.Has(BattleStatus.LowHP));
    }

    [Fact]
    public void An_enemy_on_its_last_legs_never_gets_LowHP()
    {
        // vanilla gates the whole recomputation on unit.IsPlayer
        var wolf = Unit("Wolf");
        var engine = new BattleEngine([Unit("Zidane")], [wolf], EncounterKind.Normal, new Random(1));

        wolf.CurrentHP = 1;
        engine.TickUntilReady();

        Assert.False(wolf.Has(BattleStatus.LowHP));
    }
}
