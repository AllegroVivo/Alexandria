namespace Alexandria.Battle;

/// <summary>A ported enemy AI. Returns the scene attack pool index and the
/// chosen target; the engine resolves and executes. Enemies without a brain fall
/// back to the uniform-random placeholder.</summary>
public interface IEnemyBrain
{
    (Int32 AttackID, BattleUnit Target) Decide(BattleUnit self,
        IReadOnlyList<BattleUnit> players, Random rng);
}

