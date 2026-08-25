using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

public class StealTests
{
    // Level 10 + Spirit 5 = reach 15 against a level 10 target, so the gate is decided
    // purely by which pair of rigged rolls we queue.
    private static BattleUnit Thief(SupportAbility supports = SupportAbility.None) => new()
    {
        Name = "Zidane", Level = 10, MaxHP = 500, MaxMP = 0, CurrentHP = 500, CurrentMP = 0,
        Speed = 20, Strength = 10, Magic = 0, Spirit = 5,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        AttackPower = 10, HasTrance = false, Supports = supports
    };

    private static BattleUnit Mark(params ItemID[] slots) => new()
    {
        Name = "Zaghnol", Level = 10, MaxHP = 500, MaxMP = 0, CurrentHP = 500, CurrentMP = 0,
        Speed = 20, Strength = 10, Magic = 0, Spirit = 0,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        AttackPower = 10, HasTrance = false,
        StealItems = slots
    };

    private static readonly BattleAbility Steal =
        new("Steal", AbilityScript.Steal, 0, Element.None, 0, 0, 0);

    // A Normal encounter spends two rolls placing the gauges before anything else happens.
    private const Int32 Gauges = 2;

    [Fact]
    public void An_enemy_carrying_nothing_is_never_even_attempted()
    {
        // the queue holds ONLY the gauge rolls: any dice the verb drew would throw
        var world = new WorldState();
        var mark = Mark(ItemID.NoItem, ItemID.NoItem, ItemID.NoItem, ItemID.NoItem);
        var zidane = Thief();
        var engine = new BattleEngine([zidane], [mark], EncounterKind.Normal,
            new RiggedDice(0, 0), world: world);

        var e = engine.ExecuteAbility(zidane, mark, Steal);

        Assert.Equal(AttackOutcome.Miss, e.Result.Outcome);
        Assert.Null(e.ItemMoved);
        Assert.Equal(0, world.Thefts);
    }

    [Fact]
    public void Failing_to_get_in_takes_nothing_and_counts_nothing()
    {
        // 0 % 15 = 0 is not >= 1 % 10 = 1, so the break-in fails and no slot is ever rolled
        var world = new WorldState { Thefts = 179 };
        var mark = Mark(ItemID.Potion, ItemID.Ether, ItemID.Elixir, ItemID.Ore);
        var zidane = Thief();
        var engine = new BattleEngine([zidane], [mark], EncounterKind.Normal,
            new RiggedDice(0, 0, 0, 1), world: world);

        var e = engine.ExecuteAbility(zidane, mark, Steal);

        Assert.Equal(AttackOutcome.Miss, e.Result.Outcome);
        Assert.Equal(179, world.Thefts);
        Assert.Equal(ItemID.Ore, mark.StealItems[3]);
        Assert.Empty(world.Items.Contents);
    }

    [Fact]
    public void The_rarest_slot_is_tried_first_and_empties_when_taken()
    {
        // gate passes on (1, 0); slot 3 then rolls 0, which is under its rate of 1
        var world = new WorldState { Thefts = 179 };
        var mark = Mark(ItemID.Potion, ItemID.Ether, ItemID.Elixir, ItemID.Ore);
        var zidane = Thief();
        var engine = new BattleEngine([zidane], [mark], EncounterKind.Normal,
            new RiggedDice(0, 0, 1, 0, 0), world: world);

        var e = engine.ExecuteAbility(zidane, mark, Steal);

        Assert.Equal(ItemID.Ore, e.ItemMoved);
        Assert.Equal(1, world.Items[ItemID.Ore]);
        Assert.Equal(ItemID.NoItem, mark.StealItems[3]);
        Assert.Equal(180, world.Thefts);
    }

    [Fact]
    public void Slot_zero_can_never_refuse()
    {
        // 3, 2 and 1 all roll exactly their rate — which fails, since the test is <, not <=
        // — and slot 0 then rolls 255, the highest Rand8 can produce, against a rate of 256
        var world = new WorldState();
        var mark = Mark(ItemID.Potion, ItemID.Ether, ItemID.Elixir, ItemID.Ore);
        var zidane = Thief();
        var engine = new BattleEngine([zidane], [mark], EncounterKind.Normal,
            new RiggedDice(0, 0, 1, 0, 1, 16, 64, 255), world: world);

        var e = engine.ExecuteAbility(zidane, mark, Steal);

        Assert.Equal(ItemID.Potion, e.ItemMoved);
        Assert.Equal(1, world.Thefts);
    }

    [Fact]
    public void Winning_the_roll_on_an_EMPTY_rare_slot_wastes_the_whole_steal()
    {
        // the quirk: vanilla rolls before it looks, so the Potion in slot 0 is never reached
        var world = new WorldState();
        var mark = Mark(ItemID.Potion, ItemID.NoItem, ItemID.NoItem, ItemID.NoItem);
        var zidane = Thief();
        var engine = new BattleEngine([zidane], [mark], EncounterKind.Normal,
            new RiggedDice(0, 0, 1, 0, 0), world: world);

        var e = engine.ExecuteAbility(zidane, mark, Steal);

        Assert.Equal(AttackOutcome.Miss, e.Result.Outcome);
        Assert.Equal(ItemID.Potion, mark.StealItems[0]);   // still there, untouched
        Assert.Equal(0, world.Thefts);
        Assert.Empty(world.Items.Contents);
    }

    [Fact]
    public void Nothing_is_stealable_twice()
    {
        var world = new WorldState();
        var mark = Mark(ItemID.NoItem, ItemID.NoItem, ItemID.NoItem, ItemID.Ore);
        var zidane = Thief();
        var engine = new BattleEngine([zidane], [mark], EncounterKind.Normal,
            new RiggedDice(0, 0, 1, 0, 0), world: world);

        Assert.Equal(ItemID.Ore, engine.ExecuteAbility(zidane, mark, Steal).ItemMoved);

        // the queue is now exhausted, so the second attempt must short-circuit on the guard
        var again = engine.ExecuteAbility(zidane, mark, Steal);

        Assert.Equal(AttackOutcome.Miss, again.Result.Outcome);
        Assert.Equal(1, world.Thefts);
    }
    
        // ---- the two support abilities ----

    [Fact]
    public void Bandit_skips_the_break_in_entirely()
    {
        // exactly three dice: two gauges and slot 3's roll. A gate that ran would
        // eat the third and then throw on an empty queue.
        var world = new WorldState();
        var mark = Mark(ItemID.Potion, ItemID.Ether, ItemID.Elixir, ItemID.Ore);
        var zidane = Thief(SupportAbility.Bandit);
        var engine = new BattleEngine([zidane], [mark], EncounterKind.Normal,
            new RiggedDice(0, 0, 0), world: world);

        var e = engine.ExecuteAbility(zidane, mark, Steal);

        Assert.Equal(ItemID.Ore, e.ItemMoved);
        Assert.Equal(1, world.Thefts);
    }

    [Fact]
    public void Master_Thief_floors_the_rarest_slot_at_thirty_two_in_two_hundred_and_fifty_six()
    {
        // a roll of 20 fails the bare rate of 1 outright, and clears the floor of 32
        var world = new WorldState();
        var mark = Mark(ItemID.Potion, ItemID.Ether, ItemID.Elixir, ItemID.Ore);
        var zidane = Thief(SupportAbility.MasterThief);
        var engine = new BattleEngine([zidane], [mark], EncounterKind.Normal,
            new RiggedDice(0, 0, 1, 0, 20), world: world);

        Assert.Equal(ItemID.Ore, engine.ExecuteAbility(zidane, mark, Steal).ItemMoved);
    }

    [Fact]
    public void Master_Thief_looks_before_it_rolls_and_so_never_wastes_a_steal()
    {
        // the exact case that wastes a plain steal: three empty slots are skipped
        // WITHOUT a die, and slot 0's 255-against-256 takes the Potion instead
        var world = new WorldState();
        var mark = Mark(ItemID.Potion, ItemID.NoItem, ItemID.NoItem, ItemID.NoItem);
        var zidane = Thief(SupportAbility.MasterThief);
        var engine = new BattleEngine([zidane], [mark], EncounterKind.Normal,
            new RiggedDice(0, 0, 1, 0, 255), world: world);

        var e = engine.ExecuteAbility(zidane, mark, Steal);

        Assert.Equal(ItemID.Potion, e.ItemMoved);
        Assert.Equal(ItemID.NoItem, mark.StealItems[0]);
    }
}

