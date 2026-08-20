using Alexandria.Battle;
using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

public class BattleAbilityTests
{
    private static BattleUnit Knight() => new()
    {
        Name = "Knight", Level = 14, MaxHP = 400, MaxMP = 20, CurrentHP = 400, CurrentMP = 20,
        Speed = 16, Strength = 12, Magic = 6, Spirit = 14,
        PhysicalDefense = 6, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        AttackPower = 12,
    };

    private static BattleUnit Wolf(params BattleAbility[] attacks)
    {
        var wolf = new BattleUnit
        {
            Name = "Wolf", Level = 14, MaxHP = 899, MaxMP = 464, CurrentHP = 899, CurrentMP = 464,
            Speed = 21, Strength = 11, Magic = 11, Spirit = 16,
            PhysicalDefense = 10, PhysicalEvade = 3, MagicalDefense = 10, MagicalEvade = 4,
            AttackPower = 22,
        };
        wolf.Abilities.AddRange(attacks);
        return wolf;
    }

    [Fact]
    public void Enemy_picks_a_spell_from_its_roster_and_casts_it()
    {
        // dice: target 0%1, pick 1 → Thundara, magic attack die 0 → attack = Magic 11,
        // trance fill 0 (damage now draws one)
        // damage = 11 × max(25 − 0, 1) = 275; MP 464 − 12 = 452
        var knight = Knight();
        var wolf = Wolf(
            new BattleAbility("Rush", AbilityScript.EnemyPhysicalAttack, 24, Element.None, 100, 0, 0),
            new BattleAbility("Thundara", AbilityScript.MagicAttack, 25, Element.Thunder, 0, 0, 12));
        var engine = new BattleEngine([knight], [wolf], EncounterKind.Preemptive, new RiggedDice(0, 1, 0, 0));

        var e = engine.ExecuteEnemyTurn(wolf);

        Assert.Equal("Thundara", e.Ability?.Name);
        Assert.Equal(275, e.Result.Damage);
        Assert.Equal(125, knight.CurrentHP);
        Assert.Equal(452, wolf.CurrentMP);
    }

    [Fact]
    public void Broke_enemy_falls_back_to_its_basic_attack()
    {
        // no affordable roster entry → plain ExecuteAttack with AttackPower 22
        // dice: target 0, hit 0, evade 50, attack die 0 → 11, crit q die 0 (0%4=0), crit 99,
        // trance fill 0 (damage now draws one)
        // damage = 11 × max(22 − 6, 1) = 176
        var knight = Knight();
        var wolf = Wolf(new BattleAbility("Thundara", AbilityScript.MagicAttack, 25, Element.Thunder, 0, 0, 12));
        wolf.CurrentMP = 5;
        var engine = new BattleEngine([knight], [wolf], EncounterKind.Preemptive,
            new RiggedDice(0, 0, 50, 0, 0, 99, 0));

        var e = engine.ExecuteEnemyTurn(wolf);

        Assert.Null(e.Ability);
        Assert.Equal(176, e.Result.Damage);
        Assert.Equal(224, knight.CurrentHP);
    }

    [Fact]
    public void Enemy_status_move_afflicts_the_party()
    {
        // Tongue: script 11, rate 60 → hitRate = clamp(60 + (11>>2) + 14 − 14, 1, 100) = 62
        // dice: target 0, pick 0, accuracy 10 (62 > 10 → hit), evade 50 (MEvade 0 → no)
        var knight = Knight();
        var wolf = Wolf(new BattleAbility("Tongue", AbilityScript.MagicApplyNegativeStatus, 0, Element.None, 60, BattleStatus.Slow, 0));
        var engine = new BattleEngine([knight], [wolf], EncounterKind.Preemptive,
            new RiggedDice(0, 0, 10, 50));

        var e = engine.ExecuteEnemyTurn(wolf);

        Assert.True(knight.Has(BattleStatus.Slow));
        Assert.Equal(BattleStatus.Slow, e.StatusesAdded);
    }
}
