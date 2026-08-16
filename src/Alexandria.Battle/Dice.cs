namespace Alexandria.Battle;

/// <summary>The vanilla RNG primitive (mirror: Comn.random16 = Random.Range(0, 65536)).
/// Every battle roll must flow through this one method — it routes to Next(min, max),
/// the single overload the test dice override.</summary>
public static class Dice
{
    public static Int32 Rand16(this Random rng) => rng.Next(0, UInt16.MaxValue + 1);
}

