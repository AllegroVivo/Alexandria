using Alexandria.Battle;
using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

public class InventoryTests
{
    private const Int32 Potion = 236;
    private const Int32 Ore = 254;

    // Spirit 0 keeps the crit block out of the dice (q = Spirit >> 2), and no evade means
    // the physical rolls can never turn a steal into a miss — so these tests can use a
    // plain seeded Random and still be deterministic about what they assert.
    private static BattleUnit Fighter(String name) => new()
    {
        Name = name, Level = 10, MaxHP = 500, MaxMP = 0, CurrentHP = 500, CurrentMP = 0,
        Speed = 20, Strength = 10, Magic = 0, Spirit = 0,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        AttackPower = 10, HasTrance = false,
    };

    // ---- the bag itself ----

    [Fact]
    public void Stacks_add_up_and_stop_at_ninety_nine()
    {
        var bag = new Inventory();
        bag.Add(Potion, 60);
        bag.Add(Potion, 60);

        Assert.Equal(Inventory.StackMax, bag[Potion]);
    }

    [Fact]
    public void Taking_what_you_do_not_have_reports_failure_and_changes_nothing()
    {
        var bag = new Inventory();
        bag.Add(Potion, 2);

        Assert.False(bag.Remove(Potion, 3));
        Assert.Equal(2, bag[Potion]);
        Assert.False(bag.Remove(Ore));
        Assert.Equal(0, bag[Ore]);
    }

    [Fact]
    public void An_item_taken_to_zero_leaves_the_bag_entirely()
    {
        // the doc comment's claim: absent, not a zero-count row
        var bag = new Inventory();
        bag.Add(Potion);

        Assert.True(bag.Remove(Potion));
        Assert.Equal(0, bag[Potion]);
        Assert.Empty(bag.Contents);
    }

    // ---- the scripts that move items ----

    [Fact]
    public void Enemy_steal_takes_the_item_the_ability_rate_names()
    {
        // RemoveItem reads Command.HitRate, so Rate IS the item id (0101_EnemyStealScript.cs:33)
        var world = new WorldState();
        world.Items.Add(Potion, 3);
        var victim = Fighter("Victim");
        var thief = Fighter("Thief");
        var engine = new BattleEngine([victim], [thief], EncounterKind.Normal, new Random(1), world: world);

        var e = engine.ExecuteAbility(thief, victim,
            new BattleAbility("Steal", AbilityScript.EnemySteal, 0, Element.None, Potion, 0, 0));

        Assert.Equal(2, world.Items[Potion]);
        Assert.Equal(Potion, e.ItemMoved);
    }

    [Fact]
    public void Enemy_steal_from_an_empty_bag_lands_but_takes_nothing()
    {
        var world = new WorldState();
        var victim = Fighter("Victim");
        var thief = Fighter("Thief");
        var engine = new BattleEngine([victim], [thief], EncounterKind.Normal, new Random(1), world: world);

        var e = engine.ExecuteAbility(thief, victim,
            new BattleAbility("Steal", AbilityScript.EnemySteal, 0, Element.None, Potion, 0, 0));

        Assert.Equal(AttackOutcome.Hit, e.Result.Outcome);
        Assert.Null(e.ItemMoved);
    }

    [Fact]
    public void Mug_steals_alongside_damage_that_never_misses()
    {
        var world = new WorldState();
        world.Items.Add(Potion);
        var victim = Fighter("Victim");
        var thief = Fighter("Thief");
        var engine = new BattleEngine([victim], [thief], EncounterKind.Normal, new Random(1), world: world);

        var e = engine.ExecuteAbility(thief, victim,
            new BattleAbility("Mug", AbilityScript.EnemyMug, 10, Element.None, Potion, 0, 0));

        Assert.True(e.Result.Damage > 0);
        Assert.Equal(Potion, e.ItemMoved);
        Assert.Empty(world.Items.Contents);
    }

    [Fact]
    public void Jewel_conjures_an_ore_when_it_connects()
    {
        // rate 100 clamps the hit rate to 100, which rand%100 can never reach
        var world = new WorldState();
        var eiko = Fighter("Eiko");
        var wolf = Fighter("Wolf");
        var engine = new BattleEngine([eiko], [wolf], EncounterKind.Normal, new Random(1), world: world);

        var e = engine.ExecuteAbility(eiko, wolf,
            new BattleAbility("Jewel", AbilityScript.Jewel, 0, Element.None, 100, 0, 0));

        Assert.Equal(1, world.Items[Ore]);
        Assert.Equal(Ore, e.ItemMoved);
    }

    [Fact]
    public void Jewel_conjures_nothing_when_it_misses()
    {
        // rate 1 → hit rate 1, and the rigged accuracy roll of 50 fails it
        var world = new WorldState();
        var eiko = Fighter("Eiko");
        var wolf = Fighter("Wolf");
        var engine = new BattleEngine([eiko], [wolf], EncounterKind.Normal, new RiggedDice(50), world: world);

        var e = engine.ExecuteAbility(eiko, wolf,
            new BattleAbility("Jewel", AbilityScript.Jewel, 0, Element.None, 1, 0, 0));

        Assert.Equal(AttackOutcome.Miss, e.Result.Outcome);
        Assert.Empty(world.Items.Contents);
        Assert.Null(e.ItemMoved);
    }
}
