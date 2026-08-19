namespace Alexandria.Battle;

public sealed partial class BattleEngine
{
    /// <summary>Mirror: BattleCalculator.MagicAccuracy + TryMagicHit — the ability's own
    /// rate plus Magic/4 plus the level difference, clamped to 1..100 and halved by Shell.
    /// </summary>
    private AttackOutcome MagicRoll(BattleUnit caster, BattleUnit target, Int32 baseRate, 
        Boolean divided = false, Boolean shellApplies = true)
    {
        var hitRate = Math.Clamp(baseRate + (caster.Magic >> 2) + caster.Level - target.Level, 1, 100);
        if (shellApplies && target.Has(BattleStatus.Shell))
            hitRate /= 2;
        if (divided)
            hitRate /= 2;
        if (hitRate <= _rng.Rand16() % 100)
            return AttackOutcome.Miss;
        if (target.MagicalEvade > _rng.Rand16() % 100)
            return AttackOutcome.Evaded;
        return AttackOutcome.Hit;
    }
    
    /// <summary>An ability that simply does not apply to this target.</summary>
    private BattleEvent Missed(BattleUnit caster, BattleUnit target) 
        => Finish(caster, target, new AttackResult(AttackOutcome.Miss, 0));
    
    /// <summary>An attack whose script we do not run: the animation plays, nothing else
    /// happens, and the actor still spends its turn.</summary>
    private BattleEvent Choreography(BattleUnit actor, BattleUnit target) 
        => Finish(actor, target, new AttackResult(AttackOutcome.NoEffect, 0));
    
    /// <summary>Basic attack + damage application + gauge reset: one resolved turn.</summary>
    public BattleEvent ExecuteAttack(BattleUnit attacker, BattleUnit target,
        Int32? power = null, Element? element = null, Boolean precise = false)
    {
        _currentCommand = IsEnemy(attacker) ? BattleCommandID.EnemyAtk : BattleCommandID.Attack;
        var result = PhysicalAttack.Execute(attacker, target, power ?? attacker.AttackPower, _rng,
            enemyStyle: IsEnemy(attacker), element: element ?? attacker.WeaponElement, precise: precise);
        PhysicalAttack.Apply(target, result);
        if (result.Damage > 0 && target.Has(BattleStatus.Sleep))
            StatusApplication.Remove(target, BattleStatus.Sleep);
        return Finish(attacker, target, result);
    }
    
    /// <summary>Cast + MP spend + damage + gauge reset: one resolved magic turn.</summary>
    public BattleEvent ExecuteSpell(BattleUnit caster, BattleUnit target, BattleAbility ability,
        Boolean divided = false, Int32? attackOverride = null, Boolean ignoreDefense = false)
    {
        var result = MagicAttack.Execute(caster, target, ability.Power,
            _rng, multiTarget: divided, element: ability.Elements,
            attackOverride: attackOverride, ignoreDefense: ignoreDefense);
        var added = result.Outcome is AttackOutcome.Hit or AttackOutcome.Absorbed
                    && ability.StatusMask != 0 && ability.Rate > _rng.Rand16() % 100
            ? StatusApplication.Apply(target, (BattleStatus)ability.StatusMask, _statusTable)
            : 0;
        PhysicalAttack.Apply(target, result);
        return Finish(caster, target, result, added);
    }

    /// <summary>Magic healing (mirror: 0010_MagicRecoveryScript → CalcHpMagicRecovery):
    /// the ability's power times the caster's magic-attack roll.</summary>
    public BattleEvent ExecuteHeal(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var attack = caster.MaxHP + _rng.Rand16() % (1 + ((caster.Level + caster.Magic) >> 3));
        if (caster.Has(BattleStatus.Mini))
            attack >>= 1;
        var healed = Math.Min(Math.Min(ability.Power * attack, MagicAttack.DamageCap), target.MaxHP - target.CurrentHP);
        target.CurrentHP += healed;
        return Finish(caster, target, new AttackResult(AttackOutcome.Healed, healed));
    }
    
    /// <summary>Status magic (mirror: 0011_MagicApplyNegativeStatusScript): one accuracy
    /// roll folding in spell rate, caster Magic/4, and level difference; Shell and
    /// multi-targeting halve it; no damage - on a hit the statuses simply land.</summary>
    public BattleEvent ExecuteStatusSpell(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided = false)
    {
        var hitRate = Math.Clamp(ability.Rate + (caster.Magic >> 2) + caster.Level - target.Level, 1, 100);
        if (target.Has(BattleStatus.Shell))
            hitRate /= 2;
        if (divided)
            hitRate /= 2;

        AttackResult result;
        BattleStatus landed = 0;
        if (hitRate <= _rng.Rand16() % 100)
            result = new AttackResult(AttackOutcome.Miss, 0);
        else if (target.MagicalEvade > _rng.Rand16() % 100)
            result = new AttackResult(AttackOutcome.Evaded, 0);
        else
        {
            landed = StatusApplication.Apply(target, (BattleStatus)ability.StatusMask, _statusTable);
            result = new AttackResult(AttackOutcome.Hit, 0);
        }

        return Finish(caster, target, result, landed);
    }
    
    /// <summary>Status cure (mirror: 0012_MagicCureStatusScript → TryRemoveAbilityStatuses):
    /// the listed statuses are lifted outright, and if the target carried none of them the
    /// ability registers a miss.</summary>
    public BattleEvent ExecuteCure(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var present = target.Statuses & (BattleStatus)ability.StatusMask;
        if (present != 0)
            StatusApplication.Remove(target, present);
        return Finish(caster, target,
            new AttackResult(present != 0 ? AttackOutcome.Healed : AttackOutcome.Miss, 0));
    }

    /// <summary>Revive (mirror: 0013_ReviveScript): a fallen unit returns with
    /// (Spirit + power)% of maximum HP.</summary>
    public BattleEvent ExecuteRevive(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        if (target.IsAlive || target.Has(BattleStatus.Petrify))
            return Finish(caster, target, new AttackResult(AttackOutcome.Miss, 0));
        
        var restored = Math.Min(target.MaxHP * (target.Spirit + ability.Power) / 100, target.MaxHP);
        target.CurrentHP = restored;
        StatusApplication.Remove(target, (BattleStatus)ability.StatusMask | BattleStatus.Death);
        return Finish(caster, target, new AttackResult(AttackOutcome.Healed, restored));
    }
    
    /// <summary>Instant death (mirror: 0014_DeathScript).</summary>
    public BattleEvent ExecuteDeath(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        if (target.Has(BattleStatus.EasyKill))
            return Finish(caster, target, new AttackResult(AttackOutcome.Guarded, 0));

        var outcome = MagicRoll(caster, target, ability.Rate, shellApplies: false);
        BattleStatus landed = 0;
        if (outcome == AttackOutcome.Hit)
        {
            landed = StatusApplication.Apply(target, (BattleStatus)ability.StatusMask, _statusTable);
            // IsAlive is HP-based, so the status has to reach the HP model to mean anything
            if ((landed & BattleStatus.Death) != 0)
                target.CurrentHP = 0;
        }
        
        return Finish(caster, target, new AttackResult(outcome, 0), landed);
    }

    /// <summary>Drain HP (mirror: 0016_DrainHpScript): ordinary magic damage, then the
    /// caster recovers exactly what the target lost. Divergence: that flow is inverted
    /// against a Zombie target.</summary>
    public BattleEvent ExecuteDrainHP(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided = false)
    {
        var ev = ExecuteSpell(caster, target, ability, divided);
        if (ev.Result is { Outcome: AttackOutcome.Hit, Damage: > 0 })
            caster.CurrentHP = Math.Min(caster.MaxHP, caster.CurrentHP + ev.Result.Damage);
        return ev;
    }
    
    /// <summary>Proportional damage (mirror: 0017_MagicGravityDamageScript →
    /// CalcProportionDamage): a fixed percentage of the target's MAXIMUM HP, capped at
    /// 100%.</summary>
    public BattleEvent ExecuteGravity(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided = false)
    {
        var outcome = MagicRoll(caster, target, ability.Rate, divided);
        var damage = 0;
        if (outcome == AttackOutcome.Hit)
        {
            damage = target.MaxHP * Math.Min(ability.Power, 100) / 100;
            target.CurrentHP = Math.Max(0, target.CurrentHP - damage);
        }
        return Finish(caster, target, new AttackResult(outcome, damage));
    }

    /// <summary>Meteorite (mirror: 0018_MeteoriteScript → InitializeAttackParams): the
    /// attack term is a FLAT roll — rand16 % (Magic + Level), floored at 1 — and
    /// DefensePower is forced to zero, so magic defense never applies.</summary>
    public BattleEvent ExecuteMeteorite(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided = false)
        => ExecuteSpell(caster, target, ability, divided,
            attackOverride: +_rng.Rand16() % (caster.Magic + caster.Level), ignoreDefense: true);

    /// <summary>The LV? Death family (mirror: 0022_LvDirectHPDamageScript →
    /// IsTargetLevelMultipleOfCommandRate): no accuracy roll whatsoever - the ability
    /// lands if and only if the target's LEVEL is a multiple of the ability's Rate.
    /// Then direct HP applies, so LV5 Death (rate 5, power 0) kills outright while
    /// Climhazzard (rate 1, power 1) divides every level and always leaves exactly 1 HP.
    /// </summary>
    public BattleEvent ExecuteLevelDirectHP(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => ability.Rate == 0 || target.Level % ability.Rate != 0
            ? Missed(caster, target)
            : ExecuteDirectHP(caster, target, ability, precise: true);

    /// <summary>The LV? Holy family (mirror: 0023_LvHolyScript): the same level gate, then
    /// ordinary elemental magic damage.</summary>
    public BattleEvent ExecuteLevelMagic(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided = false)
        => ability.Rate == 0 || target.Level % ability.Rate != 0
            ? Missed(caster, target)
            : ExecuteSpell(caster, target, ability, divided);

    /// <summary>Direct HP (mirror: BattleCalculator.TryDirectHPDamage) — the target's HP
    /// is SET to the ability's power, not reduced by it.</summary>
    public BattleEvent ExecuteDirectHP(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean precise)
    {
        var outcome = precise ? AttackOutcome.Hit : MagicRoll(caster, target, ability.Rate);
        var damage = 0;
        if (outcome == AttackOutcome.Hit)
        {
            if (target.Has(BattleStatus.Death))
                outcome = AttackOutcome.Miss;
            else
            {
                damage = Math.Max(0, target.CurrentHP - ability.Power);
                target.CurrentHP = ability.Power;
            }
        }
        return Finish(caster, target, new AttackResult(outcome, damage));
    }
    
    /// <summary>White Wind (mirror: 0030_WhiteWindScript): a share of the CASTER's maximum
    /// HP — a third of it when the ability's power is 0.</summary>
    public BattleEvent ExecuteWhiteWind(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var amount = ability.Power == 0 ? caster.MaxHP / 2 : caster.MaxHP * ability.Power / 100;
        var healed = Math.Min(amount, target.MaxHP - target.CurrentHP);
        target.CurrentHP += healed;
        return Finish(caster, target, new AttackResult(AttackOutcome.Healed, healed));
    }

    /// <summary>Flare Star (mirror: 0099_FlareStarScript): damage is the TARGET's own level
    /// times the ability's power.</summary>
    public BattleEvent ExecuteFlareStar(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided)
    {
        var outcome = MagicRoll(caster, target, ability.Rate, divided);
        var damage = 0;
        if (outcome == AttackOutcome.Hit)
        {
            damage = Math.Min(target.Level * ability.Power, MagicAttack.DamageCap);
            target.CurrentHP = Math.Max(0, target.CurrentHP - damage);
        }
        return Finish(caster, target, new AttackResult(outcome, damage));
    }
    
    /// <summary>Positive statuses (mirror: 0103 → TryAlterCommandStatuses): buffs are
    /// applied outright, with no accuracy roll and no evade.</summary>
    public BattleEvent ExecuteBuff(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var landed = StatusApplication.Apply(target, (BattleStatus)ability.StatusMask, _statusTable);
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0), landed);
    }

    /// <summary>Mini (mirror: 0109_MiniScript): a target already shrunk is re-hit with no
    /// roll at all — that is how the spell reliably un-shrinks.</summary>
    public BattleEvent ExecuteMini(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided = false)
    {
        var outcome = target.Has(BattleStatus.Mini)
            ? AttackOutcome.Hit
            : MagicRoll(caster, target, ability.Rate, divided);
        BattleStatus landed = 0;
        if (outcome == AttackOutcome.Hit)
            landed = StatusApplication.Apply(target, (BattleStatus)ability.StatusMask, _statusTable);
        return Finish(caster, target, new AttackResult(outcome, 0), landed);
    }
}

