using Alexandria.Formats;
using Alexandria.Rules;

namespace Alexandria.Rules.Tests;

public class SpellbookTests
{
    private static readonly RulesDB Db = RulesDB.Load(FF9Data.Data);

    [Fact]
    public void Ability_arrays_parse_and_drop_the_zero_sentinel()
    {
        Assert.Empty(AbilityRef.ParseArray("0"));
        Assert.Equal(
            new[] { new AbilityRef(AbilityKind.Active, 102), new AbilityRef(AbilityKind.Active, 101) },
            AbilityRef.ParseArray("AA:102, AA:101"));
    }

    [Fact]
    public void Mage_staff_teaches_fire()
    {
        var ability = Assert.Single(Db.Items[70].TaughtAbilities);
        Assert.Equal(new AbilityRef(AbilityKind.Active, 25), ability);
    }

    [Fact]
    public void Vivis_starting_spellbook_is_fire_and_only_fire()
    {
        // staff and hat both teach Fire; the learnable-list intersection collapses it to one entry
        var spell = Assert.Single(Spellbook.For(Db, "Vivi"));
        Assert.Equal("Fire", spell.Name);
    }

    [Fact]
    public void Doomsday_requires_learning()
    {
        Assert.DoesNotContain(Spellbook.For(Db, "Vivi"), s => s.Name == "Doomsday");
        Assert.Contains(Spellbook.For(Db, "Vivi", new HashSet<Int32> { 48 }), s => s.Name == "Doomsday");
    }

    [Fact]
    public void Zidane_starts_knowing_only_flee()
    {
        var skill = Assert.Single(Spellbook.For(Db, "Zidane"));
        Assert.Equal("Flee", skill.Name);
    }

    [Fact]
    public void Steiners_kit_teaches_no_spells()
        => Assert.Empty(Spellbook.For(Db, "Steiner"));
}
