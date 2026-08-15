namespace Alexandria.Battle.Tests;

/// <summary>Rigged dice: returns the given rolls in order, regardless of range.</summary>
sealed class RiggedDice(params Int32[] rolls) : Random
{
    private readonly Queue<Int32> _rolls = new(rolls);
    public override Int32 Next(Int32 minValue, Int32 maxValue) => _rolls.Dequeue();
}

