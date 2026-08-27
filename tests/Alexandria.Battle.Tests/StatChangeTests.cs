using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

public class StatChangeTests
{
    private static BattleUnit Unit(String name, Int32 strength = 20, Int32 magic = 20) => new()
    {
        Name = name, Level = 10, MaxHP = 600, MaxMP = 100, CurrentHP = 600, CurrentMP = 100,
        Speed = 20, Strength = strength, Magic = magic, Spirit = 0,
        PhysicalDefense = 40, PhysicalEvade = 0, MagicalDefense = 40, MagicalEvade = 0,
        AttackPower = 10, HasTrance = false
    };

    private static BattleAbility Spell(String name, AbilityScript script, Int32 power = 2)
        => new(name, script, power, Element.None, 100, 0, 0);

    [Fact]
    public void Might_raises_Strength_rather_than_erasing_it()
    {
        var caster = Unit("Steiner");
        var ally = Unit("Vivi", strength: 20);
        var engine = new BattleEngine([caster, ally], [Unit("Wolf")], EncounterKind.Normal, new Random(1));

        engine.ExecuteAbility(caster, ally, Spell("Might", AbilityScript.Might));

        Assert.Equal(30, ally.Strength);      // 20 + 20/2
    }

    [Fact]
    public void Might_caps_at_ninety_nine_even_when_that_is_a_cut()
    {
        // 0043 clamps to 99 unconditionally, so a very strong unit LOSES Strength to it
        var caster = Unit("Steiner");
        var ally = Unit("Vivi", strength: 80);
        var engine = new BattleEngine([caster, ally], [Unit("Wolf")], EncounterKind.Normal, new Random(1));

        engine.ExecuteAbility(caster, ally, Spell("Might", AbilityScript.Might));

        Assert.Equal(99, ally.Strength);      // 80 + 40 = 120, clamped
    }

    [Fact]
    public void Absorb_Magic_takes_Magic_and_Absorb_Strength_takes_Strength()
    {
        // 0094 alters "Magic", 0095 alters "Strength" — the two were dispatched swapped
        var caster = Unit("Amarant", strength: 20, magic: 20);
        var wolf = Unit("Wolf", strength: 40, magic: 60);
        var engine = new BattleEngine([caster], [wolf], EncounterKind.Normal, new Random(1));

        engine.ExecuteAbility(caster, wolf, Spell("Absorb Magic", AbilityScript.AbsorbMagic));

        Assert.Equal(30, wolf.Magic);         // halved
        Assert.Equal(40, caster.Magic);       // doubled
        Assert.Equal(40, wolf.Strength);      // untouched
        Assert.Equal(20, caster.Strength);

        engine.ExecuteAbility(caster, wolf, Spell("Absorb Strength", AbilityScript.AbsorbStrength));

        Assert.Equal(20, wolf.Strength);
        Assert.Equal(40, caster.Strength);
        Assert.Equal(30, wolf.Magic);         // still untouched by the other one
    }

    [Fact]
    public void Two_Armour_Breaks_stack_multiplicatively()
    {
        // Pinned deliberately: the debt register claimed vanilla REPLACES the first Break,
        // but 0033 passes `PhysicalDefence / 2` off the CURRENT value and ChangeStat
        // accumulates its diff, so Memoria stacks exactly as we do. Do not "fix" this.
        var caster = Unit("Steiner");
        var wolf = Unit("Wolf");
        var engine = new BattleEngine([caster], [wolf], EncounterKind.Normal,
            new RiggedDice(0, 0, 0, 0, 0, 0));

        engine.ExecuteAbility(caster, wolf, Spell("Armor Break", AbilityScript.ArmorBreak));
        Assert.Equal(20, wolf.PhysicalDefense);

        engine.ExecuteAbility(caster, wolf, Spell("Armor Break", AbilityScript.ArmorBreak));
        Assert.Equal(10, wolf.PhysicalDefense);
    }
}
