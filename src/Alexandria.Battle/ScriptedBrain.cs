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

    public ScriptedBrain(EbEntry entry, EngineHost host, Int32 selfLine)
    {
        _vm = new EbVM(entry, host);
        _host = host;
        _selfLine = selfLine;
        host.BeginSlice(selfLine);
        _vm.RunFunction(0);
    }
    
    public (Int32 AttackID, BattleUnit Target) Decide(BattleUnit self, IReadOnlyList<BattleUnit> players, Random rng)
    {
        _host.BeginSlice(_selfLine);
        _host.ChosenAttack = null;
        _vm.RunFunction(5);
        var attack = _host.ChosenAttack
                     ?? throw new InvalidOperationException($"{self.Name}'s atb ended without a BTLCMD");
        return (attack, _host.UnitFromMask(_host.SysList(0)) ?? self);
    }
}

