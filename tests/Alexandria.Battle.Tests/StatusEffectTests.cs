using Alexandria.Battle;

namespace Alexandria.Battle.Tests;

public class StatusEffectTests
{
    private static BattleUnit Attacker() => new()
    {
        Name = "A", Level = 1, MaxHP = 100, MaxMP = 0, CurrentHP = 100, CurrentMP = 0,
        Speed = 23, Strength = 21, Magic = 24, Spirit = 23,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
    };

    private static BattleUnit Target(BattleStatus statuses = BattleStatus.None) => new()
    {
        Name = "T", Level = 14, MaxHP = 899, MaxMP = 0, CurrentHP = 899, CurrentMP = 0,
        Speed = 21, Strength = 11, Magic = 11, Spirit = 16,
        PhysicalDefense = 10, PhysicalEvade = 0, MagicalDefense = 10, MagicalEvade = 0,
        Statuses = statuses,
    };

    [Fact]
    public void Protect_halves_the_attack_side()
    {
        // rig: hit 0, evade 50, attack 2 → attack 23 → Protect −1 → 11 → 11×2 = 22
        var r = PhysicalAttack.Execute(Attacker(), Target(BattleStatus.Protect), 12,
            new RiggedDice(0, 50, 2, 0, 50));
        Assert.Equal(22, r.Damage);
    }

    [Fact]
    public void Sleeping_targets_take_amplified_damage()
    {
        // attack 23 → Sleep +1 → 23*3>>1 = 34 → 34×2 = 68
        var r = PhysicalAttack.Execute(Attacker(), Target(BattleStatus.Sleep), 12,
            new RiggedDice(0, 50, 2, 0, 50));
        Assert.Equal(68, r.Damage);
    }

    [Fact]
    public void Blind_attackers_can_miss()
    {
        // hitRate 100 → Blind → 50; roll 60 → 50 <= 60 → miss
        var attacker = Attacker();
        attacker.Statuses = BattleStatus.Blind;
        var r = PhysicalAttack.Execute(attacker, Target(), 12, new RiggedDice(60));
        Assert.Equal(AttackOutcome.Miss, r.Outcome);
    }

    [Fact]
    public void Shell_halves_incoming_magic()
    {
        // attack 24+2=26 → Shell −1 → 13 → 13 × max(14−10,1)=4 → 52
        var r = MagicAttack.Execute(Attacker(), Target(BattleStatus.Shell), 14, new RiggedDice(2));
        Assert.Equal(52, r.Damage);
    }

    [Fact]
    public void Sleepers_hold_full_gauges_but_never_act_and_wake_when_hit()
    {
        var sleeper = Target(BattleStatus.Sleep);
        var fighter = Attacker();
        var engine = new BattleEngine([fighter], [sleeper], EncounterKind.Normal, new RiggedDice(0, 0, 0, 50, 2, 0, 50));

        sleeper.ATBGauge = sleeper.ATBMax;              // full…
        Assert.Same(fighter, engine.TickUntilReady());  // …but never chosen

        engine.ExecuteAttack(fighter, sleeper);         // pain wakes
        Assert.False(sleeper.Has(BattleStatus.Sleep));
    }
}