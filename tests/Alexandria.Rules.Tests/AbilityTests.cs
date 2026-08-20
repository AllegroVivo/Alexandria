using Alexandria.Formats;
using Alexandria.Rules;

namespace Alexandria.Rules.Tests;

public class AbilityTests
{
    private static readonly String AbilitiesRoot = Path.Combine(FF9Data.Data, @"Characters\Abilities");

    [Fact]
    public void Vivi_learns_the_fire_line_at_the_remembered_costs()
    {
        var vivi = CharacterAbilities.Load(Path.Combine(AbilitiesRoot, "Vivi.csv"));
        Assert.Equal(25, vivi.First(a => a.Ability == new AbilityRef(AbilityKind.Active, 25)).APCost); // Fire
        Assert.Equal(75, vivi.First(a => a.Ability == new AbilityRef(AbilityKind.Active, 27)).APCost); // Firaga
    }

    [Fact]
    public void Auto_Reflect_costs_fifteen_stones()
    {
        var gems = AbilityGem.Load(Path.Combine(AbilitiesRoot, "AbilityGems.csv"));
        Assert.Equal(15, gems[0].Gems); // Auto-Reflect
        Assert.Equal(6, gems[1].Gems);  // Auto-Float
    }

    [Fact]
    public void Every_learnable_passive_has_a_stone_cost()
    {
        var gems = AbilityGem.Load(Path.Combine(AbilitiesRoot, "AbilityGems.csv"));
        foreach (var file in Directory.EnumerateFiles(AbilitiesRoot, "*.csv"))
        {
            if (Path.GetFileName(file) == "AbilityGems.csv")
                continue;
            foreach (var a in CharacterAbilities.Load(file).Where(a => a.Ability.Kind == AbilityKind.Support))
                Assert.True(gems.ContainsKey(a.Ability.ID),
                    $"{Path.GetFileName(file)}: SA:{a.Ability.ID} has no stone cost");
        }
    }
}
