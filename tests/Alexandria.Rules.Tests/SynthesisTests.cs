using Alexandria.Rules;

namespace Alexandria.Rules.Tests;

public class SynthesisTests
{
    private const String DataRoot = @"C:\Dev\Memoria\FF9Data\Data";

    private static IReadOnlyList<SynthesisRecipe> Recipes()
        => SynthesisRecipe.Load(Path.Combine(DataRoot, @"Items\Synthesis.csv"));

    [Fact]
    public void Butterfly_Sword_is_a_Dagger_plus_a_Mage_Masher_for_300_gil()
    {
        var bs = Recipes().First(r => r.Name == "Butterfly Sword");
        Assert.Equal(300u, bs.Price);
        Assert.Equal(7, bs.ResultItemID);
        Assert.Equal(new[] { 1, 2 }, bs.IngredientItemIDs);
    }

    [Fact]
    public void The_Ogre_demands_two_Mage_Mashers()
    {
        Assert.Equal(new[] { 2, 2 }, Recipes().First(r => r.Name == "The Ogre").IngredientItemIDs);
    }

    [Fact]
    public void Every_recipe_references_real_items()
    {
        var items = Item.Load(Path.Combine(DataRoot, @"Items\Items.csv"));
        foreach (var r in Recipes())
        {
            Assert.True(items.ContainsKey(r.ResultItemID), $"{r.Name}: result item {r.ResultItemID} missing");
            foreach (var ingredient in r.IngredientItemIDs)
                Assert.True(items.ContainsKey(ingredient), $"{r.Name}: ingredient {ingredient} missing");
        }
    }
}
