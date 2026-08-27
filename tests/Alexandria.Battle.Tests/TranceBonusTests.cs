using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

public class TranceBonusTests
{
    private static BattleUnit Unit(String name, Int32 maxHP = 600, BattleStatus statuses = 0) => new()
    {
        Name = name, Level = 10, MaxHP = maxHP, MaxMP = 100, CurrentHP = maxHP, CurrentMP = 100,
        Speed = 20, Strength = 10, Magic = 20, Spirit = 0,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        AttackPower = 10, HasTrance = false, Statuses = statuses
    };

    /// <summary>Heals the player and reports how much, with the attack roll rigged to 0.</summary>
    private static Int32 HealedBy(BattleStatus casterStatuses)
    {
        var player = Unit("Zidane");
        player.CurrentHP = 1;
        var wolf = Unit("Wolf", maxHP: 100, statuses: casterStatuses);
        var engine = new BattleEngine([player], [wolf], EncounterKind.Normal, new RiggedDice(0, 0, 0));

        return engine.ExecuteAbility(wolf, player,
            new BattleAbility("Cure", AbilityScript.MagicRecovery, 1, Element.None, 0, 0, 0))
            .Result.Damage;
    }

    [Fact]
    public void An_enemy_in_Trance_heals_half_again_as_much()
    {
        // 0010 calls EnemyTranceBonusAttack, which ExecuteHeal was skipping entirely
        Assert.Equal(100, HealedBy(0));
        Assert.Equal(150, HealedBy(BattleStatus.Trance));
    }

    [Fact]
    public void Trance_and_Mini_cancel_rather_than_compounding()
    {
        // the reason Mini had to stop being an independent shift: vanilla nets the count,
        // so +1 and -1 leave the attack value untouched rather than giving 100 * 3/2 / 2
        Assert.Equal(100, HealedBy(BattleStatus.Trance | BattleStatus.Mini));
        Assert.Equal(50, HealedBy(BattleStatus.Mini));
    }

    [Fact]
    public void An_enemy_in_Trance_drains_half_again_as_much_MP()
    {
        static Int32 Drained(BattleStatus casterStatuses)
        {
            var player = Unit("Zidane");
            var wolf = Unit("Wolf", statuses: casterStatuses);
            var engine = new BattleEngine([player], [wolf], EncounterKind.Normal, new RiggedDice(0, 0, 0));

            engine.ExecuteAbility(wolf, player,
                new BattleAbility("Osmose", AbilityScript.DrainMp, 4, Element.None, 0, 0, 0));
            return 100 - player.CurrentMP;
        }

        Assert.Equal(20, Drained(0));                        // 4 * 20 >> 2
        Assert.Equal(30, Drained(BattleStatus.Trance));      // attack 20 -> 30
    }
}
