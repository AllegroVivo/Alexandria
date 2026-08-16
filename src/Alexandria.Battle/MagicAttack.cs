namespace Alexandria.Battle;

/// <summary>
/// Vanilla magic damage (mirror: 0009_MagicAttackScript + NormalMagicParams).
/// No hit roll, no crits; multi-target casts take a −1 damage modifier.
/// </summary>
public static class MagicAttack
{
    public const Int32 DamageCap = 9999;

    public static AttackResult Execute(BattleUnit caster, BattleUnit target, Int32 spellPower,
        Random rng, Boolean multiTarget = false, Int32 damageModifiers = 0,
        Element element = Element.None)
    {
        var attack = caster.Magic
                     + rng.Rand16() % (1 + ((caster.Level + caster.Magic) >> 3));

        if ((element & target.GuardElements) != 0)
            return new AttackResult(AttackOutcome.Guarded, 0);

        var modifiers = damageModifiers + (multiTarget ? -1 : 0);
        if (target.Has(BattleStatus.Shell))
            modifiers -= 1;
        if ((element & caster.BonusElements) != 0)
            modifiers += 1;
        if ((element & target.HalfElements) != 0)
            modifiers -= 1;
        var absorbed = (element & target.AbsorbElements) != 0;
        if ((element & target.WeakElements) != 0)
            modifiers += 1;
        
        // AlterStatuses(element): opposing elements cure the thermal statuses
        if ((element & (Element.Cold | Element.Aqua)) != 0)
            target.Statuses &= ~BattleStatus.Heat;
        if ((element & Element.Fire) != 0)
            target.Statuses &= ~BattleStatus.Freeze;
        
        // flight rule (CanUseCommandForFlight): Earth passes under floating targets
        if ((element & Element.Earth) != 0 && target.Has(BattleStatus.Float))
            return new AttackResult(AttackOutcome.Miss, 0);
        
        for (var i = 0; i < Math.Abs(modifiers); i++)
            attack = modifiers > 0 ? attack * 3 >> 1 : attack >> 1;

        var damage = Math.Max(attack, 1)
                     * Math.Max(spellPower - (absorbed ? 0 : target.MagicalDefense), 1);
        return new AttackResult(absorbed ? AttackOutcome.Absorbed : AttackOutcome.Hit,
            Math.Min(damage, DamageCap));
    }

    /// <summary>Rider statuses on damage spells (mirror: TryAlterMagicStatuses): a flat
    /// rate vs rand%100 roll, fully independent of the damage — Shell, level, and stats
    /// never touch it. Rolled only when the action carries a status set.</summary>
    public static BattleStatus TryRiderStatuses(BattleUnit target, UInt32 statuses, Int32 rate, Random rng)
    {
        if (statuses == 0 || rate <= rng.Rand16() % 100)
            return 0;
        var added = (BattleStatus)statuses;
        target.Statuses |= added;
        return added;
    }
}

