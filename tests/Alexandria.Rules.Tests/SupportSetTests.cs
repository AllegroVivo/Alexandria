using System.Numerics;
using Alexandria.Formats;
using Alexandria.Rules;

namespace Alexandria.Rules.Tests;

public class SupportSetTests
{
    private static readonly Dictionary<Int32, AbilityGem> Gems = new()
    {
        [22] = new AbilityGem("Master Thief", 22, 5, []),
        [25] = new AbilityGem("Add Status", 25, 3, []),
        [62] = new AbilityGem("Bandit", 62, 5, []),
        [63] = new AbilityGem("Void", 63, 20, [])
    };

    private static AbilityRef Sup(Int32 id) => new(AbilityKind.Support, id);

    [Fact]
    public void The_cheapest_abilities_are_equipped_until_the_stones_run_out()
    {
        var set = SupportSet.Equip([Sup(63), Sup(22), Sup(25)], Gems, stones: 8);

        Assert.Equal(SupportAbility.AddStatus | SupportAbility.MasterThief, set);
        Assert.False(set.HasFlag(SupportAbility.Void));   // 20 gems, never affordable here
    }

    [Fact]
    public void A_character_with_no_stones_runs_nothing()
        => Assert.Equal(SupportAbility.None, SupportSet.Equip([Sup(25)], Gems, stones: 2));

    [Fact]
    public void Active_abilities_on_the_same_gear_are_ignored()
    {
        var set = SupportSet.Equip(
            [new AbilityRef(AbilityKind.Active, 22), Sup(62)], Gems, stones: 5);

        Assert.Equal(SupportAbility.Bandit, set);
    }

    [Fact]
    public void The_same_ability_taught_by_two_items_is_paid_for_once()
    {
        // 5 stones buys Bandit; without the Distinct the duplicate would eat the budget
        var set = SupportSet.Equip([Sup(62), Sup(62)], Gems, stones: 5);

        Assert.Equal(SupportAbility.Bandit, set);
    }

    // ---- the claim the whole enum rests on ----

    [Fact]
    public void Every_enum_bit_position_is_a_row_id_in_AbilityGems()
    {
        var gems = AbilityGem.Load(Path.Combine(FF9Data.Data, @"Characters\Abilities\AbilityGems.csv"));
        var positions = Enum.GetValues<SupportAbility>()
            .Where(s => s != SupportAbility.None)
            .Select(s => BitOperations.TrailingZeroCount((UInt64)s))
            .ToList();

        Assert.Equal(64, gems.Count);
        Assert.Equal(64, positions.Distinct().Count());
        Assert.Equal(Enumerable.Range(0, 64), positions.Order());
        Assert.All(positions, p => Assert.True(gems.ContainsKey(p)));

        // the three the engine actually reads, pinned by cost
        Assert.Equal(5, gems[22].Gems);
        Assert.Equal(3, gems[25].Gems);
        Assert.Equal(5, gems[62].Gems);
    }
}