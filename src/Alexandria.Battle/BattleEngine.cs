using System.Collections.Frozen;
using Alexandria.Formats.Scripts;
using Alexandria.Rules;

namespace Alexandria.Battle;

public sealed record BattleEvent(
    BattleUnit Actor,
    BattleUnit Target,
    AttackResult Result,
    BattleStatus StatusesAdded = 0,
    BattleAbility? Ability = null,
    ItemID? ItemMoved = null);

/// <summary>
/// The headless battle loop: tick gauges until someone is ready, let them act, reset, repeat.
/// Turn order is not a list - it emerges from the clock, exactly like the original.
/// v1 simplifications: ready units act immediately (no command queue or animation delay);
/// simultaneous fills resolve players-first in slot order.
/// </summary>
public sealed partial class BattleEngine
{
    private readonly Random _rng;
    private readonly Int32 _coefficient;
    private readonly EngineHost? _host;
    private readonly WorldState _world;
    private readonly EbVM? _director;
    private readonly IReadOnlyDictionary<BattleStatus, StatusTiming> _statusTable;

    private const Int32 OpeningBudget = 600; // 40s

    private Boolean _reacting;
    private Int32 _openingFrames;

    private BattleCommandID _currentCommand = BattleCommandID.EnemyAtk;

    private delegate BattleEvent Verb(BattleEngine e, BattleUnit actor, BattleUnit target, BattleAbility ability, Boolean divided);

    public List<BattleUnit> Players { get; }
    public List<BattleUnit> Enemies { get; }

    public List<StatusTickEvent> PendingStatusEvents { get; } = [];

    /// <summary>Reactions fired by the last resolved action, for the caller to narrate
    /// (a counter's retaliation resolves as a normal enemy action).</summary>
    public List<BattleEvent> PendingReactions { get; } = [];

    /// <summary>The second and subsequent targets of a group attack, for the caller to narrate.</summary>
    public List<BattleEvent> PendingMultiHits { get; } = [];

    public BattleEngine(List<BattleUnit> players, List<BattleUnit> enemies,
        EncounterKind kind, Random rng, Int32 coefficient = ATBClock.NormalCoefficient,
        IReadOnlyDictionary<BattleStatus, StatusTiming>? statusTable = null, EngineHost? host = null,
        EbVM? director = null, WorldState? world = null)
    {
        Players = players;
        Enemies = enemies;
        _rng = rng;
        _coefficient = coefficient;
        _statusTable = statusTable ?? StatusTicker.DefaultTable;
        _host = host;
        _director = director;
        _world = world ?? new WorldState();
        ATBClock.PlaceInitialGauges(players, enemies, kind, rng);
    }

    public Boolean Victory => Enemies.All(e => !e.IsAlive);
    public Boolean Defeat => Players.All(p => !p.IsAlive);
    public Boolean IsOver => Victory || Defeat || _host?.Result is not null;

    public static Boolean CanResolve(AbilityScript script) => Verbs.ContainsKey(script);

    private IEnumerable<BattleUnit> AllUnits => Players.Concat(Enemies);

    /// <summary>Advances the clock until a living unit is ready; null when the battle is over.</summary>
    public BattleUnit? TickUntilReady()
    {
        while (!IsOver)
        {
            var ready = AllUnits.FirstOrDefault(u => u is { IsAlive: true, ATBReady: true }
                                                     && !u.Has(BattleStatus.PreventATBConfirm | BattleStatus.Stop | BattleStatus.Petrify));
            if (ready is not null)
                return ready;

            TickScripts();
            if (_host is not null && _host.Phase != EngineHost.PhaseNormal)
            {
                if (++_openingFrames <= OpeningBudget)
                    continue;
                _host.Phase = EngineHost.PhaseNormal;
            }

            foreach (var unit in AllUnits)
            {
                ATBClock.Tick(unit, _coefficient);
                PendingStatusEvents.AddRange(StatusTicker.Tick(unit, _coefficient, _statusTable));
            }
        }
        return null;
    }

    /// <summary>One frame of every script thread (mirror: ProcessEvents running each
    /// battle object per frame).</summary>
    private void TickScripts()
    {
        if (_host is null)
            return;
        if (_director is not null)
        {
            _host.BeginSlice(8);
            _director.Tick();
        }
        foreach (var unit in AllUnits)
            (unit.Brain as ScriptedBrain)?.Tick();
    }

    public Boolean IsEnemy(BattleUnit unit) => Enemies.Contains(unit);

    /// <summary>A party member casting. Deliberately the same dispatch the enemies use.</summary>
    public BattleEvent ExecuteAbility(BattleUnit actor, BattleUnit target, BattleAbility ability)
    {
        var ev = Resolve(actor, target, ability);
        DrainTrance(actor);
        return ev;
    }

    /// <summary>Enemy turn. Target: uniform among living players (mirror: the B_SELECT
    /// operator, EventEngine.OperatorSelect). Attack choice: uniform among affordable
    /// roster entries.</summary>
    public BattleEvent ExecuteEnemyTurn(BattleUnit enemy)
    {
        var ev = ChooseAndResolve(enemy);
        DrainTrance(enemy);
        return ev;
    }
    
    private BattleEvent ChooseAndResolve(BattleUnit enemy)
    {
        var brain = enemy.Brain as ScriptedBrain;
        if (enemy.Brain is not null
            && enemy.Brain.Decide(enemy, Players, _rng) is var (attackId, chosen)
            && enemy.Abilities.FirstOrDefault(a => a.ID == attackId) is { } scripted)
            return Resolve(enemy, chosen, scripted, brain?.LastTargetMask ?? 0);

        var living = Players.Where(p => p.IsAlive).ToList();
        var target = living[_rng.Rand16() % living.Count];

        var usable = enemy.Abilities.Where(a => a.MP <= enemy.CurrentMP).ToList();
        if (usable.Count == 0)
            return ExecuteAttack(enemy, target);
        return Resolve(enemy, target, usable[_rng.Rand16() % usable.Count], brain?.LastTargetMask ?? 0);
    }

    /// <summary>A basic attack as a COMMAND Verbs call ExecuteAttack directly instead.</summary>
    public BattleEvent ExecuteBasicAttack(BattleUnit actor, BattleUnit target)
    {
        var ev = ExecuteAttack(actor, target);
        DrainTrance(actor);
        return ev;
    }
    
    /// <summary>Mirror: SBattleCalculator.CalcResult's tail — every resolved
    /// (command, target) pair provokes the target's script. Enemy targets only,
    /// player attackers only, blocked by PreventCounter; HP-zero fires the death
    /// function (tag 9), otherwise the counter (tag 6). Counters can't chain:
    /// vanilla gates on IsCommandDeclarable, and a retaliation isn't declarable.</summary>
    private void Provoke(BattleUnit attacker, BattleUnit target)
    {
        if (_reacting || !IsEnemy(target) || IsEnemy(attacker))
            return;
        if (target.Has(BattleStatus.CannotAct | BattleStatus.Death | BattleStatus.Sleep))
            return;
        if (target.Brain is not ScriptedBrain brain)
            return;

        var tag = (UInt16)(target.IsAlive ? 6 : 9);
        if (tag == 9 && !target.DiesFighting)
            return;

        _reacting = true;
        try
        {
            if (brain.React(tag, 1 << Players.IndexOf(attacker), (Int32)_currentCommand, 0) is not { } attackId ||
                target.Abilities.FirstOrDefault(a => a.ID == attackId) is not { } retaliation)
                return;
            var mask = _host?.SysList(0) ?? 0;
            var chosen = _host?.UnitFromMask(mask) ?? attacker;
            PendingReactions.Add(Resolve(target, chosen, retaliation, mask));
        }
        finally
        {
            _reacting = false;
        }
    }

    /// <summary>Mirror: BattleCaster.EnemyTranceBonusAttack - an ENEMY acting in Trance
    /// deals +50%.</summary>
    private Int32 TranceBonus(BattleUnit actor) => IsEnemy(actor) && actor.Has(BattleStatus.Trance) ? 1 : 0;

    /// <summary>Mirror: SBattleCalculator.CheckDamageReaction - being acted on by the
    /// OTHER side raises the gauge by `rand16 % Spirit`, and topping it out is what puts
    /// the unit into Trance.</summary>
    private void FillTrance(BattleUnit caster, BattleUnit target, AttackResult result)
    {
        if (result.Outcome is not (AttackOutcome.Hit or AttackOutcome.CriticalHit) || result.Damage <= 0)
            return;
        if (IsEnemy(caster) == IsEnemy(target) || !target.HasTrance || !target.IsAlive)
            return;
        if (target.Has(BattleStatus.CannotTrance) || target.Spirit <= 0)
            return;

        var gain = _rng.Rand16() % target.Spirit;
        if (target.TranceGauge + gain < BattleUnit.TranceMax)
        {
            target.TranceGauge += gain;
            return;
        }
        target.TranceGauge = BattleUnit.TranceMax;
        StatusApplication.Apply(target, BattleStatus.Trance, _statusTable);
    }

    /// <summary>Mirror: btl_cmd.IsNeedToDecreaseTrance plus the drain block (:1293) - every
    /// COMMAND a tranced unit completes costs `(300 - level) / Spirit * 10`, and when the
    /// gauge cannot cover it the Trance ends.</summary>
    private void DrainTrance(BattleUnit actor)
    {
        if (!actor.Has(BattleStatus.Trance) || (actor.PermanentStatuses & BattleStatus.Trance) != 0)
            return;

        var cost = (300 - actor.Level) / Math.Max(1, actor.Spirit) * 10;
        if (actor.TranceGauge > cost)
            actor.TranceGauge -= cost;
        else
            StatusApplication.Remove(actor, BattleStatus.Trance);
    }

    /// <summary>The tail every resolved action shares (mirror: SBattleCalculator.CalcResult):
    /// the actor's gauge resets, then the target's script gets its chance to react.</summary>
    private BattleEvent Finish(BattleUnit caster, BattleUnit target, AttackResult result, BattleStatus landed = 0)
    {
        ATBClock.ResetAfterAction(caster);
        FillTrance(caster, target, result);
        Provoke(caster, target);
        return new BattleEvent(caster, target, result, landed);
    }

    /// <summary>Every unit named by a script's SV_Target mask (mirror:
    /// btl_cmd.CheckTargetCondition). An empty or unusable mask falls back to
    /// the single unit the brain nominated.</summary>
    private List<BattleUnit> ExpandTargets(Int32 mask, BattleUnit fallback, Boolean forDead = false)
    {
        var named = new List<BattleUnit>();
        for (var line = 0; line < 8; line++)
            if ((mask >> line & 1) != 0 && _host?.UnitFromMask(1 << line) is { } unit)
                named.Add(unit);

        var usable = named.Where(u => forDead || u.IsAlive).ToList();
        if (usable.Count > 0)
            return usable;
        if (forDead)
            return [fallback]; // a revive keeps the fallen ally it was aimed at
        return Revalidate(fallback) is { } one ? [one] : [];
    }

    /// <summary>Mirror: btl_cmd.CheckTargetCondition (:1209-1259). A script may legally
    /// name a dead target — GetBattleID carries no alive filter — so the engine revalidates
    /// just before the command runs: a fallen target is replaced by a random living unit
    /// from the same side, and only when that whole side is down does the action lapse.
    /// Divergence: vanilla's ForDead abilities (revive) may keep a dead player as their
    /// target.</summary>
    private BattleUnit? Revalidate(BattleUnit target)
    {
        if (target.IsAlive)
            return target;
        var side = (IsEnemy(target) ? Enemies : Players).Where(u => u.IsAlive).ToList();
        return side.Count == 0 ? null : side[_rng.Rand16() % side.Count];
    }

    /// <summary>Resolve an enemy attack through its ability-script id, the way vanilla
    /// dispatches via ScriptsLoader.GetBattleScript — ids we implement do their work,
    /// and an id with no script resolves to nothing.</summary>
    private BattleEvent Resolve(BattleUnit actor, BattleUnit target, BattleAbility ability, Int32 targetMask = 0)
    {
        var targets = ExpandTargets(targetMask, target, ability.ForDead);
        if (targets.Count == 0)
            return Choreography(actor, target);

        actor.CurrentMP -= ability.MP;

        if (ability.SetsStandAnimation is { } pose)
            actor.StandAnimation = pose;

        _currentCommand = IsEnemy(actor) ? BattleCommandID.EnemyAtk
            : ability.Script == AbilityScript.EnemyPhysicalAttack ? BattleCommandID.Attack
            : BattleCommandID.BlackMagic;

        // mirror: BattleCommand.IsDevided - only the "Many" target types pay the spread penalty; an
        // inherently party-wide attack lands at full strength
        var divided = targets.Count > 1 && ability.TargetType is >= 3 and <= 5;

        var first = ResolveOne(actor, targets[0], ability, divided);
        for (var i = 1; i < targets.Count; i++)
            PendingMultiHits.Add(ResolveOne(actor, targets[i], ability, divided));
        return first;
    }

    private BattleEvent ResolveOne(BattleUnit actor, BattleUnit target, BattleAbility ability, Boolean divided)
    {
        var ev = Verbs.TryGetValue(ability.Script, out var verb)
            ? verb(this, actor, target, ability, divided)
            : Choreography(actor, target);
        return ev with { Ability = ability };
    }

    private static readonly FrozenDictionary<AbilityScript, Verb> Verbs = new Dictionary<AbilityScript, Verb>
    {
        // AbilityScript.BaseWeapon,
        // AbilityScript.SimpleWeapon,
        // AbilityScript.WillWeapon,
        // AbilityScript.DexterityWeapon,
        // AbilityScript.MagicWeapon,
        // AbilityScript.RandomWeapon,
        // AbilityScript.BloodSwordWeapon,
        // AbilityScript.LevelWeapon,
        [AbilityScript.EnemyPhysicalAttack] = (e, actor, target, ability, _) => e.ExecuteAttack(actor, target, ability.Power, ability.Elements),
        [AbilityScript.MagicAttack] = (e, actor, target, ability, divided) => e.ExecuteSpell(actor, target, ability, divided),
        [AbilityScript.MagicRecovery] = (e, actor, target, ability, _) => e.ExecuteHeal(actor, target, ability),
        [AbilityScript.MagicApplyNegativeStatus] = (e, actor, target, ability, divided) => e.ExecuteStatusSpell(actor, target, ability, divided),
        [AbilityScript.MagicCureStatus] = (e, actor, target, ability, _) => e.ExecuteCure(actor, target, ability),
        [AbilityScript.Revive] = (e, actor, target, ability, _) => e.ExecuteRevive(actor, target, ability),
        [AbilityScript.Death] = (e, actor, target, ability, _) => e.ExecuteDeath(actor, target, ability),
        [AbilityScript.DrainMp] = (e, actor, target, ability, _) => e.ExecuteDrainMP(actor, target, ability),
        [AbilityScript.DrainHp] = (e, actor, target, ability, divided) => e.ExecuteDrainHP(actor, target, ability, divided),
        [AbilityScript.MagicGravityDamage] = (e, actor, target, ability, divided) => e.ExecuteGravity(actor, target, ability, divided),
        [AbilityScript.Meteorite] = (e, actor, target, ability, divided) => e.ExecuteMeteorite(actor, target, ability, divided),
        [AbilityScript.PhysicalAttack] = (e, actor, target, ability, _) => e.ExecutePhysicalAttack(actor, target, ability),
        [AbilityScript.OriginalMagicAttack] = (e, actor, target, ability, _) => e.ExecuteOriginalMagicAttack(actor, target, ability),
        [AbilityScript.GoblinPunch] = (e, actor, target, ability, divided) => e.ExecuteGoblinPunch(actor, target, ability, divided),
        [AbilityScript.LvDirectHPDamage] = (e, actor, target, ability, _) => e.ExecuteLevelDirectHP(actor, target, ability),
        [AbilityScript.LvHoly] = (e, actor, target, ability, divided) => e.ExecuteLevelMagic(actor, target, ability, divided),
        [AbilityScript.LvReduceDefence] = (e, actor, target, ability, _) => e.ExecuteLevelDefenseDown(actor, target, ability),
        [AbilityScript.PreciseDirectHPDamage] = (e, actor, target, ability, _) => e.ExecuteDirectHP(actor, target, ability, precise: true),
        [AbilityScript.ThousandNeedles] = (e, actor, target, ability, _) => e.ExecuteThousandNeedles(actor, target, ability),
        [AbilityScript.DirectHPDamage] = (e, actor, target, ability, _) => e.ExecuteDirectHP(actor, target, ability, precise: false),
        [AbilityScript.LimitGlove] = (e, actor, target, ability, _) => e.ExecuteLimitGlove(actor, target, ability),
        [AbilityScript.DifferentCasterHpAttack] = (e, actor, target, _, _) => e.ExecuteMissingHPAttack(actor, target),
        [AbilityScript.WhiteWind] = (e, actor, target, ability, _) => e.ExecuteWhiteWind(actor, target, ability),
        [AbilityScript.RandomMpDamage] = (e, actor, target, _, _) => e.ExecuteRandomMPDamage(actor, target),
        [AbilityScript.Darkside] = (e, actor, target, ability, _) => e.ExecuteDarkside(actor, target, ability),
        [AbilityScript.ArmorBreak] = (e, actor, target, ability, _) => e.ExecuteArmorBreak(actor, target, ability),
        [AbilityScript.PowerBreak] = (e, actor, target, ability, _) => e.ExecutePowerBreak(actor, target, ability),
        [AbilityScript.MentalBreak] = (e, actor, target, ability, _) => e.ExecuteMentalBreak(actor, target, ability),
        [AbilityScript.MagicBreak] = (e, actor, target, ability, _) => e.ExecuteMagicBreak(actor, target, ability),
        [AbilityScript.Chakra] = (e, actor, target, ability, _) => e.ExecuteChakra(actor, target, ability),
        [AbilityScript.SpareChange] = (e, actor, target, ability, _) => e.ExecuteSpareChange(actor, target, ability),
        [AbilityScript.Lancer] = (e, actor, target, ability, _) => e.ExecuteLancer(actor, target, ability),
        [AbilityScript.DragonBreath] = (e, actor, target, _, _) => e.ExecuteDragonBreath(actor, target),
        [AbilityScript.WhiteDraw] = (e, actor, target, _, _) => e.ExecuteWhiteDraw(actor, target),
        // AbilityScript.Throw,
        [AbilityScript.Might] = (e, actor, target, ability, _) => e.ExecuteMight(actor, target, ability),
        // AbilityScript.Focus,
        [AbilityScript.Sacrifice] = (e, actor, target, _, _) => e.ExecuteSacrifice(actor, target),
        // AbilityScript.SoulBlade,
        // AbilityScript.Spear,
        [AbilityScript.Phoenix] = (e, actor, target, ability, divided) => e.ExecutePhoenix(actor, target, ability, divided),
        [AbilityScript.SixDragons] = (e, actor, target, _, _) => e.ExecuteSixDragons(actor, target),
        [AbilityScript.Curse] = (e, actor, target, _, _) => e.ExecuteCurse(actor, target),
        // AbilityScript.AngelSnack,
        [AbilityScript.LuckySeven] = (e, actor, target, _, _) => e.ExecuteLuckySeven(actor, target),
        // AbilityScript.WhatIsThat,
        // AbilityScript.ChangeRow,
        // AbilityScript.FleeIteration,
        // AbilityScript.Flee,
        // AbilityScript.Steal,
        [AbilityScript.Scan] = (e, actor, target, _, _) => e.ExecuteScan(actor, target),
        [AbilityScript.Detect] = (e, actor, target, _, _) => e.ExecuteDetect(actor, target),
        // AbilityScript.Charge,
        // AbilityScript.ItemSoft,
        // AbilityScript.MagicSwordAttack,
        // AbilityScript.Eat,
        [AbilityScript.FrogDrop] = (e, actor, target, _, _) => e.ExecuteFrogDrop(actor, target),
        [AbilityScript.Thievery] = (e, actor, target, _, _) => e.ExecuteThievery(actor, target),
        [AbilityScript.DragonCrest] = (e, actor, target, _, _) => e.ExecuteDragonCrest(actor, target),
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
        [AbilityScript.Jewel] = (e, actor, target, ability, _) => e.ExecuteJewel(actor, target, ability),
        [AbilityScript.Summon] = (e, actor, target, ability, divided) => e.ExecuteSummon(actor, target, ability, divided),
        [AbilityScript.Atomos] = (e, actor, target, ability, _) => e.ExecuteAtomos(actor, target, ability),
        [AbilityScript.Odin] = (e, actor, target, ability, _) => e.ExecuteOdin(actor, target, ability),
        [AbilityScript.Melt] = (e, actor, target, _, _) => e.ExecuteMelt(actor, target),
        [AbilityScript.HPSwitching] = (e, actor, target, _, _) => e.ExecuteHPSwitch(actor, target),
        // AbilityScript.HalfDefence,
        [AbilityScript.Cannon] = (e, actor, target, ability, divided) => e.ExecuteCannon(actor, target, ability, divided),
        // AbilityScript.ItemAdd,
        [AbilityScript.Maelstrom] = (e, actor, target, ability, _) => e.ExecuteMaelstrom(actor, target, ability),
        [AbilityScript.AbsorbMagic] = (e, actor, target, _, _) => e.ExecuteAbsorbStat(actor, target, magic: false),
        [AbilityScript.AbsorbStrength] = (e, actor, target, _, _) => e.ExecuteAbsorbStat(actor, target, magic: true),
        [AbilityScript.TranceFull] = (e, actor, target, _, _) => e.ExecuteTranceFull(actor, target),
        [AbilityScript.Entice] = (e, actor, target, ability, _) => e.ExecuteEntice(actor, target, ability),
        [AbilityScript.SimpleAttackGaia] = (e, actor, target, ability, _) => e.ExecuteSimpleAttackGaia(actor, target, ability),
        [AbilityScript.FlareStar] = (e, actor, target, ability, divided) => e.ExecuteFlareStar(actor, target, ability, divided),
        [AbilityScript.PreciseEnemyPhysicalAttack] = (e, actor, target, ability, _) => e.ExecuteAttack(actor, target, ability.Power, ability.Elements, precise: true),
        [AbilityScript.EnemySteal] = (e, actor, target, ability, _) => e.ExecuteSteal(actor, target, ability),
        [AbilityScript.EnemyMug] = (e, actor, target, ability, _) => e.ExecuteMug(actor, target, ability),
        [AbilityScript.MagicApplyPositiveStatus] = (e, actor, target, ability, _) => e.ExecuteBuff(actor, target, ability),
        [AbilityScript.TonberryKarma] = (e, actor, target, _, _) => e.ExecuteTonberryKarma(actor, target),
        [AbilityScript.GrandCross] = (e, actor, target, _, _) => e.ExecuteGrandCross(actor, target),
        // AbilityScript.Swallow,
        // AbilityScript.PreciseEnemyPhysicalAttackAndChangeRow,
        [AbilityScript.IaiStrike] = (e, actor, target, ability, divided) => e.ExecuteIaiStrike(actor, target, ability, divided),
        [AbilityScript.Mini] = (e, actor, target, ability, divided) => e.ExecuteMini(actor, target, ability, divided)
    }.ToFrozenDictionary();
}
