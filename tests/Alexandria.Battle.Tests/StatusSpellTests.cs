using Alexandria.Battle;
using Alexandria.Formats;
using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

public class StatusSpellTests
{
    [Fact]
    public void Status_sets_parse_by_bit_index()
    {
        var sets = StatusSet.Load(Path.Combine(FF9Data.Data, @"Battle\StatusSets.csv"));
        Assert.Equal(0u, sets[0].StatusMask);                       // Set 0: none
        Assert.Equal((UInt32)BattleStatus.Death, sets[2].StatusMask);
        Assert.Equal((UInt32)BattleStatus.Sleep, sets[17].StatusMask);
    }

    [Fact]
    public void Sleep_lands_when_the_accuracy_roll_passes()
    {
        // Vivi-ish: rate 50 + Magic 24>>2(=6) + lvl 13 − lvl 14 = 55% hit
        // rig: accuracy 54 (55<=54 false → pass), evade 50 (MEva 4 not > 50)
        var (vivi, wolf) = Duel();
        var engine = new BattleEngine([vivi], [wolf], EncounterKind.Preemptive, new RiggedDice(54, 50));

        var e = engine.ExecuteAbility(vivi, wolf, new BattleAbility(
            "Sleep", AbilityScript.MagicApplyNegativeStatus, 0, Element.None, 50, BattleStatus.Sleep, 10));
        
        Assert.Equal(AttackOutcome.Hit, e.Result.Outcome);
        Assert.True(wolf.Has(BattleStatus.Sleep));
        Assert.Equal(40, vivi.CurrentMP);
    }

    [Fact]
    public void Sleep_can_miss()
    {
        // accuracy roll 60 → 55 <= 60 → miss, no status
        var (vivi, wolf) = Duel();
        var engine = new BattleEngine([vivi], [wolf], EncounterKind.Preemptive, new RiggedDice(60));

        var e = engine.ExecuteAbility(vivi, wolf, new BattleAbility(
            "Sleep", AbilityScript.MagicApplyNegativeStatus, 0, Element.None, 50, BattleStatus.Sleep, 10));
        
        Assert.Equal(AttackOutcome.Miss, e.Result.Outcome);
        Assert.False(wolf.Has(BattleStatus.Sleep));
    }

    private static (BattleUnit, BattleUnit) Duel() => (
        new BattleUnit
        {
            Name = "Vivi", Level = 13, MaxHP = 100, MaxMP = 50, CurrentHP = 100, CurrentMP = 50,
            Speed = 16, Strength = 12, Magic = 24, Spirit = 19,
            PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        },
        new BattleUnit
        {
            Name = "Wolf", Level = 14, MaxHP = 899, MaxMP = 464, CurrentHP = 899, CurrentMP = 464,
            Speed = 21, Strength = 11, Magic = 11, Spirit = 16,
            PhysicalDefense = 10, PhysicalEvade = 3, MagicalDefense = 10, MagicalEvade = 4,
        });
}