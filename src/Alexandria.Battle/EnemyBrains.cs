namespace Alexandria.Battle;

/// <summary>Hand-ported minds, one factory per enemy, tables decoded from each
/// script's init function by the disassembler.</summary>
public static class EnemyBrains
{
    public static IEnemyBrain? For(String name, Random rng) => name switch
    {
        "Type C" => TypeC(rng),
        "Bandersnatch" => Bandersnatch(rng),
        _ => null
    };

    /// <summary>EVT_BATTLE_AC_E028F entry 1: Strike, Fira, Blizzara, Thundara.</summary>
    public static IEnemyBrain TypeC(Random rng) => new ShuffledDeckBrain(rng,
        attackIDs: [0, 1, 2, 3],
        mpCosts: [0, 12, 12, 12],
        targets: [CardTarget.RandomPlayer, CardTarget.RandomPlayer, CardTarget.RandomPlayer, CardTarget.RandomPlayer]);

    /// <summary>EVT_BATTLE_AC_E028F entry 2: Rush ×2, Thundara, Tongue - Tongue
    /// never targets a player already asleep.</summary>
    public static IEnemyBrain Bandersnatch(Random rng) => new ShuffledDeckBrain(rng,
        attackIDs: [4, 4, 5, 6],
        mpCosts: [0, 0, 12, 0],
        targets: [CardTarget.RandomPlayer, CardTarget.RandomPlayer, CardTarget.RandomPlayer, CardTarget.RandomAwakePlayer]);
}

