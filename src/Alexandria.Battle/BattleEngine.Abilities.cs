using Alexandria.Rules;

namespace Alexandria.Battle;

public sealed partial class BattleEngine
{
    /// <summary>Which gem feeds which eidolon (mirror: 0085_SummonScript's switch).
    /// Madeen has none - it scales with the caster's level instead.</summary>
    private static readonly Dictionary<String, String> SummonGem = new()
    {
        ["Shiva"] = "Opal", ["Ifrit"] = "Topaz", ["Ramuh"] = "Peridot",
        ["Leviathan"] = "Aquamarine", ["Bahamut"] = "Garnet",
        ["Ark"] = "Lapis Lazuli", ["Fenrir"] = "Sapphire"
    };
    
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

    /// <summary>Damage that skips the whole calculator: no accuracy, no defense, no
    /// element, no modifiers.</summary>
    private BattleEvent FlatDamage(BattleUnit caster, BattleUnit target, Int32 amount)
    {
        var damage = Math.Clamp(amount, 0, MagicAttack.DamageCap);
        target.CurrentHP = Math.Max(0, target.CurrentHP - damage);
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, damage));
    }

    /// <summary>Statuses that land 100% of the time.</summary>
    private BattleEvent LandStatuses(BattleUnit caster, BattleUnit target, BattleStatus statuses)
        => Finish(caster, target, new AttackResult(
            AttackOutcome.Hit, 0), StatusApplication.Apply(target, statuses, _statusTable));

    /// <summary>Rider statuses on a damage ability (mirror: TryAlterMagicStatuses): one
    /// flat rate roll, independent of the damage.</summary>
    private BattleStatus RollRiders(BattleUnit target, BattleAbility ability, AttackResult result)
        => result.Outcome is AttackOutcome.Hit or AttackOutcome.Absorbed
           && ability.StatusMask != 0 && ability.Rate > _rng.Rand16() % 100
            ? StatusApplication.Apply(target, ability.StatusMask, _statusTable)
            : 0;
    
    // AbilityScript.BaseWeapon,
    // AbilityScript.SimpleWeapon,
    // AbilityScript.WillWeapon,
    // AbilityScript.DexterityWeapon,
    // AbilityScript.MagicWeapon,
    // AbilityScript.RandomWeapon,
    // AbilityScript.BloodSwordWeapon,
    // AbilityScript.LevelWeapon,
    
    /// <summary>Basic attack + damage application + gauge reset: one resolved turn.</summary>
    public BattleEvent ExecuteAttack(BattleUnit attacker, BattleUnit target,
        Int32? power = null, Element? element = null, Boolean precise = false)
    {
        _currentCommand = IsEnemy(attacker) ? BattleCommandID.EnemyAtk : BattleCommandID.Attack;
        var result = PhysicalAttack.Execute(attacker, target, power ?? attacker.AttackPower, _rng,
            enemyStyle: IsEnemy(attacker), element: element ?? attacker.WeaponElement, precise: precise,
            damageModifiers: TranceBonus(attacker));
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
            attackOverride: attackOverride, ignoreDefense: ignoreDefense,
            damageModifiers: TranceBonus(caster));
        var added = RollRiders(target, ability, result);
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
        var outcome = MagicRoll(caster, target, ability.Rate, divided);
        return outcome == AttackOutcome.Hit
            ? LandStatuses(caster, target, ability.StatusMask)
            : Finish(caster, target, new AttackResult(outcome, 0));
    }
    
    /// <summary>Status cure (mirror: 0012_MagicCureStatusScript → TryRemoveAbilityStatuses):
    /// the listed statuses are lifted outright, and if the target carried none of them the
    /// ability registers a miss.</summary>
    public BattleEvent ExecuteCure(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var present = target.Statuses & ability.StatusMask;
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
        StatusApplication.Remove(target, ability.StatusMask | BattleStatus.Death);
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
            landed = StatusApplication.Apply(target, ability.StatusMask, _statusTable);
            // IsAlive is HP-based, so the status has to reach the HP model to mean anything
            if ((landed & BattleStatus.Death) != 0)
                target.CurrentHP = 0;
        }
        
        return Finish(caster, target, new AttackResult(outcome, 0), landed);
    }

    /// <summary>MP drain (mirror: 0015_DrainMpScript → CalcMpDamage). No accuracy roll and
    /// no element handling whatsoever. The amount is max(0, power − MagicalDefence) × attack >> 2,
    /// where attack is the caster's usual magic roll halved by their own Mini and by the
    /// target's Shell.</summary>
    public BattleEvent ExecuteDrainMP(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        if (ReferenceEquals(caster, target) || !target.IsAlive || target.Has(BattleStatus.Petrify))
            return Missed(caster, target);

        var attack = caster.Magic + _rng.Rand16() % (1 + ((caster.Level + caster.Magic) >> 3));
        if (caster.Has(BattleStatus.Mini))
            attack >>= 1;
        if (target.Has(BattleStatus.Shell))
            attack >>= 1;

        var drained = Math.Min(Math.Max(0, ability.Power - target.MagicalDefense) * attack >> 2, target.CurrentMP);
        target.CurrentMP -= drained;
        caster.CurrentMP = Math.Min(caster.MaxHP, caster.CurrentMP + drained);

        return Finish(caster, target, new AttackResult(AttackOutcome.MPDamage, drained));
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

    public BattleEvent ExecutePhysicalAttack(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var result = PhysicalAttack.Execute(caster, target,
            caster.AttackPower * ability.Power / 10, _rng,
            enemyStyle: IsEnemy(caster), element: caster.WeaponElement, precise: true,
            damageModifiers: TranceBonus(caster));
        PhysicalAttack.Apply(target, result);
        return Finish(caster, target, result, RollRiders(target, ability, result));
    }

    /// <summary> (mirror: 0020_OriginalMagicAttackScript): magic-shaped damage driven by
    /// the weapon. Power is `weapon * ability / 10` and the attack roll is the caster's
    /// LOW PHYSICAL roll (SetLowPhysicalAttack), but it lands against MAGIC defense
    /// and is halved by Shell.</summary>
    public BattleEvent ExecuteOriginalMagicAttack(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var attack = caster.Strength + _rng.Rand16() % (1 + ((caster.Level + caster.Strength) >> 2));
        var result = MagicAttack.Execute(caster, target,
            caster.AttackPower * ability.Power / 10, _rng,
            multiTarget: false, element: ability.Elements,
            attackOverride: attack, damageModifiers: TranceBonus(caster));
        PhysicalAttack.Apply(target, result);
        return Finish(caster, target, result, RollRiders(target, ability, result));
    }

    /// <summary>Goblin Punch (mirror: 0021): ordinary magic damage, except that when
    /// caster and target are the SAME LEVEL the attack gains the caster's level and
    /// defense is zeroed.</summary>
    public BattleEvent ExecuteGoblinPunch(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided = false)
    {
        var matched = caster.Level == target.Level;
        var attack = caster.Magic + _rng.Rand16() % (1 + ((caster.Level + caster.Magic) >> 3));
        return ExecuteSpell(caster, target, ability, divided, ignoreDefense: matched,
            attackOverride: matched ? attack + caster.Level : attack);
    }

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
    
    // AbilityScript.LvReduceDefence,

    /// <summary>Thousand Needles (mirror: 0026): power × 100 + rate, dealt flatly to
    /// anyone.</summary>
    public BattleEvent ExecuteThousandNeedles(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => FlatDamage(caster, target, ability.Power * 100 + ability.Rate);

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

    /// <summary>Limit Glove (mirror: 0028): the same flat number as Thousand Needles, but
    /// only from a caster sitting on exactly 1 HP - otherwise it simply misses.</summary>
    public BattleEvent ExecuteLimitGlove(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => caster.CurrentHP != 1
            ? Missed(caster, target)
            : FlatDamage(caster, target, ability.Power * 100 + ability.Rate);

    /// <summary>Pumpkin Head and kin (mirror: 0029): damage equals the caster's MISSING HP.</summary>
    public BattleEvent ExecuteMissingHPAttack(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => FlatDamage(caster, target, caster.MaxHP - caster.CurrentHP);
    
    /// <summary>White Wind (mirror: 0030_WhiteWindScript): a share of the CASTER's maximum
    /// HP — a third of it when the ability's power is 0.</summary>
    public BattleEvent ExecuteWhiteWind(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var amount = ability.Power == 0 ? caster.MaxHP / 2 : caster.MaxHP * ability.Power / 100;
        var healed = Math.Min(amount, target.MaxHP - target.CurrentHP);
        target.CurrentHP += healed;
        return Finish(caster, target, new AttackResult(AttackOutcome.Healed, healed));
    }

    /// <summary>Magic Hammer (mirror: 0031): drains a random amount of whatever MP the
    /// target still has, and gives the caster nothing.</summary>
    public BattleEvent ExecuteRandomMPDamage(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        if (target.CurrentMP == 0)
            return Finish(caster, target, new AttackResult(AttackOutcome.MPDamage, 0));
        var drained = _rng.Rand16() % target.CurrentMP;
        target.CurrentMP -= drained;
        return Finish(caster, target, new AttackResult(AttackOutcome.MPDamage, drained));
    }

    /// <summary>Darkside (mirror: 0032_DarksideScript): Spends an EIGHTH of the user's
    /// maximum HP to swing.</summary>
    public BattleEvent ExecuteDarkside(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var result = PhysicalAttack.Execute(caster, target,
            caster.AttackPower * ability.Power / 10, _rng,
            enemyStyle: IsEnemy(caster), element: ability.Elements, precise: true,
            damageModifiers: TranceBonus(caster), statusModifiers: false);
        PhysicalAttack.Apply(target, result);

        if (result.Outcome != AttackOutcome.Guarded)
            PhysicalAttack.Apply(caster, caster.MaxHP >> 3);

        return Finish(caster, target, result);
    }

    /// <summary>The four Breaks (mirror: 0033/0034/0035/0036) An ordinary magic accuracy roll,
    /// rate + Magic/4 + level difference, clamped and halved by Shell — and on a hit,
    /// one stat is written down.</summary>
    private BattleEvent ExecuteBreak(BattleUnit caster, BattleUnit target, BattleAbility ability, Action<BattleUnit> reduce)
    {
        var outcome = MagicRoll(caster, target, ability.Rate);
        if (outcome == AttackOutcome.Hit)
            reduce(target);
        return Finish(caster, target, new AttackResult(outcome, 0));
    }

    /// <summary>Armor Break (0033): physical defense halved.</summary>
    public BattleEvent ExecuteArmorBreak(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => ExecuteBreak(caster, target, ability, u => u.PhysicalDefense /= 2);

    /// <summary>Power Break (0034): Strength down to three quarters.</summary>
    public BattleEvent ExecutePowerBreak(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => ExecuteBreak(caster, target, ability, u => u.Strength = u.Strength * 3 / 4);
    
    /// <summary>Mental Break (0035): magical defense halved.</summary>
    public BattleEvent ExecuteMentalBreak(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => ExecuteBreak(caster, target, ability, u => u.MagicalDefense /= 2);
    
    /// <summary>Magic Break (0036): Magic down to three quarters.</summary>
    public BattleEvent ExecuteMagicBreak(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => ExecuteBreak(caster, target, ability, u => u.Magic = u.Magic * 3 / 4);
        
    // AbilityScript.Chakra,

    /// <summary>Spare Change (mirror: 0038_SpareChangeScript): Amarant throws
    /// `power x level` gil and the damage is that throw squared, tenthed, scaled by
    /// Spirit, over what the party held BEFORE paying.</summary>
    public BattleEvent ExecuteSpareChange(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var purse = _world.Gil;
        var thrown = ability.Power * caster.Level;
        if (purse == 0 || thrown > purse)
            return Missed(caster, target);

        _world.Gil -= thrown;
        return FlatDamage(caster, target,
            (Int32)((Int64)thrown * thrown / 10 * caster.Spirit / purse));
    }

    /// <summary>Lancer (mirror: 0039_LancerScript): a weapon-powered physical hit with NO
    /// accuracy roll, which additionally drains MP equal to a SIXTEENTH of the HP damage
    /// dealt.</summary>
    public BattleEvent ExecuteLancer(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var ev = ExecuteAttack(caster, target, ability.Power > 0 ? ability.Power : null, 
            ability.Elements, precise: true);
        if (ev.Result.Damage > 0)
            target.CurrentMP = Math.Max(0, target.CurrentMP - (ev.Result.Damage >> 4));
        return ev;
    }

    /// <summary>Dragon Breath (mirror: 0040_DragonBreathScript): damage equal to the
    /// target's MISSING HP, dealt to every enemy.</summary>
    public BattleEvent ExecuteDragonBreath(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => FlatDamage(caster, target, target.MaxHP - target.CurrentHP);

    /// <summary>White Draw (mirror: 0041_WhiteDrawScript): restores MP to the WHOLE PARTY
    /// — a random amount up to twice the target's level, the same figure for everyone.</summary>
    public BattleEvent ExecuteWhiteDraw(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var restored = target.Level > 0 ? _rng.Rand16() % (target.Level * 2) : 0;
        foreach (var ally in Players.Where(p => p.IsAlive))
            ally.CurrentMP = Math.Min(ally.MaxMP, ally.CurrentMP + restored);
        return Finish(caster, target, new AttackResult(AttackOutcome.Healed, restored));
    }
    
    // AbilityScript.Throw,    

    /// <summary>Might (mirror: 0043): raises the target's Strength by Strength ÷ power,
    /// capped at 99.</summary>
    public BattleEvent ExecuteMight(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        if (ability.Power > 0)
            target.Strength = Math.Min(00, target.Strength + target.Strength / ability.Power);
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0));
    }
    
    // AbilityScript.Focus,
    // AbilityScript.Sacrifice,
    // AbilityScript.SoulBlade,
    // AbilityScript.Spear,
    // AbilityScript.Phoenix,

    /// <summary>Six Dragons (mirror: 0050_SixDragonsScript): one roll out of a hundred
    /// picks among six outcomes — 10% full HP and MP, 20% full HP, 20% full MP, 15% HP to
    /// 1, 15% MP to 1, and the remaining 20% reduces both to 1.</summary>
    public BattleEvent ExecuteSixDragons(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var roll = _rng.Rand16() % 100;
        if (roll < 10)
        {
            target.CurrentHP = target.MaxHP;
            target.CurrentMP = target.MaxMP;
        }
        else if (roll < 30)
            target.CurrentHP = target.MaxHP;
        else if (roll < 50)
            target.CurrentMP = target.MaxMP;
        else if (roll < 65)
            target.CurrentHP = 1;
        else if (roll < 80)
            target.CurrentMP = 1;
        else
        {
            target.CurrentHP = 1;
            target.CurrentMP = 1;
        }
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0));
    }
    
    // AbilityScript.Curse,
    // AbilityScript.AngelSnack,
    // AbilityScript.LuckySeven,
    // AbilityScript.WhatIsThat,
    // AbilityScript.ChangeRow,
    // AbilityScript.FleeIteration,
    // AbilityScript.Flee,
    // AbilityScript.Steal,

    /// <summary>Scan (mirror: 0059 → Libra): pure information.</summary>
    public BattleEvent ExecuteScan(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => Finish(caster, target, new AttackResult(AttackOutcome.Scanned, 0));
    
    // AbilityScript.Detect,
    // AbilityScript.Charge,
    // AbilityScript.ItemSoft,
    // AbilityScript.MagicSwordAttack,
    // AbilityScript.Eat,

    /// <summary>Frog Drop (mirror: 0066_FrogDropScript): damage is every frog Quina has
    /// ever caught times the caster's level.</summary>
    public BattleEvent ExecuteFrogDrop(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => FlatDamage(caster, target, _world.Frogs == 0 ? 1 : _world.Frogs * caster.Level);

    /// <summary>Thievery (mirror: 0067_ThieveryScript): the save's successful steal count
    /// times the caster's Dexterity, halved.</summary>
    public BattleEvent ExecuteThievery(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => FlatDamage(caster, target, _world.Thefts * caster.Speed / 2);

    /// <summary>Dragon's Crest (mirror: 0068_DragonCrestScript): damage is the SQUARE of
    /// how many dragons this save has killed.</summary>
    public BattleEvent ExecuteDragonCrest(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => FlatDamage(caster, target, _world.Kills[EnemyCategory.Dragon] * _world.Kills[EnemyCategory.Dragon]);
    
    // AbilityScript.ItemPotion,
    // AbilityScript.ItemEther,
    // AbilityScript.ItemElixir,
    // AbilityScript.ItemPhoenix,
    // AbilityScript.ItemCureStatus,
    // AbilityScript.ItemGem,
    // AbilityScript.DeadPepper,
    // AbilityScript.Tent,
    // AbilityScript.DarkMatter,
    // AbilityScript.DoubleCastSpecial,
    // AbilityScript.MassSpear,
    // AbilityScript.Jewel,

    /// <summary>Summon (mirror: 0085_SummonScript): ordinary magic damage whose POWER
    /// is raised by the number of that eidolon's gem in the party's inventory, or by the
    /// caster's level (for Madeen).</summary>
    public BattleEvent ExecuteSummon(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided = false)
    {
        var bonus = ability.Name == "Madeen"
            ? caster.Level
            : SummonGem.TryGetValue(ability.Name, out var gem)
                ? Gems.GetValueOrDefault(gem)
                : 0;
        return ExecuteSpell(caster, target, ability with { Power = ability.Power + bonus }, divided);
    }
    
    // AbilityScript.Atomos,
    // AbilityScript.Odin,

    /// <summary>Melt (mirror: 0088): damage equals the caster's CURRENT HP - and then the
    /// caster dies.</summary>
    public BattleEvent ExecuteMelt(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var ev = FlatDamage(caster, target, caster.CurrentHP);
        caster.CurrentHP = 0;
        return ev;
    }

    /// <summary>HP Switching (mirror: 0089): caster and target trade current HP outright,
    /// each clamped to their own maximum.</summary>
    public BattleEvent ExecuteHPSwitch(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var casterHP = caster.CurrentHP;
        caster.CurrentHP = Math.Min(target.CurrentHP, caster.MaxHP);
        target.CurrentHP = Math.Min(casterHP, target.MaxHP);
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0));
    }
    
    // AbilityScript.HalfDefence,

    /// <summary>Cannon (mirror: 0091 → CalcCannonProportionDamage): a percentage of the
    /// target's CURRENT HP, capped at 100%. Cannon can never kill on its own.</summary>
    public BattleEvent ExecuteCannon(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided = false)
    {
        var outcome = MagicRoll(caster, target, ability.Rate, divided);
        var damage = 0;
        if (outcome == AttackOutcome.Hit)
        {
            damage = target.CurrentHP * Math.Min(ability.Power, 100) / 100;
            target.CurrentHP -= damage;
        }
        return Finish(caster, target, new AttackResult(outcome, damage));
    }
    
    // AbilityScript.ItemAdd,

    /// <summary>Maelstrom (mirror: 0093): sets the target's HP to a random 1–9 then rolls the
    /// ability's statuses normally.</summary>
    public BattleEvent ExecuteMaelstrom(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        target.CurrentHP = 1 + _rng.Rand8() % 9;
        return LandStatuses(caster, target, ability.StatusMask);
    }

    /// <summary>Absorb Magic / Absorb Strength (mirror: 0094 / 0095): halve the stat on the
    /// target, then double the caster's own — capped at 255.</summary>
    public BattleEvent ExecuteAbsorbStat(BattleUnit caster, BattleUnit target, Boolean magic)
    {
        if (magic)
        {
            target.Magic /= 2;
            caster.Magic = Math.Min(255, caster.Magic * 2);
        }
        else
        {
            target.Strength /= 2;
            caster.Strength = Math.Min(255, caster.Strength * 2);
        }
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0));
    }

    /// <summary>Trance (mirror: 0096_TranceFullScript): fills the target's gauge outright
    /// and puts them in Trance, refusing anyone who has no gauge to fill.</summary>
    public BattleEvent ExecuteTranceFull(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        if (!target.HasTrance)
            return Missed(caster, target);
        target.TranceGauge = BattleUnit.TranceMax;
        return LandStatuses(caster, target, BattleStatus.Trance);
    }

    /// <summary>Entice (mirror: 0097): lands its statuses on anyone who is not female, and
    /// misses anyone who is.</summary>
    public BattleEvent ExecuteEntice(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => (target.PlayerCategory & CharacterCategory.Female) != 0
            ? Missed(caster, target)
            : LandStatuses(caster, target, ability.StatusMask);

    /// <summary>Simple Attack, Gaia (mirror: 0098_SimpleAttackGaiaScript): an ordinary
    /// physical attack that misses anyone born on Terra.</summary>
    public BattleEvent ExecuteSimpleAttackGaia(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => (target.PlayerCategory & CharacterCategory.Terra) != 0
            ? Missed(caster, target)
            : ExecuteAttack(caster, target, ability.Power, ability.Elements);
    
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

    /// <summary>Steal (mirror: 0101): a physical accuracy roll and, on success, an item theft.</summary>
    public BattleEvent ExecuteSteal(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var result = PhysicalAttack.Execute(caster, target, 0, _rng,
            enemyStyle: IsEnemy(caster), element: Element.None);
        return Finish(caster, target,
            new AttackResult(result.Outcome == AttackOutcome.Miss ? AttackOutcome.Miss : AttackOutcome.Hit, 0));
    }

    /// <summary>Mug (mirror: 0102): a physical hit with NO accuracy roll and no crit, it
    /// always connects, followed by an item theft.</summary>
    public BattleEvent ExecuteMug(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => ExecuteAttack(caster, target, ability.Power, ability.Elements, precise: true);
    
    /// <summary>Positive statuses (mirror: 0103 → TryAlterCommandStatuses): buffs are
    /// applied outright, with no accuracy roll and no evade.</summary>
    public BattleEvent ExecuteBuff(BattleUnit caster, BattleUnit target, BattleAbility ability) 
        => LandStatuses(caster, target, ability.StatusMask);

    /// <summary>Everyone's Grudge (mirror: 0104_TonberryKarmaScript): damage doubles with
    /// every Tonberry the save has killed — 1 &lt;&lt; (count − 1), capped at a shift of 20.</summary>
    public BattleEvent ExecuteTonberryKarma(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => FlatDamage(caster, target,
            _world.Tonberries == 0 ? 0 : 1 << Math.Min(20, _world.Tonberries - 1));

    /// <summary>Grand Cross (mirror: 0105) Sixteen statuses are each rolled independently at 1-in-8
    /// (<code>rand8 &gt;&gt; 5 == 0</code>); LowHP is not inflicted but reduces the target to a
    /// random 1–9 HP.</summary>
    public BattleEvent ExecuteGrandCross(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        const BattleStatus roster =
            BattleStatus.Petrify | BattleStatus.Silence | BattleStatus.Blind | BattleStatus.Trouble
            | BattleStatus.Zombie | BattleStatus.Death | BattleStatus.Confuse | BattleStatus.Berserk
            | BattleStatus.Stop | BattleStatus.Poison | BattleStatus.Sleep | BattleStatus.Heat
            | BattleStatus.Freeze | BattleStatus.Doom | BattleStatus.Mini | BattleStatus.LowHP;

        BattleStatus landed = 0;
        for (var bit = 0; bit < 32; bit++)
        {
            var status = (BattleStatus)(1u << bit);
            if ((roster & status) == 0 || _rng.Rand8() >> 5 != 0)
                continue;
            if (status == BattleStatus.LowHP)
            {
                if (!target.Has(BattleStatus.Death))
                    target.CurrentHP = 1 + _rng.Rand8() % 9;
                continue;
            }
            landed |= StatusApplication.Apply(target, status, _statusTable);
        }

        if ((landed & BattleStatus.Death) != 0)
            target.CurrentHP = 0;

        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0), landed);
    }
    
    // AbilityScript.Swallow,
    // AbilityScript.PreciseEnemyPhysicalAttackAndChangeRow,
    // AbilityScript.IaiStrike,

    /// <summary>Mini (mirror: 0109_MiniScript): a target already shrunk is re-hit with no
    /// roll at all — that is how the spell reliably un-shrinks.</summary>
    public BattleEvent ExecuteMini(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided = false)
    {
        var outcome = target.Has(BattleStatus.Mini)
            ? AttackOutcome.Hit
            : MagicRoll(caster, target, ability.Rate, divided);
        return outcome == AttackOutcome.Hit
            ? LandStatuses(caster, target, ability.StatusMask)
            : Finish(caster, target, new AttackResult(outcome, 0));
    }
}

