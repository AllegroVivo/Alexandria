using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

/// <summary>The bag's rules, checked against the reference rather than assumed (debt #27).
/// InventoryTests covers the bag's own arithmetic; these pin the places where a battle
/// script's behaviour depends on it.</summary>
public class InventoryRulesTests
{
    private static BattleUnit Unit(String name, params ItemID[] steals) => new()
    {
        Name = name, Level = 10, MaxHP = 600, MaxMP = 100, CurrentHP = 600, CurrentMP = 100,
        Speed = 20, Strength = 10, Magic = 20, Spirit = 5,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        AttackPower = 10, HasTrance = false, StealItems = steals
    };

    [Fact]
    public void Stealing_into_a_full_stack_loses_the_item_but_still_costs_the_enemy_its_slot()
    {
        // mirror: StealItem clears the slot and bumps steal_no BEFORE AddToInventory, and
        // BattleHUD.ItemAdd's `FF9Item_Add(id, 1) == 0` early return only skips a UI refresh
        var world = new WorldState { Thefts = 10 };
        world.Items.Add(ItemID.Ore, 99);
        var zidane = Unit("Zidane");
        var mark = Unit("Zaghnol", ItemID.NoItem, ItemID.NoItem, ItemID.NoItem, ItemID.Ore);
        var engine = new BattleEngine([zidane], [mark], EncounterKind.Normal,
            new RiggedDice(0, 0, 1, 0, 0), world: world);

        var e = engine.ExecuteAbility(zidane, mark,
            new BattleAbility("Steal", AbilityScript.Steal, 0, Element.None, 0, 0, 0));

        Assert.Equal(ItemID.Ore, e.ItemMoved);
        Assert.Equal(99, world.Items[ItemID.Ore]);          // the ore itself is gone for good
        Assert.Equal(ItemID.NoItem, mark.StealItems[3]);    // but the enemy still lost it
        Assert.Equal(11, world.Thefts);                     // and it still counted
    }

    [Fact]
    public void A_thrown_gem_counts_itself_because_it_has_already_left_the_bag()
    {
        // 0074 is `Power * (GetCount(item) + 1)`, and vanilla removes the item at
        // CMD_MODE_SELECT_VFX — before the script counts — so the +1 puts it back
        var world = new WorldState();
        world.Items.Add(ItemID.Opal, 5);
        var garnet = Unit("Garnet");
        var vivi = Unit("Vivi");
        vivi.CurrentHP = 1;
        var engine = new BattleEngine([garnet, vivi], [Unit("Wolf")], EncounterKind.Normal,
            new RiggedDice(0, 0, 0), world: world);

        world.Items.Remove(ItemID.Opal);          // what the runner does before resolving
        var e = engine.ExecuteAbility(garnet, vivi, new BattleAbility(
            "Opal", AbilityScript.ItemGem, 10, Element.None, 0, 0, 0, (Int32)ItemID.Opal));

        Assert.Equal(50, e.Result.Damage);        // 10 * (4 held + 1) — the thrown one counts
    }

    [Fact]
    public void The_bag_holds_as_many_distinct_items_as_it_likes()
    {
        // vanilla's FF9ITEM_MAX = 256 is commented out in ff9item.2.cs — Memoria removed the cap
        var bag = new Inventory();
        foreach (var id in Enum.GetValues<ItemID>().Where(i => i != ItemID.NoItem))
            bag.Add(id);

        Assert.True(bag.Contents.Count() > 200);
    }
}
