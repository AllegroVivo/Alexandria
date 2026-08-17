using Alexandria.Formats.Scripts;

namespace Alexandria.Battle;

/// <summary>IEbHost over the live battle. Lines 0-3 are the party, 4-7 the enemy
/// formation (vanilla btl_id layout); a multi-bit mask resolves to its lowest
/// line, as RequestAction and FindBattleUnit do. Stat ids follow
/// btl_scrp.GetCharacterData.</summary>
public sealed class EngineHost(IReadOnlyList<BattleUnit> players, IReadOnlyList<BattleUnit> enemies, Random rng) : IEbHost
{
    private Int32 _reactCommand;
    private Int32 _reactAbility;
    private readonly UInt16[] _sysList = new UInt16[8];

    /// <summary>Text ids queued by MESB (0xD0); the runner drains and prints them.</summary>
    public readonly List<Int32> PendingDialogs = [];
    
    /// <summary>The attack id the last BTLCMD chose; ScriptedBrain resets and reads it.</summary>
    public Int32? ChosenAttack;

    /// <summary>Mirror: ProcessCodeExt seeds SV_FunctionEnemy with the running
    /// object's own mask before every slice of battle code.</summary>
    public void BeginSlice(Int32 line) => _sysList[1] = (UInt16)(1 << line);

    /// <summary>Mirror: RequestAction stores _btlCmdPrmCmd/_btlCmdPrmSub before a
    /// counter (tag 6) or death (tag 9) function runs; Sysvar 28/29 hand them to
    /// the script at reaction levels (GetSysvar.cs: gExec.level <= 2 branch).</summary>
    public void SeedReaction(Int32 commandId, Int32 abilityId)
    {
        _reactCommand = commandId;
        _reactAbility = abilityId;
    }

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

    public Int32 Sysvar(Int32 id) => id switch
    {
        0 => Random8(),
        25 => 0, // SeqBusy: headless runs no animation sequences
        28 => _reactCommand, // CurCommand: what provoked the reaction
        29 => _reactAbility, // CurAbility: its ability/sub id
        30 => 4, // BattlePhase: the in-battle phase every wait-loop waits for
        36 => -1, // Attacker: GetBattleData returns UInt32.MaxValue → -1 when no command is in flight
        _ => throw new NotSupportedException($"sysvar {id} — teach me")
    };

    public Int32 SysList(Int32 slot) => slot switch
    {
        2 => (1 << players.Count) - 1,
        3 => ((1 << enemies.Count) - 1) << 4,
        4 => ((1 << enemies.Count) - 1) << 4 | (1 << players.Count) - 1,
        _ => _sysList[slot]
    };

    public void SetSysList(Int32 slot, Int32 value) => _sysList[slot] = (UInt16)value;

    public void BattleDialog(Int32 textId) => PendingDialogs.Add(textId);

    public Int32 ReadStat(Int32 mask, Int32 statId)
    {
        var unit = UnitFromMask(mask);
        if (unit is null)
            return 0;
        return statId switch
        {
            35 => unit.MaxHP,
            36 => unit.CurrentHP,
            37 => unit.MaxMP,
            38 => unit.CurrentMP,
            39 => unit.ATBMax,
            40 => unit.ATBGauge,
            41 => unit.Level,
            42 => (Int32)unit.ImmuneStatuses >>> 24,               // STATUS_IMMUNE_B
            43 => (Int32)((UInt32)unit.ImmuneStatuses & 0xFFFFFF), // STATUS_IMMUNE_A
            44 => (Int32)unit.PermanentStatuses >>> 24,            // STATUS_AUTO_B
            45 => (Int32)((UInt32)unit.PermanentStatuses & 0xFFFFFF),
            46 => (Int32)unit.Statuses >>> 24,                     // STATUS_CURRENT_B
            47 => (Int32)((UInt32)unit.Statuses & 0xFFFFFF),
            48 => (Int32)unit.GuardElements,
            49 => (Int32)unit.AbsorbElements,
            50 => (Int32)unit.HalfElements,
            51 => (Int32)unit.WeakElements,
            64 => unit.Row,
            65 => Int32.TrailingZeroCount(mask),                   // COLUMN = bi.line_no = the line itself
            66 => unit.ModelType,
            67 or 68 => unit.Category,   // vanilla: 67 player-side, 68 enemy-side — one field serves both
            69 => unit.StandAnimation,
            72 => unit.Strength,
            73 => unit.Magic,
            74 => unit.PhysicalDefense,
            75 => unit.PhysicalEvade,
            76 => unit.MagicalDefense,
            77 => unit.MagicalEvade,
            // write-only verbs read back as vanilla's no-case default (btl_scrp.cs:197)
            32 or 33 or 34 or (>= 54 and <= 56) or (>= 59 and <= 63) or 71 or 78 => 0xFFFFFF,
            _ => throw new NotSupportedException($"stat {statId} — teach me")
        };
    }

    public void WriteStat(Int32 mask, Int32 statId, Int32 value)
    {
        var unit = UnitFromMask(mask);
        if (unit is null)
            return;
        switch (statId)
        {
        case 35: unit.MaxHP = value; break;
        case 36: unit.CurrentHP = value; break;
        case 37: unit.MaxMP = value; break;
        case 38: unit.CurrentMP = value; break;
        case 40: unit.ATBGauge = value; break; // vanilla adds an Ozma speed-config guard here
        case 42: unit.ImmuneStatuses = (BattleStatus)((UInt32)unit.ImmuneStatuses & 0xFFFFFF | (UInt32)value << 24); break;
        case 43: unit.ImmuneStatuses = (BattleStatus)((UInt32)unit.ImmuneStatuses & 0xFF000000 | (UInt32)value & 0xFFFFFF); break;
        case 44: SetPermanent(unit, (UInt32)unit.PermanentStatuses & 0xFFFFFF | (UInt32)value << 24); break;
        case 45: SetPermanent(unit, (UInt32)unit.PermanentStatuses & 0xFF000000 | (UInt32)value & 0xFFFFFF); break;
        case 46: unit.Statuses = (BattleStatus)((UInt32)unit.Statuses & 0xFFFFFF | (UInt32)value << 24); break;
        case 47: unit.Statuses = (BattleStatus)((UInt32)unit.Statuses & 0xFF000000 | (UInt32)value & 0xFFFFFF); break;
        case 48: unit.GuardElements = (Element)value; break;
        case 49: unit.AbsorbElements = (Element)value; break;
        case 50: unit.HalfElements = (Element)value; break;
        case 51: unit.WeakElements = (Element)value; break;
        case 69: unit.StandAnimation = value; break; // plain store, no side call — vanilla applies it at the next idle
        case 72: unit.Strength = value; break;
        case 73: unit.Magic = value; break;
        case 74: unit.PhysicalDefense = value; break;
        case 75: unit.PhysicalEvade = value; break;
        case 76: unit.MagicalDefense = value; break;
        case 77: unit.MagicalEvade = value; break;
        // rendering & roster verbs a headless sim honors as no-ops (vanilla intent in comments):
        case 32 or 33: break;         // show/hide unit + add/remove from roster (btl_sys)
        case 34: break;               // KillStandardCommands — the sim queues no commands
        case 52 or 53: break;         // tar_mode request / SetDisappear — no targeting model yet
        case 54: break;               // shadow toggle
        case 55 or 56: break;         // model scale set/reset (the tag-7 Mini handlers)
        case >= 59 and <= 63: break;  // mesh hide/show, texture anims
        case 71: break;               // SetBonus — reward banking not modeled
        default: throw new NotSupportedException($"stat {statId} write — teach me");
        }
    }

    /// <summary>Mirror-lite of btl_stat.MakeStatusesPermanent: the write diffs the
    /// mask — what fell off is removed (from current too), what came on is inflicted.
    /// The full AlterStatus immunity/opposite machinery is a future fidelity bite.</summary>
    private static void SetPermanent(BattleUnit unit, UInt32 mask)
    {
        var target = (BattleStatus)mask;
        var removed = unit.PermanentStatuses & ~target;
        unit.PermanentStatuses = target;
        unit.Statuses = (unit.Statuses & ~removed) | target;
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

