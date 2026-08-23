using System.Numerics;

namespace Alexandria.Battle;

public sealed class KillCounts
{
    public const Int16 Cap = 9999;

    private readonly Int16[] _tallies = new Int16[8];

    public Int16 this[EnemyCategory category]
    {
        get => _tallies[Slot(category)];
        set => _tallies[Slot(category)] = Math.Clamp(value, (Int16)0, Cap);
    }

    private static Int32 Slot(EnemyCategory category)
    {
        Int32 bits = (Byte)category;
        if (!BitOperations.IsPow2(bits))
            throw new ArgumentOutOfRangeException(nameof(category), category,
                "A tally belongs to exactly one category bit.");
        return BitOperations.TrailingZeroCount(bits);
    }
}

