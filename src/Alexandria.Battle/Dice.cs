namespace Alexandria.Battle;

/// <summary>The vanilla RNG primitive (mirror: Comn.random16 = Random.Range(0, 65536)).
/// Every battle roll must flow through this one method — it routes to Next(min, max),
/// the single overload the test dice override.</summary>
public static class Dice
{
    extension(Random rng)
    {
        /// <summary>Vanilla random8 (mirror: Comn.random8 = Random.Range(0, 256)) — the
        /// script VM's die, distinct from the battle formulas' rand16.</summary>
        public Int32 Rand8() => rng.Next(0, 256);

        public Int32 Rand16() => rng.Next(0, UInt16.MaxValue + 1);
    }
}

