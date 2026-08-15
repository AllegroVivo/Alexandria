using Alexandria.Battle;

namespace Alexandria.Battle.Tests;

public class StatusRiderTests
{
    private static BattleUnit Vivi() => new()
    {
        Name = "Vivi", Level = 14, MaxHP = 200, MaxMP = 60, CurrentHP = 200, CurrentMP = 60,
        Speed = 16, Strength = 10, Magic = 19, Spirit = 14,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
    };

    private static BattleUnit Wolf(BattleStatus statuses = 0) => new()
    {
        Name = "Wolf", Level = 14, MaxHP = 899, MaxMP = 0, CurrentHP = 899, CurrentMP = 0,
        Speed = 21, Strength = 11, Magic = 11, Spirit = 16,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 4, MagicalEvade = 0,
        Statuses = statuses,
    };

    [Fact]
    public void Rider_lands_below_the_rate()
    {
        // Bio at rate 20: roll 10 → 20 > 10, poison sticks
        var wolf = Wolf();
        var added = MagicAttack.TryRiderStatuses(wolf, (UInt32)BattleStatus.Poison, 20, new RiggedDice(10));
        Assert.Equal(BattleStatus.Poison, added);
        Assert.True(wolf.Has(BattleStatus.Poison));
    }

    [Fact]
    public void Rider_fails_at_exactly_the_rate()
    {
        // vanilla is strict: HitRate > roll, so roll 20 vs rate 20 misses
        var wolf = Wolf();
        var added = MagicAttack.TryRiderStatuses(wolf, (UInt32)BattleStatus.Poison, 20, new RiggedDice(20));
        Assert.Equal((BattleStatus)0, added);
        Assert.False(wolf.Has(BattleStatus.Poison));
    }

    [Fact]
    public void No_status_set_means_no_roll()
    {
        // an empty rig: if this consumed a die, RiggedDice would blow up — Fire spends nothing
        var wolf = Wolf();
        var added = MagicAttack.TryRiderStatuses(wolf, 0, 20, new RiggedDice());
        Assert.Equal((BattleStatus)0, added);
    }

    [Fact]
    public void Shell_halves_bio_damage_but_not_the_poison_chance()
    {
        // attack = 19 + 0 % (1 + ((14+19)>>3)) = 19, Shell −1 → 9; damage = 9 × (42−4) = 342
        // status die 19 → 20 > 19: poison lands anyway
        var wolf = Wolf(BattleStatus.Shell);
        var dice = new RiggedDice(0, 19);

        var result = MagicAttack.Execute(Vivi(), wolf, spellPower: 42, dice);
        var added = MagicAttack.TryRiderStatuses(wolf, (UInt32)BattleStatus.Poison, 20, dice);

        Assert.Equal(342, result.Damage);
        Assert.Equal(BattleStatus.Poison, added);
    }
}