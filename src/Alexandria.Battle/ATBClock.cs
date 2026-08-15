namespace Alexandria.Battle;

public enum EncounterKind
{
    Normal,
    Preemptive,
    BackAttack
}

/// <summary>
/// The vanilla ATB clock (mirror: btl_para.cs + HonoluluBattleMain.ProcessActiveTime).
/// Everyone charges at the same rate; speed shrinks your gauge instead.
/// One tick = 1/15th of a second at the game's battle tick rate.
/// </summary>
public static class ATBClock
{
    // cfg.btl_speed 0/1/2 → coefficient (btl_para.GetATBCoef); in-game default is Normal.
    public const Int32 SlowCoefficient = 8;
    public const Int32 NormalCoefficient = 10;
    public const Int32 FastCoefficient = 14;

    /// <summary>Dead, acting, or already-full units don't charge. Overshoot is clamped, never carried.</summary>
    public static void Tick(BattleUnit unit, Int32 coefficient = NormalCoefficient)
    {
        if (!unit.IsAlive || unit.ATBReady)
            return;

        unit.ATBGauge = Math.Min(unit.ATBMax, unit.ATBGauge + coefficient);
    }

    public static void ResetAfterAction(BattleUnit unit) => unit.ATBGauge = 0;

    /// <summary>Battle-start placement (btl_init.cs:431-438, 498-506).
    /// The % rolls keep the shipped modulo bias on purpose.</summary>
    public static void PlaceInitialGauges(
        IReadOnlyList<BattleUnit> players, IReadOnlyList<BattleUnit> enemies,
        EncounterKind kind, Random rng)
    {
        foreach (var p in players)
        {
            p.ATBGauge = kind switch
            {
                EncounterKind.Preemptive => p.ATBMax - 1,
                EncounterKind.BackAttack => 0,
                _ => rng.Next(0, UInt16.MaxValue) % p.ATBMax
            };
        }

        foreach (var e in enemies)
            e.ATBGauge = kind == EncounterKind.Preemptive
                ? 0
                : rng.Next(0, UInt16.MaxValue) % e.ATBMax;
    }
}

