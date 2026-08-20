using Alexandria.Battle;
using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

public class ElementTests
{
    private static BattleUnit Vivi() => new()
    {
        Name = "Vivi", Level = 14, MaxHP = 200, MaxMP = 60, CurrentHP = 200, CurrentMP = 60,
        Speed = 16, Strength = 10, Magic = 19, Spirit = 14,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
    };

    private static BattleUnit Wolf() => new()
    {
        Name = "Wolf", Level = 14, MaxHP = 899, MaxMP = 0, CurrentHP = 899, CurrentMP = 0,
        Speed = 21, Strength = 11, Magic = 11, Spirit = 16,
        PhysicalDefense = 10, PhysicalEvade = 0, MagicalDefense = 4, MagicalEvade = 0,
    };

    [Fact]
    public void Weakness_amplifies_by_half()
    {
        // attack = 19 + 0 % 5 = 19; weak +1 → 19×3>>1 = 28; damage = 28 × (29−4) = 700
        var wolf = Wolf();
        wolf.WeakElements = Element.Fire;
        var r = MagicAttack.Execute(Vivi(), wolf, 29, new RiggedDice(0), element: Element.Fire);
        Assert.Equal(700, r.Damage);
    }

    [Fact]
    public void Half_cuts_the_attack_side()
    {
        // attack 19 >> 1 = 9; damage = 9 × 25 = 225
        var wolf = Wolf();
        wolf.HalfElements = Element.Cold;
        var r = MagicAttack.Execute(Vivi(), wolf, 29, new RiggedDice(0), element: Element.Cold);
        Assert.Equal(225, r.Damage);
    }

    [Fact]
    public void Guard_blocks_everything_including_the_rider()
    {
        // empty dice after the attack roll: if the rider rolled, RiggedDice would blow up
        var vivi = Vivi();
        var wolf = Wolf();
        wolf.GuardElements = Element.Fire;
        var engine = new BattleEngine([vivi], [wolf], EncounterKind.Preemptive, new RiggedDice(0));

        var e = engine.ExecuteAbility(vivi, wolf, new BattleAbility(
            "Fire", AbilityScript.MagicAttack, 42, Element.Fire, 20, BattleStatus.Poison, 18));
        
        Assert.Equal(AttackOutcome.Guarded, e.Result.Outcome);
        Assert.Equal(899, wolf.CurrentHP);
        Assert.False(wolf.Has(BattleStatus.Poison));
    }

    [Fact]
    public void Absorb_heals_for_the_undefended_amount()
    {
        // absorb zeroes defense: heal = 19 × 29 (not 25) = 551; 500 + 551 caps at 899
        var vivi = Vivi();
        var wolf = Wolf();
        wolf.AbsorbElements = Element.Darkness;
        wolf.CurrentHP = 500;
        var engine = new BattleEngine([vivi], [wolf], EncounterKind.Preemptive, new RiggedDice(0));

        var e = engine.ExecuteAbility(vivi, wolf, new BattleAbility(
            "Darkness", AbilityScript.MagicAttack, 29, Element.Darkness, 0, 0, 6));
        
        Assert.Equal(AttackOutcome.Absorbed, e.Result.Outcome);
        Assert.Equal(899, wolf.CurrentHP);
    }

    [Fact]
    public void Earth_magic_passes_under_floating_targets()
    {
        var wolf = Wolf();
        wolf.Statuses = BattleStatus.Float;
        var r = MagicAttack.Execute(Vivi(), wolf, 30, new RiggedDice(0), element: Element.Earth);
        Assert.Equal(AttackOutcome.Miss, r.Outcome);
    }

    [Fact]
    public void Fire_thaws_a_frozen_target()
    {
        var wolf = Wolf();
        wolf.Statuses = BattleStatus.Freeze;
        MagicAttack.Execute(Vivi(), wolf, 14, new RiggedDice(0), element: Element.Fire);
        Assert.False(wolf.Has(BattleStatus.Freeze));
    }

    [Fact]
    public void Elemental_weapon_meets_weakness()
    {
        // player physical: hit 0, evade 50, attack die 2 → 10+2=12, crit q die 0/crit 99 → no
        // weak +1 → 12×3>>1 = 18; damage = 18 × max(22−10, 1) = 216
        var vivi = Vivi();
        vivi.WeaponElement = Element.Fire;
        var wolf = Wolf();
        wolf.WeakElements = Element.Fire;
        var r = PhysicalAttack.Execute(vivi, wolf, 22, new RiggedDice(0, 50, 2, 0, 99),
            element: vivi.WeaponElement);
        Assert.Equal(216, r.Damage);
    }
}