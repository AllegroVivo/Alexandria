using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

public class KillTallyTests
{
    private static BattleUnit Unit(String name, EnemyCategory category = EnemyCategory.None, Int32 gil = 0) => new()
    {
        Name = name, Level = 10, MaxHP = 500, MaxMP = 0, CurrentHP = 500, CurrentMP = 0,
        Speed = 20, Strength = 10, Magic = 10, Spirit = 0,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        AttackPower = 10, HasTrance = false,
        Category = category, BonusGil = gil
    };

    // ---- the tally itself ----

    [Fact]
    public void A_creature_of_two_kinds_counts_under_both()
    {
        // btl_mot.cs:341 walks all eight bits rather than picking one
        var kills = new KillCounts();
        kills.Record(EnemyCategory.Dragon | EnemyCategory.Undead);

        Assert.Equal(1, kills[EnemyCategory.Dragon]);
        Assert.Equal(1, kills[EnemyCategory.Undead]);
        Assert.Equal(0, kills[EnemyCategory.Beast]);
    }

    [Fact]
    public void A_creature_of_no_kind_counts_nowhere()
    {
        var kills = new KillCounts();
        kills.Record(EnemyCategory.None);

        Assert.All(Enum.GetValues<EnemyCategory>().Where(c => c != EnemyCategory.None),
            c => Assert.Equal(0, kills[c]));
    }

    [Fact]
    public void A_tally_stops_at_nine_thousand_nine_hundred_and_ninety_nine()
    {
        var kills = new KillCounts { [EnemyCategory.Beast] = KillCounts.Cap };
        kills.Record(EnemyCategory.Beast);

        Assert.Equal(KillCounts.Cap, kills[EnemyCategory.Beast]);
    }

    // ---- the engine banking it ----

    [Fact]
    public void A_dead_enemy_is_tallied_once_however_often_we_look()
    {
        var world = new WorldState();
        var dragon = Unit("Abomination", EnemyCategory.Dragon);
        var engine = new BattleEngine([Unit("Freya")], [dragon], EncounterKind.Normal, new Random(1), world: world);

        dragon.CurrentHP = 0;
        engine.Settle();
        engine.Settle();
        engine.Settle();

        Assert.Equal(1, world.Kills[EnemyCategory.Dragon]);
    }

    [Fact]
    public void A_dragon_killed_this_battle_arms_Dragons_Crest_before_it_ends()
    {
        // the whole point of banking per-death rather than at victory
        var world = new WorldState { Kills = { [EnemyCategory.Dragon] = 5 } };
        var dragon = Unit("Abomination", EnemyCategory.Dragon);
        var survivor = Unit("Grand Dragon", EnemyCategory.Dragon);
        var freya = Unit("Freya");
        var engine = new BattleEngine([freya], [dragon, survivor], EncounterKind.Normal, new Random(1), world: world);

        dragon.CurrentHP = 0;
        engine.TickUntilReady();

        var e = engine.ExecuteAbility(freya, survivor,
            new BattleAbility("Dragon's Crest", AbilityScript.DragonCrest, 0, Element.None, 0, 0, 0));

        Assert.Equal(6, world.Kills[EnemyCategory.Dragon]);
        Assert.Equal(36, e.Result.Damage);
    }

    // ---- the purse ----

    [Fact]
    public void Victory_pays_the_gil_of_the_enemies_actually_killed()
    {
        // an enemy swallowed whole never dies, so btl_sys.SetBonus never banks it
        var world = new WorldState { Gil = 100 };
        var killed = Unit("Zaghnol", EnemyCategory.Beast, gil: 300);
        var swallowed = Unit("Axolotl", EnemyCategory.Beast, gil: 500);
        var engine = new BattleEngine([Unit("Quina")], [killed, swallowed],
            EncounterKind.Normal, new Random(1), world: world);

        killed.CurrentHP = 0;
        swallowed.IsRemoved = true;
        engine.Settle();

        Assert.True(engine.Victory);
        Assert.Equal(400, world.Gil);
        Assert.Equal(1, world.Kills[EnemyCategory.Beast]);
    }

    [Fact]
    public void Running_away_forfeits_the_purse_but_never_the_kills()
    {
        var world = new WorldState { Gil = 100 };
        var slain = Unit("Zaghnol", EnemyCategory.Beast, gil: 300);
        var engine = new BattleEngine([Unit("Zidane")], [slain], EncounterKind.Normal, new Random(1), world: world);

        slain.CurrentHP = 0;
        engine.Escaped = true;
        engine.Settle();

        Assert.Equal(100, world.Gil);
        Assert.Equal(1, world.Kills[EnemyCategory.Beast]);
    }

    [Fact]
    public void Settling_twice_pays_once()
    {
        var world = new WorldState { Gil = 100 };
        var slain = Unit("Zaghnol", EnemyCategory.Beast, gil: 300);
        var engine = new BattleEngine([Unit("Zidane")], [slain], EncounterKind.Normal, new Random(1), world: world);

        slain.CurrentHP = 0;
        engine.Settle();
        engine.Settle();

        Assert.Equal(400, world.Gil);
    }
}
