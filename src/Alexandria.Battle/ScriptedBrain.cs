using Alexandria.Formats.Scripts;

namespace Alexandria.Battle;

/// <summary>An enemy mind run from its .eb bytecode. Init (tag 0) fires
/// once at construction, shuffling decks into VM vars; each Decide runs the atb
/// function (tag 5) and reads the verdict out of the host — the attack from
/// BTLCMD, the target from SV_Target. A script that ends with an empty target
/// mask falls back to self, matching the hand-ports.</summary>
public sealed class ScriptedBrain : IEnemyBrain
{
    private readonly EbVM _vm;
    private readonly EngineHost _host;
    private readonly Int32 _selfLine;
    private readonly EbEntry _entry;

    private const Int32 MainLoopLevel = 7; // cEventLevelN - 1, the idle priority
    
    /// <summary>The full SV_Target mask from the last Decide. A scripted attack may
    /// legally name several units at once and the engine fans out over this rather
    /// than the single unit Decide returns.</summary>
    public Int32 LastTargetMask { get; private set; }

    public ScriptedBrain(EbEntry entry, EngineHost host, Int32 selfLine, EbState? state = null)
    {
        _vm = new EbVM(entry, host, state);
        _host = host;
        _selfLine = selfLine;
        _entry = entry;
        host.BeginSlice(selfLine);
        _vm.RunFunction(0);
        _vm.Request(1, MainLoopLevel);
    }

    /// <summary>One frame of this mind's main loop — the boss's stage manager:
    /// it introduces its siblings through the shared map pool, rewrites its own
    /// stats, and polls for the phase changes its other functions branch on.</summary>
    public void Tick()
    {
        _host.BeginSlice(_selfLine);
        _vm.Tick();
    }
    
    public (Int32 AttackID, BattleUnit Target) Decide(BattleUnit self, IReadOnlyList<BattleUnit> players, Random rng)
    {
        _host.BeginSlice(_selfLine);
        _host.ChosenAttack = null;
        _vm.RunFunction(5);
        LastTargetMask = _host.SysList(0);
        var attack = _host.ChosenAttack
                     ?? throw new InvalidOperationException($"{self.Name}'s atb ended without a BTLCMD");
        return (attack, _host.UnitFromMask(LastTargetMask) ?? self);
    }

    /// <summary>Runs a reaction function if this mind has one (mirror:
    /// EventEngine.RequestAction - SysList[0] = the provoking attacker, sysvars
    /// 28/29 = the command that provoked it). Counter is tag 6, death is tag 9.
    /// Returns the attack the script chose to retaliate with, if any.</summary>
    public Int32? React(UInt16 tag, Int32 attackerMask, Int32 commandId, Int32 abilityId)
    {
        if (_entry.Functions.All(f => f.Tag != tag))
            return null;
        _host.BeginSlice(_selfLine);
        _host.SetSysList(0, attackerMask);
        _host.SeedReaction(commandId, abilityId);
        _host.ChosenAttack = null;
        _vm.RunFunction(tag);
        return _host.ChosenAttack;
    }
}

