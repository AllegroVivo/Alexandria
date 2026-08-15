using Alexandria.Battle;

namespace Alexandria.Battle.Tests;

public class MagicAttackTests
{
    private static BattleUnit Mage() => new()
    {
        Name = "Mage", Level = 13, MaxHP = 100, MaxMP = 50, CurrentHP = 100, CurrentMP = 50,
        Speed = 16, Strength = 12, Magic = 24, Spirit = 19,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
    };

    private static BattleUnit Wolf() => new()
    {
        Name = "Wolf", Level = 14, MaxHP = 899, MaxMP = 464, CurrentHP = 899, CurrentMP = 464,
        Speed = 21, Strength = 11, Magic = 11, Spirit = 16,
        PhysicalDefense = 10, PhysicalEvade = 3, MagicalDefense = 10, MagicalEvade = 4,
    };

    [Fact]
    public void Fire_burns_exactly_as_the_formula_says()
    {
        // attack roll 2 → range 1+((13+24)>>3)=5 → attack = 24+2 = 26
        // damage = 26 × max(14−10, 1) = 104     (Fire power 14 vs MDef 10)
        var result = MagicAttack.Execute(Mage(), Wolf(), spellPower: 14, new RiggedDice(2));
        Assert.Equal(AttackOutcome.Hit, result.Outcome);
        Assert.Equal(104, result.Damage);
    }

    [Fact]
    public void Multi_target_halves_the_attack_side()
    {
        // attack 26 → >>1 = 13 → damage = 13 × 4 = 52
        var result = MagicAttack.Execute(Mage(), Wolf(), 14, new RiggedDice(2), multiTarget: true);
        Assert.Equal(52, result.Damage);
    }
}
