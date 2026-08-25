using Alexandria.Formats;
using Alexandria.Rules;

namespace Alexandria.Rules.Tests;

public class LoadoutTests
{
    private static readonly RulesDB Db = RulesDB.Load(FF9Data.Data);

    private const Int32 Weapon = 0, Wrist = 2;
    private const Int32 MythrilDagger = 3, LeatherWrist = 89, MagicArmlet = 93, ThiefGloves = 98;

    [Fact]
    public void A_support_ability_is_not_the_active_of_the_same_number()
    {
        // the collision is in the shipped data: Garnet learns active 19, never support 19,
        // and Leather Wrist teaches support 19 — worth +60 to nobody
        var learnable = Db.Learnables["Garnet"].Select(a => a.Ability).ToHashSet();
        Assert.Contains(new AbilityRef(AbilityKind.Active, 19), learnable);
        Assert.DoesNotContain(new AbilityRef(AbilityKind.Support, 19), learnable);
        Assert.Contains(new AbilityRef(AbilityKind.Support, 19), Db.Items[LeatherWrist].TaughtAbilities);

        // and the consequence: with Kind respected, Garnet's wrist pick changes
        Assert.Equal(MagicArmlet, Loadout.Best(Db, "Garnet")[Wrist]);
    }

    [Fact]
    public void The_thief_gear_never_wins_a_slot_on_its_own_merits()
    {
        // Best means STRONGEST, and both thief items are early or defensively weak
        var zidane = Loadout.Best(Db, "Zidane");
        Assert.NotEqual(MythrilDagger, zidane[Weapon]);
        Assert.NotEqual(ThiefGloves, zidane[Wrist]);
    }

    [Fact]
    public void Insisting_on_gear_takes_the_slot_it_occupies_and_leaves_the_rest_alone()
    {
        var best = Loadout.Best(Db, "Zidane");
        var forced = Loadout.Best(Db, "Zidane", insist: [MythrilDagger, ThiefGloves]);

        Assert.Equal(MythrilDagger, forced[Weapon]);
        Assert.Equal(ThiefGloves, forced[Wrist]);
        Assert.Equal(best[1], forced[1]);
        Assert.Equal(best[3], forced[3]);
        Assert.Equal(best[4], forced[4]);
    }

    [Fact]
    public void Insisting_on_gear_a_character_cannot_wear_changes_nothing()
    {
        // the Mythril Dagger is Zidane's alone
        Assert.Equal(Loadout.Best(Db, "Vivi"),
            Loadout.Best(Db, "Vivi", insist: [MythrilDagger]));
    }

    [Fact]
    public void The_thief_kit_puts_Bandit_and_Master_Thief_within_reach()
    {
        // the point of the override: debt #38 was that these two could never be observed
        var slots = Loadout.Best(Db, "Zidane", insist: [MythrilDagger, ThiefGloves]);
        var kit = EquipmentKit.Resolve(slots, Db.Items, Db.Weapons, Db.Armors, Db.GrowthStats);
        var stats = Db.BaseStats.First(c => c.Name == "Zidane");
        var grown = GrowthSimulator.LevelUpTo(60, GrowthBonuses.None, kit.GrowthPerLevel);
        var stones = StatGrowth.MagicStones(stats, 60, grown);

        var supports = SupportSet.Equip(kit.TaughtAbilities, Db.Gems, stones);

        Assert.True(supports.HasFlag(SupportAbility.Bandit));
        Assert.True(supports.HasFlag(SupportAbility.MasterThief));
    }
}
