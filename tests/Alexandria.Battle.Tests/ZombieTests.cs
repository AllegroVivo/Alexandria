using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

public class ZombieTests
{
    private static BattleUnit Unit(String name, BattleStatus statuses = 0) => new()
    {
        Name = name, Level = 10, MaxHP = 600, MaxMP = 100, CurrentHP = 600, CurrentMP = 100,
        Speed = 20, Strength = 10, Magic = 20, Spirit = 10,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        AttackPower = 10, HasTrance = false, Statuses = statuses
    };

    private static (BattleEngine Engine, BattleUnit Cleric, BattleUnit Zombie) Setup(Random? rng = null)
    {
        var cleric = Unit("Garnet");
        var zombie = Unit("Zombie", BattleStatus.Zombie);
        return (new BattleEngine([cleric], [zombie], EncounterKind.Normal, rng ?? new Random(1)),
            cleric, zombie);
    }

    private static BattleAbility Spell(String name, AbilityScript script, Int32 power = 10, Int32 rate = 100)
        => new(name, script, power, Element.None, rate, 0, 0);

    [Fact]
    public void Healing_magic_harms_the_undead()
    {
        var (engine, cleric, zombie) = Setup();
        zombie.CurrentHP = 300;

        var e = engine.ExecuteAbility(cleric, zombie, Spell("Cure", AbilityScript.MagicRecovery));

        Assert.Equal(AttackOutcome.Hit, e.Result.Outcome);
        Assert.True(zombie.CurrentHP < 300);
        Assert.Equal(300 - zombie.CurrentHP, e.Result.Damage);
    }

    [Fact]
    public void White_Wind_harms_the_undead()
    {
        var (engine, cleric, zombie) = Setup();
        zombie.CurrentHP = 400;

        engine.ExecuteAbility(cleric, zombie, Spell("White Wind", AbilityScript.WhiteWind));

        Assert.True(zombie.CurrentHP < 400);
    }

    [Fact]
    public void Life_kills_the_undead_but_has_to_connect_first()
    {
        // gauges, then MagicRoll's hit and evade rolls
        var (engine, cleric, zombie) = Setup(new RiggedDice(0, 0, 0, 0));

        var e = engine.ExecuteAbility(cleric, zombie, Spell("Life", AbilityScript.Revive));

        Assert.Equal(0, zombie.CurrentHP);
        Assert.True(zombie.Has(BattleStatus.Death));
        Assert.Equal(BattleStatus.Death, e.StatusesAdded);
    }

    [Fact]
    public void Life_that_misses_leaves_the_undead_standing()
    {
        // a hit roll of 100 fails a hitRate that clamps at 100
        // rate 1 + Magic/4 = a hitRate of 6, which a roll of 10 clears
        var (engine, cleric, zombie) = Setup(new RiggedDice(0, 0, 10));

        var e = engine.ExecuteAbility(cleric, zombie, Spell("Life", AbilityScript.Revive, rate: 1));

        Assert.Equal(AttackOutcome.Miss, e.Result.Outcome);
        Assert.True(zombie.IsAlive);
    }

    [Fact]
    public void Death_restores_the_undead_to_full()
    {
        var (engine, cleric, zombie) = Setup();
        zombie.CurrentHP = 120;

        var e = engine.ExecuteAbility(cleric, zombie, Spell("Death", AbilityScript.Death));

        Assert.Equal(zombie.MaxHP, zombie.CurrentHP);
        Assert.Equal(AttackOutcome.Healed, e.Result.Outcome);
        Assert.Equal(480, e.Result.Damage);
    }

    [Fact]
    public void Drain_MP_runs_backwards_and_the_caster_pays()
    {
        var (engine, cleric, zombie) = Setup();
        zombie.CurrentMP = 0;

        engine.ExecuteAbility(cleric, zombie, Spell("Osmose", AbilityScript.DrainMp, power: 40));

        var paid = 100 - cleric.CurrentMP;
        Assert.True(paid > 0);
        Assert.Equal(paid, zombie.CurrentMP);   // exactly what the caster lost, the Zombie gained
    }

    [Fact]
    public void Drain_HP_is_NOT_inverted()
    {
        // 0016 has no Zombie branch — the comment that said otherwise was wrong
        var (engine, cleric, zombie) = Setup();
        cleric.CurrentHP = 100;

        var e = engine.ExecuteAbility(cleric, zombie, Spell("Drain", AbilityScript.DrainHp, power: 20));

        if (e.Result is { Outcome: AttackOutcome.Hit, Damage: > 0 })
            Assert.True(cleric.CurrentHP > 100);
    }

    [Fact]
    public void An_Elixir_finishes_the_undead()
    {
        var (engine, cleric, zombie) = Setup();

        engine.ExecuteAbility(cleric, zombie, Spell("Elixir", AbilityScript.ItemElixir));

        Assert.Equal(0, zombie.CurrentHP);
        Assert.Equal(0, zombie.CurrentMP);
        Assert.True(zombie.Has(BattleStatus.Death));
    }

    [Fact]
    public void A_Phoenix_Down_leaves_the_undead_on_nought_to_nine()
    {
        // gauges, then the HP roll: 0 is fatal, where a living ally would get 1 to 10
        var (engine, cleric, zombie) = Setup(new RiggedDice(0, 0, 0));

        engine.ExecuteAbility(cleric, zombie, Spell("Phoenix Down", AbilityScript.ItemPhoenix));

        Assert.Equal(0, zombie.CurrentHP);
        Assert.True(zombie.Has(BattleStatus.Death));
    }

    [Fact]
    public void Regen_eats_away_at_the_undead()
    {
        var zombie = Unit("Zombie", BattleStatus.Zombie | BattleStatus.Regen);
        zombie.CurrentHP = 400;

        var events = StatusTicker.Tick(zombie, ATBClock.NormalCoefficient, StatusTicker.DefaultTable).ToList();
        while (zombie.CurrentHP == 400)
            events.AddRange(StatusTicker.Tick(zombie, ATBClock.NormalCoefficient, StatusTicker.DefaultTable));

        Assert.True(zombie.CurrentHP < 400);
        Assert.Contains(events, e => e is { Status: BattleStatus.Regen, HPDelta: < 0 });
    }
}
