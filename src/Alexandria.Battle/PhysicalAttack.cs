namespace Alexandria.Battle;

public enum AttackOutcome
{
    Evaded,
    Hit,
    CriticalHit
}

/// <summary>
/// Vanilla basic physical attack (mirrors: BattleCaster.SetLowPhysicalAttack,
/// BattleCalculator.TryPhysicalHit/TryCriticalHit, CalcContext, btl_para.SetDamage).
/// Note: a base HitRate of 100 can never lose the miss roll (0-99), so basic
/// attacks are only ever avoided via evade - faithful to the original.
public static class PhysicalAttack
{
    public const Int32 DamageCap = 9999;

    public static AttackResult Execute(
        BattleUnit attacker, BattleUnit target, Int32 attackPower,
        Random rng, Boolean enemyStyle = false, Int32 damageModifiers = 0)
    {
        // evade roll: evaded when target.PhysicalEvade > rand16 % 100
        if (target.PhysicalEvade > rng.Next(0, UInt16.MaxValue) % 100)
            return new AttackResult(AttackOutcome.Evaded, 0);
        
        // attack roll: Str + rand16 % (1 + ((Level + Str) >> shift)); players >>3, enemies >>2
        var shift = enemyStyle ? 2 : 3;
        var attack = attacker.Strength
                     + rng.Next(0, UInt16.MaxValue) % (1 + ((attacker.Level + attacker.Strength) >> shift));
        
        // crit: q = Spirit >> 2; crit when rand16 % q > rand16 % 100 (~Spirit/4 %)
        var crit = false;
        var q = attacker.Spirit >> 2;
        if (q != 0 && rng.Next(0, UInt16.MaxValue) % q > rng.Next(0, UInt16.MaxValue) % 100)
        {
            attack *= 2;
            crit = true;
        }
        
        // situational modifiers: each +1 → ×1.5 (integer), each −1 → ×0.5 (statuses feed this later)
        for (var i = 0; i < Math.Abs(damageModifiers); i++)
            attack = damageModifiers > 0 ? attack * 3 >> 1 : attack >> 1;
        
        // damage = EnsureAttack × EnsurePowerDifference, capped at 9999
        var damage = Math.Max(attack, 1) * Math.Max(attackPower - target.PhysicalDefense, 1);
        return new AttackResult(crit ? AttackOutcome.CriticalHit : AttackOutcome.Hit, Math.Min(damage, DamageCap));
    }

    /// <summary>btl_para.SetDamage: HP floors at zero, never negative.</summary>
    public static void Apply(BattleUnit target, Int32 damage)
        => target.CurrentHP = target.CurrentHP > damage ? target.CurrentHP - damage : 0;
}

public sealed record AttackResult(AttackOutcome Outcome, Int32 Damage);
