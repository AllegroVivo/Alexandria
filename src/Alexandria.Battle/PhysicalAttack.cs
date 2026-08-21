namespace Alexandria.Battle;

public enum AttackOutcome
{
    Miss,
    Evaded,
    Hit,
    CriticalHit,
    Guarded,
    Absorbed,
    NoEffect,
    Healed,
    MPDamage,
    Scanned,
    Detected
}

/// <summary>
/// Vanilla basic physical attack (mirrors: BattleCaster.SetLowPhysicalAttack,
/// BattleCalculator.TryPhysicalHit/TryCriticalHit, CalcContext, btl_para.SetDamage).
/// Note: a base HitRate of 100 can never lose the miss roll (0-99), so basic
/// attacks are only ever avoided via evade.
/// </summary>
public static class PhysicalAttack
{
    public const Int32 DamageCap = 9999;

    public static AttackResult Execute(
        BattleUnit attacker, BattleUnit target, Int32 attackPower,
        Random rng, Boolean enemyStyle = false, Int32 damageModifiers = 0,
        Element element = Element.None, Boolean precise = false,
        Boolean statusModifiers = true)
    {
        if (!precise)
        {
            // hit roll first (TryPhysicalHit): Blind/Confuse caster and Defend target halve it
            var hitRate = 100;
            if (attacker.Has(BattleStatus.Blind | BattleStatus.Confuse))
                hitRate /= 2;
            if (target.Has(BattleStatus.Defend))
                hitRate /= 2;
            if (hitRate <= rng.Rand16() % 100)
                return new AttackResult(AttackOutcome.Miss, 0);
        
            // evade roll: evaded when target.PhysicalEvade > rand16 % 100
            if (target.PhysicalEvade > rng.Rand16() % 100)
                return new AttackResult(AttackOutcome.Evaded, 0);
        }
        
        // attack roll: Str + rand16 % (1 + ((Level + Str) >> shift)); players >>3, enemies >>2
        var shift = enemyStyle ? 2 : 3;
        var attack = attacker.Strength
                     + rng.Rand16() % (1 + ((attacker.Level + attacker.Strength) >> shift));
        
        // element gate (weapon element for players, attack-data element for enemies)
        if ((element & target.GuardElements) != 0)
            return new AttackResult(AttackOutcome.Guarded, 0);
        var absorbed = (element & target.AbsorbElements) != 0;
        
        // crit: q = Spirit >> 2; crit when rand16 % q > rand16 % 100 (~Spirit/4 %)
        var crit = false;
        var q = attacker.Spirit >> 2;
        if (!precise && q != 0 && rng.Rand16() % q > rng.Rand16() % 100)
        {
            attack *= 2;
            crit = true;
        }

        // status modifiers (BattleTarget.cs): Defend/Protect −1, Sleep/Mini +1
        var modifiers = damageModifiers;
        if (statusModifiers)
        {
            if (target.Has(BattleStatus.Defend | BattleStatus.Protect))
                modifiers -= 1;
            if (target.Has(BattleStatus.Sleep | BattleStatus.Mini))
                modifiers += 1;
        }
        if ((element & target.HalfElements) != 0)
            modifiers -= 1;
        if ((element & target.WeakElements) != 0)
            modifiers += 1;
        if ((element & attacker.BonusElements) != 0)
            modifiers += 1;
        
        // situational modifiers: each +1 → ×1.5 (integer), each −1 → ×0.5 (statuses feed this later)
        for (var i = 0; i < Math.Abs(modifiers); i++)
            attack = modifiers > 0 ? attack * 3 >> 1 : attack >> 1;

        var damage = Math.Max(attack, 1)
                     * Math.Max(attackPower - (absorbed ? 0 : target.PhysicalDefense), 1);
        var outcome = absorbed ? AttackOutcome.Absorbed
            : crit ? AttackOutcome.CriticalHit : AttackOutcome.Hit;
        return new AttackResult(outcome, Math.Min(damage, DamageCap));
    }

    /// <summary>btl_para.SetDamage: HP floors at zero, never negative.</summary>
    public static void Apply(BattleUnit target, Int32 damage)
        => target.CurrentHP = target.CurrentHP > damage ? target.CurrentHP - damage : 0;

    public static void Apply(BattleUnit target, AttackResult result)
    {
        if (result.Outcome == AttackOutcome.Absorbed)
            target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + result.Damage);
        else
            Apply(target, result.Damage);
    }
}

public sealed record AttackResult(AttackOutcome Outcome, Int32 Damage);
