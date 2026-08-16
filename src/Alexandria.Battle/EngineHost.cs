using Alexandria.Formats.Scripts;

namespace Alexandria.Battle;

/// <summary>IEbHost over the live battle. Lines 0-3 are the party, 4-7 the enemy
/// formation (vanilla btl_id layout); a multi-bit mask resolves to its lowest
/// line, as RequestAction and FindBattleUnit do. Stat ids follow
/// btl_scrp.GetCharacterData.</summary>
public sealed class EngineHost(IReadOnlyList<BattleUnit> players, IReadOnlyList<BattleUnit> enemies, Random rng) : IEbHost
{
    private readonly UInt16[] _sysList = new UInt16[8];

    /// <summary>The attack id the last BTLCMD chose; ScriptedBrain resets and reads it.</summary>
    public Int32? ChosenAttack;

    /// <summary>Mirror: ProcessCodeExt seeds SV_FunctionEnemy with the running
    /// object's own mask before every slice of battle code.</summary>
    public void BeginSlice(Int32 line) => _sysList[1] = (UInt16)(1 << line);

    public BattleUnit? UnitFromMask(Int32 mask)
    {
        for (var line = 0; line < 8; line++)
            if ((mask >> line & 1) != 0)
                return Unit(line);
        return null;
    }

    private BattleUnit? Unit(Int32 line) => line < 4
        ? (line < players.Count ? players[line] : null)
        : (line - 4 < enemies.Count ? enemies[line - 4] : null);

    public Int32 Random8() => rng.Rand8();

    public Int32 Sysvar(Int32 id) => id == 0
        ? Random8()
        : throw new NotSupportedException($"sysvar {id} — teach me");

    public Int32 SysList(Int32 slot) => slot switch
    {
        2 => (1 << players.Count) - 1,
        3 => ((1 << enemies.Count) - 1) << 4,
        4 => ((1 << enemies.Count) - 1) << 4 | (1 << players.Count) - 1,
        _ => _sysList[slot]
    };

    public void SetSysList(Int32 slot, Int32 value) => _sysList[slot] = (UInt16)value;

    public Int32 ReadStat(Int32 mask, Int32 statId)
    {
        var unit = UnitFromMask(mask)
                   ?? throw new InvalidOperationException($"stat {statId} read from empty mask");
        return statId switch
        {
            35 => unit.MaxHP,
            36 => unit.CurrentHP,
            37 => unit.MaxMP,
            38 => unit.CurrentMP,
            39 => unit.ATBMax,
            40 => unit.ATBGauge,
            41 => unit.Level,
            46 => (Int32)unit.Statuses >>> 24,                    // STATUS_CURRENT_B: high 8
            47 => (Int32)((UInt32)unit.Statuses & 0xFFFFFF),      // STATUS_CURRENT_A: low 24
            48 => (Int32)unit.GuardElements,
            49 => (Int32)unit.AbsorbElements,
            50 => (Int32)unit.HalfElements,
            51 => (Int32)unit.WeakElements,
            _ => throw new NotSupportedException($"stat {statId} — teach me")
        };
    }

    public void WriteStat(Int32 mask, Int32 statId, Int32 value)
    {
        var unit = UnitFromMask(mask)
                   ?? throw new InvalidOperationException($"stat {statId} write to empty mask");
        switch (statId)
        {
            case 36: unit.CurrentHP = value; break;
            case 38: unit.CurrentMP = value; break;
            case 40: unit.ATBGauge = value; break;
            case 46: unit.Statuses = (BattleStatus)((UInt32)unit.Statuses & 0xFFFFFF | (UInt32)value << 24); break;
            case 47: unit.Statuses = (BattleStatus)((UInt32)unit.Statuses & 0xFF000000 | (UInt32)value & 0xFFFFFF); break;
            default: throw new NotSupportedException($"stat {statId} write — teach me");
        }
    }

    public Int32 RandomBit(Int32 mask) // mirror: OperatorSelect
    {
        var bits = new List<Int32>();
        for (var line = 0; line < 8; line++)
            if ((mask >> line & 1) != 0)
                bits.Add(line);
        if (bits.Count == 0)
            return 0;
        return 1 << bits[(bits.Count * Random8()) >> 8];
    }

    public void Attack(Int32 attackId) => ChosenAttack = attackId;
}

