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
    
    [Fact]
    public void The_book_follows_the_kit_it_is_handed()
    {
        // with no learned abilities the book is purely what the gear teaches, so this
        // pins the kit parameter actually being READ — the thing that would break silently
        var best = EquipmentKit.Resolve(Loadout.Best(Db, "Zidane"),
            Db.Items, Db.Weapons, Db.Armors, Db.GrowthStats);
        var thief = EquipmentKit.Resolve(Loadout.Best(Db, "Zidane", insist: [3, 98]),
            Db.Items, Db.Weapons, Db.Armors, Db.GrowthStats);

        var bestBook = Spellbook.For(Db, "Zidane", kit: best).Select(s => s.Name).ToList();
        var thiefBook = Spellbook.For(Db, "Zidane", kit: thief).Select(s => s.Name).ToList();

        // The Tower teaches AA:107 and AA:108; a Mythril Dagger teaches SA:62 and no actives.
        // Thievery is weapon-only so it goes — but Lucky Seven SURVIVES, because the book is a
        // union over every slot and the Thief Hat in the helmet teaches AA:107 too.
        Assert.Contains("Thievery", bestBook);
        Assert.DoesNotContain("Thievery", thiefBook);
        Assert.Contains("Lucky Seven", bestBook);
        Assert.Contains("Lucky Seven", thiefBook);
    }
}
