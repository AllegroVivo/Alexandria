using Alexandria.Battle;

namespace Alexandria.Battle.Tests;

public class PhysicalAttackTests
{
    private static BattleUnit Zidaneish() => new()
    {
        Name = "Zidane", Level = 1, MaxHP = 105, MaxMP = 36, CurrentHP = 105, CurrentMP = 36,
        Speed = 23, Strength = 21, Magic = 18, Spirit = 23,
        PhysicalDefense = 6, PhysicalEvade = 5, MagicalDefense = 6, MagicalEvade = 3,
    };

    private static BattleUnit TypeC() => new()
    {
        Name = "Type C", Level = 13, MaxHP = 623, MaxMP = 447, CurrentHP = 623, CurrentMP = 447,
        Speed = 21, Strength = 11, Magic = 11, Spirit = 16,
        PhysicalDefense = 10, PhysicalEvade = 3, MagicalDefense = 13, MagicalEvade = 4,
    };

    [Fact]
    public void A_clean_hit_computes_exactly()
    {
        // evade roll 50 → 50%100=50, PEvade 3 not > 50 → hit
        // attack roll 2 → range 1+((1+21)>>3)=3 → attack = 21 + 2%3(=2) = 23
        // crit rolls 0, 50 → q=23>>2=5, 0%5=0 not > 50 → no crit
        // damage = 23 × max(12−10, 1) = 46      (attackPower 12, Type C PDef 10)
        var result = PhysicalAttack.Execute(Zidaneish(), TypeC(), attackPower: 12,
            new RiggedDice(50, 2, 0, 50));

        Assert.Equal(AttackOutcome.Hit, result.Outcome);
        Assert.Equal(46, result.Damage);
    }

    [Fact]
    public void A_critical_doubles_the_attack_side()
    {
        // same as above but crit rolls 4, 3 → 4%5=4 > 3 → crit: attack 46 → damage 92
        var result = PhysicalAttack.Execute(Zidaneish(), TypeC(), 12, new RiggedDice(50, 2, 4, 3));

        Assert.Equal(AttackOutcome.CriticalHit, result.Outcome);
        Assert.Equal(92, result.Damage);
    }

    [Fact]
    public void Evade_beats_the_roll()
    {
        // evade roll 1 → 1%100=1, PEvade 3 > 1 → evaded, no damage
        var result = PhysicalAttack.Execute(Zidaneish(), TypeC(), 12, new RiggedDice(1));

        Assert.Equal(AttackOutcome.Evaded, result.Outcome);
        Assert.Equal(0, result.Damage);
    }

    [Fact]
    public void Damage_caps_at_9999_and_hp_floors_at_zero()
    {
        var target = TypeC();
        var result = PhysicalAttack.Execute(Zidaneish(), target, attackPower: 9999,
            new RiggedDice(50, 2, 0, 50));
        Assert.Equal(9999, result.Damage);

        PhysicalAttack.Apply(target, result.Damage);
        Assert.Equal(0, target.CurrentHP);
        Assert.False(target.IsAlive);
    }
}