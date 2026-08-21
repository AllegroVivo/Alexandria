using System.Reflection.Metadata;
using Alexandria.Rules;

namespace Alexandria.Battle;

public sealed partial class BattleEngine
{
    /// <summary>Every restorative item uses a fixed Attack of 15 against zero defense,
    /// the amount is simply `power × 15` (mirror: CalcHpMagicRecovery — AttackPower × Attack).</summary>
    private const Int32 ItemAttack = 15;
    
    /// <summary>Which gem feeds which eidolon (mirror: 0085_SummonScript's switch).
    /// Madeen has none - it scales with the caster's level instead.</summary>
    private static readonly Dictionary<String, ItemID> SummonGem = new()
    {
        ["Shiva"] = ItemID.Opal,
        ["Ifrit"] = ItemID.Topaz,
        ["Ramuh"] = ItemID.Peridot,
        ["Leviathan"] = ItemID.Aquamarine,
        ["Bahamut"] = ItemID.Garnet,
        ["Ark"] = ItemID.LapisLazuli,
        ["Fenrir"] = ItemID.Sapphire
    };
    
    /// <summary>The nine weapons Soul Blade accepts (mirror: 0046's switch).</summary>
    private static readonly HashSet<ItemID> SoulBlades =
    [
        ItemID.ButterflySword, ItemID.TheOgre, ItemID.Exploda, ItemID.RuneTooth,
        ItemID.AngelBless, ItemID.Sargatanas, ItemID.Masamune, ItemID.TheTower,
        ItemID.UltimaWeapon
    ];

    /// <summary>Mirror: BattleCalculator.MagicAccuracy + TryMagicHit — the ability's own
    /// rate plus Magic/4 plus the level difference, clamped to 1..100 and halved by Shell.
    /// </summary>
    private AttackOutcome MagicRoll(BattleUnit caster, BattleUnit target, Int32 baseRate,
        Boolean divided = false, Boolean shellApplies = true, Int32 hitBonus = 0)
    {
        var hitRate = Math.Clamp(
            baseRate + (caster.Magic >> 2) + caster.Level - target.Level, 1, 100) + hitBonus;
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

    /// <summary>LV3 Def-less (mirror: 0024_LvReduceDefenceScript): if the target's level
    /// divides by the ability's rate, both defenses are re-rolled DOWN to a random value
    /// below what they were.</summary>
    public BattleEvent ExecuteLevelDefenseDown(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        if (ability.Rate == 0 || target.Level % ability.Rate != 0)
            return Missed(caster, target);

        if (target.PhysicalDefense != 0)
        {
            target.PhysicalDefense = _rng.Rand16() % target.PhysicalDefense;
            if (target.MagicalDefense != 0)
                target.MagicalDefense = _rng.Rand16() % target.MagicalDefense;
        }
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0));
    }

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
    public BattleEvent ExecuteMissingHPAttack(BattleUnit caster, BattleUnit target)
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
    public BattleEvent ExecuteRandomMPDamage(BattleUnit caster, BattleUnit target)
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

    /// <summary>Chakra (mirror: 0037_ChakraScript): restores the ability's power as a percentage
    /// of the target's maximum HP and MP at once.</summary>
    public BattleEvent ExecuteChakra(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var hp = Math.Min(target.MaxHP * ability.Power / 100, target.MaxHP - target.CurrentHP);
        var mp = Math.Min(target.MaxMP * ability.Power / 100, target.MaxMP - target.CurrentMP);
        target.CurrentHP += hp;
        target.CurrentMP += mp;
        return Finish(caster, target, new AttackResult(AttackOutcome.Healed, hp));
    }

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
    public BattleEvent ExecuteDragonBreath(BattleUnit caster, BattleUnit target)
        => FlatDamage(caster, target, target.MaxHP - target.CurrentHP);

    /// <summary>White Draw (mirror: 0041_WhiteDrawScript): restores MP to the WHOLE PARTY
    /// — a random amount up to twice the target's level, the same figure for everyone.</summary>
    public BattleEvent ExecuteWhiteDraw(BattleUnit caster, BattleUnit target)
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

    /// <summary>Sacrifice (mirror: 0045_SacrificeScript): The user restores an ally to full
    /// HP and MP and dies in the process.</summary>
    public BattleEvent ExecuteSacrifice(BattleUnit caster, BattleUnit target)
    {
        if (target.Has(BattleStatus.Death) || caster.Has(BattleStatus.Petrify | BattleStatus.Death))
            return Missed(caster, target);

        var restored = target.MaxHP - target.CurrentHP;
        target.CurrentHP = target.MaxHP;
        target.CurrentMP = target.MaxMP;
        caster.CurrentMP = 0;
        caster.CurrentHP = 0;
        StatusApplication.Apply(caster, BattleStatus.Death, _statusTable);
        return Finish(caster, target, new AttackResult(AttackOutcome.Healed, restored));
    }

    /// <summary>Soul Blade (mirror: 0046_SoulBladeScript): The user throws their sword's own
    /// status rider at the target - no damage, no accuracy roll.</summary>
    public BattleEvent ExecuteSoulBlade(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => SoulBlades.Contains(caster.Weapon)
            ? LandStatuses(caster, target, caster.WeaponStatus)
            : Missed(caster, target);
    
    // AbilityScript.Spear,

    /// <summary>Phoenix (mirror: 0049_PhoenixScript): Revive on an ally and ordinary
    /// magic damage to an enemy.</summary>
    public BattleEvent ExecutePhoenix(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided)
        => IsEnemy(target)
            ? ExecuteSpell(caster, target, ability, divided)
            : ExecuteRevive(caster, target, ability);

    /// <summary>Six Dragons (mirror: 0050_SixDragonsScript): one roll out of a hundred
    /// picks among six outcomes — 10% full HP and MP, 20% full HP, 20% full MP, 15% HP to
    /// 1, 15% MP to 1, and the remaining 20% reduces both to 1.</summary>
    public BattleEvent ExecuteSixDragons(BattleUnit caster, BattleUnit target)
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

    /// <summary>Curse (mirror: 0051_CurseScript): no damage, makes the target
    /// WEAK to an element and strips all defense against it, clears guard, absorb
    /// and half in one pass.</summary>
    public BattleEvent ExecuteCurse(BattleUnit caster, BattleUnit target)
    {
        var element = (Element)(1 << (_rng.Rand8() % 8));
        target.WeakElements |= element;
        target.GuardElements &= ~element;
        target.AbsorbElements &= ~element;
        target.HalfElements &= ~element;
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0));
    }

    /// <summary>Angel's Snack (mirror: 0052_AngelSnackScript): uses a remedy on the target
    /// and pays for it, the item being the ability's Power field.</summary>
    public BattleEvent ExecuteAngelSnack(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided)
    {
        var item = (ItemID)ability.Power;
        if (_world.Items[item] == 0 || !_itemEffects.TryGetValue(item, out var effect))
            return Missed(caster, target);

        _world.Items.Remove(item);
        return ResolveOne(caster, target, ability, divided) with { Ability = ability, ItemMoved = item };
    }

    /// <summary>Lucky Seven (mirror: 0053_LuckySevenScript): a check on the CASTER's
    /// own HP - if it ends in a 7, one roll in four picks 7, 77, 777 or 7777.</summary>
    public BattleEvent ExecuteLuckySeven(BattleUnit caster, BattleUnit target)
        => FlatDamage(caster, target, caster.CurrentHP % 10 != 7 ? 1 : (_rng.Rand8() % 4) switch
        {
            0 => 7,
            1 => 77,
            2 => 777,
            _ => 7777
        });

    /// <summary>What's That!? (mirror: 0054_WhatIsThatScript): The user points off-stage and
    /// the target turns to look.</summary>
    public BattleEvent ExecuteWhatIsThat(BattleUnit caster, BattleUnit target)
    {
        if (IsEnemy(target) && SpecialStart)
            return Missed(caster, target);

        target.TurnedAway = true;
        if (!IsEnemy(target))
            target.Row = target.DefaultRow;
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0));
    }
    
    // AbilityScript.ChangeRow,
    // AbilityScript.FleeIteration,
    // AbilityScript.Flee,
    // AbilityScript.Steal,

    /// <summary>Scan (mirror: 0059 → Libra): pure information.</summary>
    public BattleEvent ExecuteScan(BattleUnit caster, BattleUnit target)
        => Finish(caster, target, new AttackResult(AttackOutcome.Scanned, 0));

    /// <summary>Detect (mirror: 0060_DetectScript → SetBattlePeeping): displays the target's
    /// STEAL table, rarest first.</summary>
    public BattleEvent ExecuteDetect(BattleUnit caster, BattleUnit target)
        => Finish(caster, target, new AttackResult(AttackOutcome.Detected, 0));

    /// <summary>Charge! (mirror: 0061_ChargeScript): The user rallies every party member who
    /// has low HP, causing each one immediately attacks a random enemy</summary>
    public BattleEvent ExecuteCharge(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var rallied = Players
            .Where(p => p.IsAlive && p.CurrentHP * 6 <= p.MaxHP && !p.Has(BattleStatus.NoInput))
            .ToList();
        var living = Enemies.Where(e => e.IsAlive).ToList();
        if (rallied.Count == 0 || living.Count == 0)
            return Missed(caster, target);

        _reacting = true;
        try
        {
            foreach (var ally in rallied)
                PendingReactions.Add(ExecuteAttack(ally, living[_rng.Rand16() % living.Count]));
        }
        finally
        {
            _reacting = false;
        }
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0));
    }

    /// <summary>Soft (mirror: 0062_ItemSoftScript): cures Petrify and/or reduces the HP
    /// of any creature made of stone to 0.</summary>
    public BattleEvent ExecuteItemSoft(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        if ((target.Category & EnemyCategory.Stone) == 0)
            return ExecuteCure(caster, target, ability);
        caster.CurrentHP = 0;
        StatusApplication.Apply(target, BattleStatus.Death, _statusTable);
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0));
    }
    
    // AbilityScript.MagicSwordAttack,
    // AbilityScript.Eat,

    /// <summary>Frog Drop (mirror: 0066_FrogDropScript): damage is every frog Quina has
    /// ever caught times the caster's level.</summary>
    public BattleEvent ExecuteFrogDrop(BattleUnit caster, BattleUnit target)
        => FlatDamage(caster, target, _world.Frogs == 0 ? 1 : _world.Frogs * caster.Level);

    /// <summary>Thievery (mirror: 0067_ThieveryScript): the save's successful steal count
    /// times the caster's Dexterity, halved.</summary>
    public BattleEvent ExecuteThievery(BattleUnit caster, BattleUnit target)
        => FlatDamage(caster, target, _world.Thefts * caster.Speed / 2);

    /// <summary>Dragon's Crest (mirror: 0068_DragonCrestScript): damage is the SQUARE of
    /// how many dragons this save has killed.</summary>
    public BattleEvent ExecuteDragonCrest(BattleUnit caster, BattleUnit target)
        => FlatDamage(caster, target, _world.Kills[EnemyCategory.Dragon] * _world.Kills[EnemyCategory.Dragon]);

    /// <summary>Potion / Hi-Potion (mirror: 0069): HP back, then the item's own statuses
    /// rolled against its rate - which is 0, so they never land anything.</summary>
    public BattleEvent ExecuteItemPotion(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var healed = 0;
        if(ability.Power>0)
        {
            healed = Math.Min(ability.Power * ItemAttack, target.MaxHP - target.CurrentHP);
            target.CurrentHP += healed;
        }
        var landed = ability.StatusMask != 0 && _rng.Rand16() % 100 < ability.Rate
            ? StatusApplication.Apply(target, ability.StatusMask, _statusTable)
            : 0;
        return Finish(caster, target, new AttackResult(AttackOutcome.Healed, healed), landed);
    }

    /// <summary>Ether (mirror: 0070): `power × 15`, into MP.</summary>
    public BattleEvent ExecuteItemEther(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var restored = Math.Min(ability.Power * ItemAttack, target.MaxMP - target.CurrentMP);
        target.CurrentMP += restored;
        return Finish(caster, target, new AttackResult(AttackOutcome.Healed, restored));
    }

    /// <summary>Elixir (mirror: 0071): fully fills target's HP and MP.</summary>
    public BattleEvent ExecuteItemElixir(BattleUnit caster, BattleUnit target)
    {
        var healed = target.MaxHP - target.CurrentHP;
        target.CurrentHP = target.MaxHP;
        target.CurrentMP = target.MaxMP;
        return Finish(caster, target, new AttackResult(AttackOutcome.Healed, healed));
    }

    /// <summary>Phoenix Down / Pinion (mirror: 0072): a fallen ally returns on 1–10 HP.</summary>
    public BattleEvent ExecuteItemPhoenix(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        if (target.IsAlive)
            return Missed(caster, target);
        target.CurrentHP = 1 + _rng.Rand8() % 10;
        StatusApplication.Remove(target, ability.StatusMask | BattleStatus.Death);
        return Finish(caster, target, new AttackResult(AttackOutcome.Healed, target.CurrentHP));
    }
    
    // AbilityScript.ItemCureStatus,

    /// <summary>Gems used as items (mirror: 0074): healing of `power × (how many you still
    /// hold + 1)`.</summary>
    public BattleEvent ExecuteItemGem(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var healed = Math.Min(ability.Power * (_world.Items[(ItemID)ability.ID] + 1), target.MaxHP - target.CurrentHP);
        target.CurrentHP += healed;
        return Finish(caster, target, new AttackResult(AttackOutcome.Healed, healed));
    }

    /// <summary>Dead Pepper (mirror: 0075): damage of `power × (held + 1)`.</summary>
    public BattleEvent ExecuteDeadPepper(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => FlatDamage(caster, target, ability.Power * (_world.Items[(ItemID)ability.ID] + 1));

    /// <summary>Tent (mirror: 0076): restore half of HP and MP. Also performs a
    /// coin-flip for a chance of being "Bitten" and taking the item's statuses
    /// instead.</summary>
    public BattleEvent ExecuteTent(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var healed = Math.Min(target.MaxHP >> 1, target.MaxHP - target.CurrentHP);
        target.CurrentHP += healed;
        target.CurrentMP = Math.Min(target.MaxMP, target.CurrentMP + (target.MaxMP >> 1));

        var landed = ability.Rate < _rng.Rand16() % 100
            ? StatusApplication.Apply(target, ability.StatusMask, _statusTable)
            : 0;
        return Finish(caster, target, new AttackResult(AttackOutcome.Healed, healed), landed);
    }

    /// <summary>Dark Matter (mirror: 0077): flat damage of 9,999.</summary>
    public BattleEvent ExecuteDarkMatter(BattleUnit caster, BattleUnit target)
        => FlatDamage(caster, target, MagicAttack.DamageCap);
    
    // AbilityScript.DoubleCastSpecial,
    // AbilityScript.MassSpear,

    /// <summary>Jewel (mirror: 0084_JewelScript): a magic accuracy roll that takes nothing
    /// from anyone - a hit conjures an Ore into the bag.</summary>
    public BattleEvent ExecuteJewel(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var outcome = MagicRoll(caster, target, ability.Rate);
        if (outcome != AttackOutcome.Hit)
            return Finish(caster, target, new AttackResult(outcome, 0));
        _world.Items.Add(ItemID.Ore);
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0)) with { ItemMoved = ItemID.Ore };
    }

    /// <summary>Summon (mirror: 0085_SummonScript): ordinary magic damage whose POWER
    /// is raised by the number of that eidolon's gem in the party's inventory, or by the
    /// caster's level (for Madeen).</summary>
    public BattleEvent ExecuteSummon(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided = false)
    {
        var bonus = ability.Name == "Madeen"
            ? caster.Level
            : SummonGem.TryGetValue(ability.Name, out var gem)
                ? _world.Items[gem]
                : 0;
        return ExecuteSpell(caster, target, ability with { Power = ability.Power + bonus }, divided);
    }

    /// <summary>Atomos (mirror: 0086_AtomosScript): proportional damage - a share of the
    /// target's maximum HP.</summary>
    public BattleEvent ExecuteAtomos(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        if (target.Has(BattleStatus.EasyKill))
            return Missed(caster, target);
        if ((ability.Elements & target.GuardElements) != 0)
            return Finish(caster, target, new AttackResult(AttackOutcome.Guarded, 0));

        var share = Math.Min(ability.Power + _world.Items[ItemID.Amethyst], 100);
        var damage = target.MaxHP * share / 100;
        target.CurrentHP = Math.Max(0, target.CurrentHP - damage);
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, damage));
    }

    /// <summary>Odin (mirror: 0087_OdinScript): a accuracy roll that lands the ability's
    /// Death outright.</summary>
    public BattleEvent ExecuteOdin(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        if (target.Has(BattleStatus.EasyKill))
            return Finish(caster, target, new AttackResult(AttackOutcome.Guarded, 0));

        var outcome = MagicRoll(caster, target, ability.Rate, shellApplies: false,
            hitBonus: _world.Items[ItemID.Ore] >> 1);
        if (outcome != AttackOutcome.Hit)
            return Finish(caster, target, new AttackResult(outcome, 0));

        var landed = StatusApplication.Apply(target, ability.StatusMask, _statusTable);
        if ((landed & BattleStatus.Death) != 0)
            target.CurrentHP = 0;
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0), landed);
    }

    /// <summary>Melt (mirror: 0088): damage equals the caster's CURRENT HP - and then the
    /// caster dies.</summary>
    public BattleEvent ExecuteMelt(BattleUnit caster, BattleUnit target)
    {
        var ev = FlatDamage(caster, target, caster.CurrentHP);
        caster.CurrentHP = 0;
        return ev;
    }

    /// <summary>HP Switching (mirror: 0089): caster and target trade current HP outright,
    /// each clamped to their own maximum.</summary>
    public BattleEvent ExecuteHPSwitch(BattleUnit caster, BattleUnit target)
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
    public BattleEvent ExecuteTranceFull(BattleUnit caster, BattleUnit target)
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
        if (result.Outcome == AttackOutcome.Miss)
            return Finish(caster, target, new AttackResult(AttackOutcome.Miss, 0));

        var stolen = (ItemID)ability.Rate;
        var taken = _world.Items.Remove(stolen);
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0)) with
        {
            ItemMoved = taken ? stolen : null
        };
    }

    /// <summary>Mug (mirror: 0102): a physical hit with NO accuracy roll and no crit, it
    /// always connects, followed by an item theft.</summary>
    public BattleEvent ExecuteMug(BattleUnit caster, BattleUnit target, BattleAbility ability)
    {
        var stolen = (ItemID)ability.Rate;
        var ev = ExecuteAttack(caster, target, ability.Power, ability.Elements, precise: true);
        return _world.Items.Remove(stolen) ? ev with { ItemMoved = stolen } : ev;
    }

    /// <summary>Positive statuses (mirror: 0103 → TryAlterCommandStatuses): buffs are
    /// applied outright, with no accuracy roll and no evade.</summary>
    public BattleEvent ExecuteBuff(BattleUnit caster, BattleUnit target, BattleAbility ability)
        => LandStatuses(caster, target, ability.StatusMask);

    /// <summary>Everyone's Grudge (mirror: 0104_TonberryKarmaScript): damage doubles with
    /// every Tonberry the save has killed — 1 &lt;&lt; (count − 1), capped at a shift of 20.</summary>
    public BattleEvent ExecuteTonberryKarma(BattleUnit caster, BattleUnit target)
        => FlatDamage(caster, target,
            _world.Tonberries == 0 ? 0 : 1 << Math.Min(20, _world.Tonberries - 1));

    /// <summary>Grand Cross (mirror: 0105) Sixteen statuses are each rolled independently at 1-in-8
    /// (<code>rand8 &gt;&gt; 5 == 0</code>); LowHP is not inflicted but reduces the target to a
    /// random 1–9 HP.</summary>
    public BattleEvent ExecuteGrandCross(BattleUnit caster, BattleUnit target)
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

    /// <summary>Iai Strike (mirror: 0108_IaiStrikeScript): an accuracy roll landing Death</summary>
    public BattleEvent ExecuteIaiStrike(BattleUnit caster, BattleUnit target, BattleAbility ability, Boolean divided)
    {
        if (target.Has(BattleStatus.EasyKill))
            return Finish(caster, target, new AttackResult(AttackOutcome.Guarded, 0));

        var outcome = MagicRoll(caster, target, ability.Rate, divided);
        if (outcome != AttackOutcome.Hit)
            return Finish(caster, target, new AttackResult(outcome, 0));

        var landed = StatusApplication.Apply(target, ability.StatusMask, _statusTable);
        if ((landed & BattleStatus.Death) != 0)
            target.CurrentHP = 0;
        return Finish(caster, target, new AttackResult(AttackOutcome.Hit, 0), landed);
    }

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
