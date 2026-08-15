namespace Alexandria.Battle;

/// <summary>
/// Vanilla magic damage (mirror: 0009_MagicAttackScript + NormalMagicParams).
/// No hit roll, no crits; multi-target casts take a −1 damage modifier.
/// </summary>
public static class MagicAttack
{
    public const Int32 DamageCap = 9999;

    public static AttackResult Execute(BattleUnit caster, BattleUnit target, Int32 spellPower,
        Random rng, Boolean multiTarget = false, Int32 damageModifiers = 0)
    {
        var attack = caster.Magic
                     + rng.Rand16() % (1 + ((caster.Level + caster.Magic) >> 3));

        var modifiers = damageModifiers + (multiTarget ? -1 : 0);
        if (target.Has(BattleStatus.Shell))
            modifiers -= 1;
        for (var i = 0; i < Math.Abs(modifiers); i++)
            attack = modifiers > 0 ? attack * 3 >> 1 : attack >> 1;

        var damage = Math.Max(attack, 1) * Math.Max(spellPower - target.MagicalDefense, 1);
        return new AttackResult(AttackOutcome.Hit, Math.Min(damage, DamageCap));
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

