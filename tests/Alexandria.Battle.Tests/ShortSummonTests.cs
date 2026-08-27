using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

public class ShortSummonTests
{
    private static BattleUnit Unit(String name, Int32 mp = 100) => new()
    {
        Name = name, Level = 10, MaxHP = 9999, MaxMP = 100, CurrentHP = 9999, CurrentMP = mp,
        Speed = 20, Strength = 10, Magic = 20, Spirit = 0,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        AttackPower = 10, HasTrance = false
    };

    // attack = Magic 20 + a rigged 0; power 30 against 0 defence => 600, short => 400
    private static BattleAbility Shiva(Int32 mp = 20)
        => new("Shiva", AbilityScript.Summon, 30, Element.None, 0, 0, mp);

    [Fact]
    public void The_first_cast_of_a_summon_always_plays_in_full()
    {
        // and draws no die for the decision, so the queue holds only gauges and the attack roll
        var world = new WorldState();
        var garnet = Unit("Garnet");
        var wolf = Unit("Wolf");
        var engine = new BattleEngine([garnet], [wolf], EncounterKind.Normal,
            new RiggedDice(0, 0, 0), world: world);

        var e = engine.ExecuteAbility(garnet, wolf, Shiva());

        Assert.Equal(600, e.Result.Damage);
        Assert.Contains("Shiva", world.SummonsSeen);
    }

    [Fact]
    public void Once_seen_a_summon_is_usually_the_short_version()
    {
        var world = new WorldState();
        world.SummonsSeen.Add("Shiva");
        var garnet = Unit("Garnet");
        var wolf = Unit("Wolf");
        // the decision roll, then the attack roll
        var engine = new BattleEngine([garnet], [wolf], EncounterKind.Normal,
            new RiggedDice(0, 0, 0, 0), world: world);

        Assert.Equal(400, engine.ExecuteAbility(garnet, wolf, Shiva()).Result.Damage);   // 600 * 2/3
    }

    [Fact]
    public void A_high_roll_still_gets_the_full_animation()
    {
        var world = new WorldState();
        world.SummonsSeen.Add("Shiva");
        var garnet = Unit("Garnet");
        var wolf = Unit("Wolf");
        // 230 fails `roll < 230` by exactly one
        var engine = new BattleEngine([garnet], [wolf], EncounterKind.Normal,
            new RiggedDice(0, 0, 230, 0), world: world);

        Assert.Equal(600, engine.ExecuteAbility(garnet, wolf, Shiva()).Result.Damage);
    }

    [Fact]
    public void A_caster_short_of_MP_gets_the_full_version_more_often()
    {
        // 200 is short when the threshold is 230 and full when it drops to 170,
        // and the threshold turns on holding MORE than twice the ability's cost
        static Int32 DamageWith(Int32 casterMP)
        {
            var world = new WorldState();
            world.SummonsSeen.Add("Shiva");
            var garnet = Unit("Garnet", mp: casterMP);
            var wolf = Unit("Wolf");
            var engine = new BattleEngine([garnet], [wolf], EncounterKind.Normal,
                new RiggedDice(0, 0, 200, 0), world: world);
            return engine.ExecuteAbility(garnet, wolf, Shiva(mp: 20)).Result.Damage;
        }

        Assert.Equal(400, DamageWith(100));   // 100 > 40, so 230: short
        Assert.Equal(600, DamageWith(40));    // 40 is not > 40, so 170: full
    }

    [Fact]
    public void The_sightings_survive_a_save()
    {
        var path = Path.Combine(Path.GetTempPath(), $"summons-{Guid.NewGuid():N}.json");
        try
        {
            var world = new WorldState();
            world.SummonsSeen.Add("Shiva");
            world.SummonsSeen.Add("Bahamut");
            world.Save(path);

            var loaded = WorldState.Load(path);

            Assert.Contains("Shiva", loaded.SummonsSeen);
            Assert.Contains("Bahamut", loaded.SummonsSeen);
            Assert.DoesNotContain("Odin", loaded.SummonsSeen);
        }
        finally
        {
            File.Delete(path);
        }
    }
    
    // ---- the decision is made on the COMMAND, whatever the target turns out to be ----

    [Fact]
    public void Phoenix_records_its_sighting_even_when_it_revives_an_ally()
    {
        var world = new WorldState();
        var eiko = Unit("Eiko");
        var vivi = Unit("Vivi");
        vivi.CurrentHP = 0;
        var engine = new BattleEngine([eiko, vivi], [Unit("Wolf")], EncounterKind.Normal,
            new RiggedDice(0, 0, 0), world: world);

        engine.ExecuteAbility(eiko, vivi, new BattleAbility(
            "Phoenix", AbilityScript.Phoenix, 30, Element.None, 0, 0, 20, ForDead: true));

        Assert.True(vivi.IsAlive);
        Assert.Contains("Phoenix", world.SummonsSeen);
    }

    [Fact]
    public void Atomos_records_its_sighting_even_when_the_target_guards()
    {
        var world = new WorldState();
        var garnet = Unit("Garnet");
        var wolf = Unit("Wolf");
        wolf.GuardElements = Element.Thunder;
        var engine = new BattleEngine([garnet], [wolf], EncounterKind.Normal,
            new RiggedDice(0, 0), world: world);

        var e = engine.ExecuteAbility(garnet, wolf, new BattleAbility(
            "Atomos", AbilityScript.Atomos, 30, Element.Thunder, 0, 0, 20));

        Assert.Equal(AttackOutcome.Guarded, e.Result.Outcome);
        Assert.Equal(9999, wolf.CurrentHP);            // nothing landed
        Assert.Contains("Atomos", world.SummonsSeen);  // but the summon was still seen
    }
}
