using System.Net.Security;
using Alexandria.Formats.Scripts;
using Alexandria.Rules;

namespace Alexandria.Battle;

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

    private const Int32 OpeningBudget = 600; // 40s

    private IReadOnlyDictionary<BattleStatus, StatusTiming> _statusTable;
    private Boolean _reacting;
    private readonly EbVM? _director;
    private Int32 _openingFrames;

    private BattleCommandID _currentCommand = BattleCommandID.EnemyAtk;

    /// <summary>The ability scripts Resolve can run.</summary>
    public static readonly HashSet<AbilityScript> ResolvableScripts =
    [
        AbilityScript.Death,
        AbilityScript.EnemyPhysicalAttack,
        AbilityScript.MagicAttack,
        AbilityScript.MagicRecovery,
        AbilityScript.MagicApplyNegativeStatus,
        AbilityScript.MagicCureStatus,
        AbilityScript.Revive,
        AbilityScript.DrainHp,
        AbilityScript.MagicGravityDamage,
        AbilityScript.Meteorite,
        AbilityScript.LvDirectHPDamage,
        AbilityScript.LvHoly,
        AbilityScript.PreciseDirectHPDamage,
        AbilityScript.DirectHPDamage,
        AbilityScript.WhiteWind,
        AbilityScript.FlareStar,
        AbilityScript.PreciseEnemyPhysicalAttack,
        AbilityScript.MagicApplyPositiveStatus,
        AbilityScript.Mini
    ];

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
        EbVM? director = null)
    {
        Players = players;
        Enemies = enemies;
        _rng = rng;
        _coefficient = coefficient;
        _statusTable = statusTable ?? StatusTicker.DefaultTable;
        _host = host;
        _director = director;
        ATBClock.PlaceInitialGauges(players, enemies, kind, rng);
    }

    public Boolean Victory => Enemies.All(e => !e.IsAlive);
    public Boolean Defeat => Players.All(p => !p.IsAlive);
    public Boolean IsOver => Victory || Defeat || _host?.Result is not null;

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
        => Resolve(actor, target, ability);

    /// <summary>Enemy turn. Target: uniform among living players (mirror: the B_SELECT
    /// operator, EventEngine.OperatorSelect). Attack choice: uniform among affordable
    /// roster entries.</summary>
    public BattleEvent ExecuteEnemyTurn(BattleUnit enemy)
    {
        if (enemy.Brain is not null
            && enemy.Brain.Decide(enemy, Players, _rng) is var (attackId, chosen)
            && enemy.Abilities.FirstOrDefault(a => a.ID == attackId) is { } scripted)
            return Resolve(enemy, chosen, scripted, (enemy.Brain as ScriptedBrain)?.LastTargetMask ?? 0);

        var living = Players.Where(p => p.IsAlive).ToList();
        var target = living[_rng.Rand16() % living.Count];

        var usable = enemy.Abilities.Where(a => a.MP <= enemy.CurrentMP).ToList();
        if (usable.Count == 0)
            return ExecuteAttack(enemy, target);
        return Resolve(enemy, target, usable[_rng.Rand16() % usable.Count],
            (enemy.Brain as ScriptedBrain)?.LastTargetMask ?? 0);
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

    /// <summary>The tail every resolved action shares (mirror: SBattleCalculator.CalcResult):
    /// the actor's gauge resets, then the target's script gets its chance to react.</summary>
    private BattleEvent Finish(BattleUnit caster, BattleUnit target, AttackResult result, BattleStatus landed = 0)
    {
        ATBClock.ResetAfterAction(caster);
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
        var ev = ability.Script switch
        {
            AbilityScript.EnemyPhysicalAttack => ExecuteAttack(actor, target, ability.Power, ability.Elements),
            AbilityScript.PreciseEnemyPhysicalAttack => ExecuteAttack(actor, target, ability.Power, ability.Elements, precise: true),
            AbilityScript.MagicAttack => ExecuteSpell(actor, target, ability, divided),
            AbilityScript.MagicRecovery => ExecuteHeal(actor, target, ability),
            AbilityScript.MagicApplyNegativeStatus => ExecuteStatusSpell(actor, target, ability, divided),
            AbilityScript.MagicApplyPositiveStatus => ExecuteBuff(actor, target, ability),
            AbilityScript.MagicCureStatus => ExecuteCure(actor, target, ability),
            AbilityScript.Revive => ExecuteRevive(actor, target, ability),
            AbilityScript.Death => ExecuteDeath(actor, target, ability),
            AbilityScript.DrainHp => ExecuteDrainHP(actor, target, ability, divided),
            AbilityScript.MagicGravityDamage => ExecuteGravity(actor, target, ability, divided),
            AbilityScript.Meteorite => ExecuteMeteorite(actor, target, ability, divided),
            AbilityScript.LvDirectHPDamage => ExecuteLevelDirectHP(actor, target, ability),
            AbilityScript.LvHoly => ExecuteLevelMagic(actor, target, ability, divided),
            AbilityScript.PreciseDirectHPDamage => ExecuteDirectHP(actor, target, ability, precise: true),
            AbilityScript.DirectHPDamage => ExecuteDirectHP(actor, target, ability, precise: false),
            AbilityScript.WhiteWind => ExecuteWhiteWind(actor, target, ability),
            AbilityScript.FlareStar => ExecuteFlareStar(actor, target, ability, divided),
            AbilityScript.Mini => ExecuteMini(actor, target, ability, divided),
            _ => Choreography(actor, target)
        };
        return ev with { Ability = ability };
    }
}

public sealed record BattleEvent(
    BattleUnit Actor,
    BattleUnit Target,
    AttackResult Result,
    BattleStatus StatusesAdded = 0,
    BattleAbility? Ability = null);
