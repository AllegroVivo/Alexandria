using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

public class WeaponStatusTests
{
    // Spirit 0 keeps the crit block out of the dice (q = Spirit >> 2) and HasTrance stays
    // false, so a swing costs exactly three rolls: accuracy, evade, damage variance.
    private const Int32 Gauges = 2;

    private static BattleUnit Vivi(SupportAbility supports, Int32 rate,
        BattleStatus status = BattleStatus.Silence) => new()
    {
        Name = "Vivi", Level = 14, MaxHP = 200, MaxMP = 60, CurrentHP = 200, CurrentMP = 60,
        Speed = 16, Strength = 12, Magic = 19, Spirit = 0,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        AttackPower = 12, HasTrance = false,
        Supports = supports, WeaponStatus = status, WeaponStatusRate = rate
    };

    private static BattleUnit Wolf() => new()
    {
        Name = "Wolf", Level = 14, MaxHP = 899, MaxMP = 0, CurrentHP = 899, CurrentMP = 0,
        Speed = 21, Strength = 11, Magic = 11, Spirit = 0,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        AttackPower = 10, HasTrance = false
    };

    // gauges, then accuracy / evade / variance, then the rider's roll
    private static RiggedDice Dice(Int32 rider) => new(0, 0, 0, 0, 0, rider);

    [Fact]
    public void The_rider_lands_below_the_rate()
    {
        var vivi = Vivi(SupportAbility.AddStatus, rate: 20);
        var wolf = Wolf();
        var engine = new BattleEngine([vivi], [wolf], EncounterKind.Normal, Dice(10));

        var e = engine.ExecuteBasicAttack(vivi, wolf);

        Assert.Equal(BattleStatus.Silence, e.StatusesAdded);
        Assert.True(wolf.Has(BattleStatus.Silence));
    }

    [Fact]
    public void The_rider_fails_at_exactly_the_rate()
    {
        // rate <= roll fails, the same boundary MagicAttack.TryRiderStatuses uses
        var vivi = Vivi(SupportAbility.AddStatus, rate: 20);
        var wolf = Wolf();
        var engine = new BattleEngine([vivi], [wolf], EncounterKind.Normal, Dice(20));

        Assert.Equal(0, (Int32)engine.ExecuteBasicAttack(vivi, wolf).StatusesAdded);
        Assert.False(wolf.Has(BattleStatus.Silence));
    }

    [Fact]
    public void Without_Add_Status_the_weapon_never_rolls_at_all()
    {
        // the queue holds only the gauges and the swing: a rider roll would throw
        var vivi = Vivi(SupportAbility.None, rate: 100);
        var wolf = Wolf();
        var engine = new BattleEngine([vivi], [wolf], EncounterKind.Normal,
            new RiggedDice(0, 0, 0, 0, 0));

        Assert.Equal(0, (Int32)engine.ExecuteBasicAttack(vivi, wolf).StatusesAdded);
    }

    [Fact]
    public void An_ability_that_swings_a_weapon_is_not_the_Attack_command()
    {
        // ExecuteAbility routes through the verb table, which calls ExecuteAttack directly —
        // so no rider roll, and again the short queue proves it
        var vivi = Vivi(SupportAbility.AddStatus, rate: 100);
        var wolf = Wolf();
        var engine = new BattleEngine([vivi], [wolf], EncounterKind.Normal,
            new RiggedDice(0, 0, 0, 0, 0));

        var e = engine.ExecuteAbility(vivi, wolf, new BattleAbility(
            "Attack", AbilityScript.EnemyPhysicalAttack, 0, Element.None, 0, 0, 0));

        Assert.Equal(0, (Int32)e.StatusesAdded);
        Assert.False(wolf.Has(BattleStatus.Silence));
    }

    [Fact]
    public void A_missed_swing_carries_no_status()
    {
        // at hitRate 100 the check `100 <= roll % 100` can never be true — a clean swing
        // never misses — so the miss has to come from Blind halving it to 50
        var vivi = Vivi(SupportAbility.AddStatus, rate: 100);
        vivi.Statuses = BattleStatus.Blind;
        var wolf = Wolf();
        var engine = new BattleEngine([vivi], [wolf], EncounterKind.Normal,
            new RiggedDice(0, 0, 50));

        var e = engine.ExecuteBasicAttack(vivi, wolf);

        Assert.Equal(AttackOutcome.Miss, e.Result.Outcome);
        Assert.False(wolf.Has(BattleStatus.Silence));
    }

    [Fact]
    public void An_immune_target_shrugs_the_rider_off()
    {
        // it goes through StatusApplication, not a raw OR, because Memoria calls TryAlterStatuses
        var vivi = Vivi(SupportAbility.AddStatus, rate: 100);
        var wolf = Wolf();
        wolf.ImmuneStatuses = BattleStatus.Silence;
        var engine = new BattleEngine([vivi], [wolf], EncounterKind.Normal, Dice(0));

        Assert.Equal(0, (Int32)engine.ExecuteBasicAttack(vivi, wolf).StatusesAdded);
        Assert.False(wolf.Has(BattleStatus.Silence));
    }
}
