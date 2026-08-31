using Alexandria.Formats.Scripts;

namespace Alexandria.Battle;

/// <summary>IEbHost over the live battle. Lines 0-3 are the party, 4-7 the enemy
/// formation (vanilla btl_id layout); a multi-bit mask resolves to its lowest
/// line, as RequestAction and FindBattleUnit do. Stat ids follow
/// btl_scrp.GetCharacterData.</summary>
public sealed class EngineHost(IReadOnlyList<BattleUnit> players, IReadOnlyList<BattleUnit> enemies, Random rng,
    IReadOnlyDictionary<BattleStatus, StatusTiming>? statusTable = null) : IEbHost
{
    private Int32 _reactCommand;
    private Int32 _reactAbility;
    private readonly UInt16[] _sysList = new UInt16[8];
    
    private readonly IReadOnlyDictionary<BattleStatus, StatusTiming> _statusTable = statusTable ?? StatusTicker.DefaultTable;

    /// <summary>Mirror: FF9StateBattleSystem.btl_phase (:10-18). A battle opens in
    /// PHASE_EVENT — the scripts' window — and only reaches PHASE_NORMAL when a
    /// director calls EnableATB.</summary>
    public const Int32 PhaseEvent = 1;
    public const Int32 PhaseNormal = 4;

    public Int32 Phase { get; set; } = PhaseEvent;

    /// <summary>Text ids queued by MESB (0xD0); the runner drains and prints them.</summary>
    public readonly List<Int32> PendingDialogs = [];

    /// <summary>Moves fired by BTLSEQ: the enemy performs an attack's sequence
    /// immediately, bypassing the command queue, on the current SV_Target. Vanilla
    /// plays the animation and lets its seq-codes do any damage.</summary>
    public readonly List<(Int32 AttackID, Int32 TargetMask)> PendingSpecials = [];

    /// <summary>Numbers the scripts interpolate into battle dialog (the Ragtime Mouse's
    /// question number, a gil payout).</summary>
    public readonly Dictionary<Int32, Int32> TextVariables = [];

    /// <summary>The attack id the last BTLCMD chose; ScriptedBrain resets and reads it.</summary>
    public Int32? ChosenAttack;

    /// <summary>Set by EndBattle/GameOver — vanilla's btl_result: 1 victory,
    /// 2 victory without pose, 3 defeat, 4 escape, 7 enemy flee (FF9StateGlobal.cs:10-17).</summary>
    public Int32? Result;

    public Int32 GilEarned;
    
    /// <summary>Mirror: sysvar 31 = btl_scene.PatNum — which SB2_PATTERN formation this
    /// encounter uses.</summary>
    public Int32 Pattern { get; set; }

    /// <summary>Entries spawned by the director's init, in order. Mirror: StartEvents
    /// seats the party in _objPtrList[0..3] then hard-resets _opLStart to 4, so the Nth
    /// spawn in battle mode takes battle line 4 + N.</summary>
    public readonly List<Int32> SpawnedEntries = [];

    /// <summary>Mirror: ProcessCodeExt seeds SV_FunctionEnemy with the running
    /// object's own mask before every slice of battle code.</summary>
    public void BeginSlice(Int32 line) => _sysList[1] = (UInt16)(line is >= 0 and < 8 ? 1 << line : 0);

    public void AttackSpecial(Int32 attackId) => PendingSpecials.Add((attackId, _sysList[0]));
    public void SetTextVariable(Int32 scriptId, Int32 value) => TextVariables[scriptId] = value;

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
        17 => 0, // TimerUI.Time
        21 => 0, // TimerUI.Enable
        25 => 0, // SeqBusy: headless runs no animation sequences
        28 => _reactCommand, // CurCommand: what provoked the reaction
        29 => _reactAbility, // CurAbility: its ability/sub id
        30 => Phase, // BattlePhase: the in-battle phase every wait-loop waits for
        31 => Pattern,
        34 => Phase, // same field via GetBattleData
        36 => -1, // Attacker: GetBattleData returns UInt32.MaxValue → -1 when no command is in flight
        40 => 0x7F, // LoadStatus (btl_load_status): BBG|NPC|CHR|INITNPC|INITCHR|FADENPC|FADECHR
        // all set — a headless battle has no loading screen to wait through
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

    public void Spawn(Int32 entry, Int32 uid) => SpawnedEntries.Add(entry);

    public Int32 ReadStat(Int32 mask, CharacterStat stat)
    {
        var unit = UnitFromMask(mask);
        if (unit is null)
            return 0;
        return stat switch
        {
            CharacterStat.MaxHP => unit.MaxHP,
            CharacterStat.HP => unit.CurrentHP,
            CharacterStat.MaxMP => unit.MaxMP,
            CharacterStat.MP => unit.CurrentMP,
            CharacterStat.MaxATB => unit.ATBMax,
            CharacterStat.ATB => unit.ATBGauge,
            CharacterStat.Level => unit.Level,
            CharacterStat.StatusImmuneB => (Int32)unit.ImmuneStatuses >>> 24, // STATUS_IMMUNE_B
            CharacterStat.StatusImmuneA => (Int32)((UInt32)unit.ImmuneStatuses & 0xFFFFFF), // STATUS_IMMUNE_A
            CharacterStat.StatusAutoB => (Int32)unit.PermanentStatuses >>> 24, // STATUS_AUTO_B
            CharacterStat.StatusAutoA => (Int32)((UInt32)unit.PermanentStatuses & 0xFFFFFF),
            CharacterStat.StatusCurrentB => (Int32)unit.Statuses >>> 24, // STATUS_CURRENT_B
            CharacterStat.StatusCurrentA => (Int32)((UInt32)unit.Statuses & 0xFFFFFF),
            CharacterStat.ElementImmune => (Int32)unit.GuardElements,
            CharacterStat.ElementAbsorb => (Int32)unit.AbsorbElements,
            CharacterStat.ElementHalf => (Int32)unit.HalfElements,
            CharacterStat.ElementWeak => (Int32)unit.WeakElements,
            CharacterStat.Model => unit.ModelID,
            CharacterStat.Row => unit.Row,
            CharacterStat.Column => Int32.TrailingZeroCount(mask), // COLUMN = bi.line_no = the line itself
            CharacterStat.ModelType => unit.ModelType,
            CharacterStat.Category => (Int32)unit.PlayerCategory,
            CharacterStat.Class => (Int32)unit.Category,
            CharacterStat.StandAnimation => unit.StandAnimation,
            CharacterStat.EnemyID => Int32.TrailingZeroCount(mask) % 4, // ENEMY_ID
            CharacterStat.Strength => unit.Strength,
            CharacterStat.Magic => unit.Magic,
            CharacterStat.Defence => unit.PhysicalDefense,
            CharacterStat.Evade => unit.PhysicalEvade,
            CharacterStat.MagicalDefence => unit.MagicalDefense,
            CharacterStat.MagicalEvade => unit.MagicalEvade,
            // write-only verbs read back as vanilla's no-case default (btl_scrp.cs:197)
            CharacterStat.Appear or CharacterStat.Disappear or CharacterStat.KillCommands
                or CharacterStat.Shadow or CharacterStat.ModelScale or CharacterStat.ModelScaleReset
                or CharacterStat.HideMesh or CharacterStat.ShowMesh
                or CharacterStat.TextureAnimPlay or CharacterStat.TextureAnimStop
                or CharacterStat.TextureAnimOnce or CharacterStat.SetBonus or CharacterStat.SummonEiko
                => 0xFFFFFF,
            _ => throw new NotSupportedException($"stat {stat} — teach me")
        };
    }

    public void WriteStat(Int32 mask, CharacterStat stat, Int32 value)
    {
        var unit = UnitFromMask(mask);
        if (unit is null)
            return;
        switch (stat)
        {
            case CharacterStat.MaxHP: unit.MaxHP = value; break;
            case CharacterStat.HP: unit.CurrentHP = value; break;
            case CharacterStat.MaxMP: unit.MaxMP = value; break;
            case CharacterStat.MP: unit.CurrentMP = value; break;
            case CharacterStat.ATB: unit.ATBGauge = value; break; // vanilla adds an Ozma speed-config guard here
            case CharacterStat.StatusImmuneB: unit.ImmuneStatuses = (BattleStatus)((UInt32)unit.ImmuneStatuses & 0xFFFFFF | (UInt32)value << 24); break;
            case CharacterStat.StatusImmuneA: unit.ImmuneStatuses = (BattleStatus)((UInt32)unit.ImmuneStatuses & 0xFF000000 | (UInt32)value & 0xFFFFFF); break;
            case CharacterStat.StatusAutoB: SetPermanent(unit, (UInt32)unit.PermanentStatuses & 0xFFFFFF | (UInt32)value << 24); break;
            case CharacterStat.StatusAutoA: SetPermanent(unit, (UInt32)unit.PermanentStatuses & 0xFF000000 | (UInt32)value & 0xFFFFFF); break;
            case CharacterStat.StatusCurrentB: WriteStatuses(unit, 0xFF000000, ((UInt32)value & 0xFF) << 24); break;
            case CharacterStat.StatusCurrentA: WriteStatuses(unit, 0x00FFFFFF, (UInt32)value & 0xFFFFFF); break;
            case CharacterStat.ElementImmune: unit.GuardElements = (Element)value; break;
            case CharacterStat.ElementAbsorb: unit.AbsorbElements = (Element)value; break;
            case CharacterStat.ElementHalf: unit.HalfElements = (Element)value; break;
            case CharacterStat.ElementWeak: unit.WeakElements = (Element)value; break;
            case CharacterStat.StandAnimation: unit.StandAnimation = value; break; // plain store, no side call — vanilla applies it at the next idle
            case CharacterStat.Strength: unit.Strength = value; break;
            case CharacterStat.Magic: unit.Magic = value; break;
            case CharacterStat.Defence: unit.PhysicalDefense = value; break;
            case CharacterStat.Evade: unit.PhysicalEvade = value; break;
            case CharacterStat.MagicalDefence: unit.MagicalDefense = value; break;
            case CharacterStat.MagicalEvade: unit.MagicalEvade = value; break;
            // rendering & roster verbs a headless sim honors as no-ops (vanilla intent in comments):
            case CharacterStat.Appear or CharacterStat.Disappear: break; // show/hide unit + add/remove from roster (btl_sys)
            case CharacterStat.KillCommands: break; // KillStandardCommands — the sim queues no commands
            case CharacterStat.Targetable or CharacterStat.Vanished: break; // tar_mode request / SetDisappear — no targeting model yet
            case CharacterStat.Shadow: break; // shadow toggle
            case CharacterStat.ModelScale or CharacterStat.ModelScaleReset: break; // model scale set/reset (the tag-7 Mini handlers)
            case >= CharacterStat.HideMesh and <= CharacterStat.TextureAnimOnce: break; // mesh hide/show, texture anims
            case CharacterStat.SetBonus: break; // SetBonus — reward banking not modeled
            default: throw new NotSupportedException($"stat {stat} write — teach me");
        }
    }

    /// <summary>Mirror-lite of btl_stat.MakeStatusesPermanent: the write diffs the
    /// mask — what fell off is removed (from current too), what came on is inflicted.
    /// The full AlterStatus immunity/opposite machinery is a future fidelity bite.</summary>
    private void SetPermanent(BattleUnit unit, UInt32 mask)
    {
        var target = (BattleStatus)mask;
        StatusApplication.Remove(unit, unit.PermanentStatuses & ~target);
        unit.PermanentStatuses = StatusApplication.Apply(unit, target, _statusTable);
    }
    
    /// <summary>A script writing a status window (mirror: btl_scrp.SetCharacterData cases 46/47,
    /// which call RemoveStatuses then AlterStatuses rather than storing the word).</summary>
    private void WriteStatuses(BattleUnit unit, UInt32 window, UInt32 wanted)
    {
        StatusApplication.Remove(unit, (BattleStatus)((UInt32)unit.Statuses & window & ~wanted));
        StatusApplication.Apply(unit, (BattleStatus)wanted, _statusTable);
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

    public void FieldOp(Int32 opcode, IReadOnlyList<Int32> args) => throw new NotSupportedException();

    public void BattleSet(Int32 verb, Int32 value)
    {
        switch (verb)
        {
            case 32: // DisableATB
                Phase = PhaseEvent;
                break;
            case 33 when Phase == PhaseEvent: // EndBattle
                Result = value;
                break;
            case 34 when Phase == PhaseEvent: // GameOver
                Result = 3;
                break;
            case 35 when Phase == PhaseEvent: // EnableATB
                Phase = PhaseNormal;
                break;
            case 38: // AddGil
                GilEarned += value;
                break;
        }
    }
}
