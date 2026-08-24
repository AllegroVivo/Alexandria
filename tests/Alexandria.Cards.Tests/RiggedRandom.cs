namespace Alexandria.Cards.Tests;

/// <summary>Hands back the rolls it was given, then throws.</summary>
sealed class RiggedRandom(params Int32[] rolls) : Random
{
    private readonly Queue<Int32> _rolls = new(rolls);
    public override Int32 Next(Int32 minValue, Int32 maxValue) => _rolls.Dequeue();
}

