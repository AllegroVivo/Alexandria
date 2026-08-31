using Alexandria.Formats.Scripts;

namespace Alexandria.Battle;

public sealed class FieldHost(WorldState world, FieldContext context, Random rng) : IEbHost
{
    public Int32 Random8() => rng.Rand8();

    public Int32 Sysvar(Int32 id) => id switch
    {
        0 => Random8(),
        2 => context.UserControl,
        6 => world.Gil,
        16 => world.Frogs,
        33 => world.Gil,
        _ => throw new NotSupportedException($"field sysvar {id} — teach me")
    };

    public Int32 SysList(Int32 slot) => 0;
    public void SetSysList(Int32 slot, Int32 value) { }
    public Int32 RandomBit(Int32 mask) => mask;
    public void Spawn(Int32 entry, Int32 uid) { }

    public Int32 ReadStat(Int32 targetMask, CharacterStat stat)
        => throw new NotSupportedException($"field stat read {stat} — teach me");
    public void WriteStat(Int32 targetMask, CharacterStat stat, Int32 value)
        => throw new NotSupportedException($"field stat write {stat} — teach me");

    public void Attack(Int32 attackId) => throw new NotSupportedException("Attack on the field.");
    public void AttackSpecial(Int32 attackId) => throw new NotSupportedException("AttackSpecial on the field");
    public void BattleSet(Int32 verb, Int32 value) => throw new NotSupportedException("BattleSet on the field");
    public void BattleDialog(Int32 textId) { }
    public void SetTextVariable(Int32 scriptId, Int32 value) { }

    /// <summary>A property of another object (mirror: EBin.getvobj). Uids 250-254 are indirect:
    /// 250 is whoever the player is driving, 251-254 the party slots. 255 (self) is resolved by
    /// the VM before it gets here.</summary>
    public Int32 ReadObjectStat(Int32 uid, CharacterStat stat)
    {
        var target = uid switch
        {
            250 => context.ControlUID,
            >= 251 and <= 254 => context.PartyUID[uid - 251],
            _ => uid
        };

        return (Int32)stat switch
        {
            5 => target,
            _ => throw new NotSupportedException($"object {target} property {stat} — teach me")
        };
    }
    
    /// <summary>Opcodes the VM sized but does not model. Nine of them write the field context
    /// (mirror: EventContext); everything else is still unlearned and says so.</summary>
    public void FieldOp(Int32 opcode, IReadOnlyList<Int32> args)
    {
        switch (opcode)
        {
            case 0x2D: context.UserControl = 0; break;          // UCOFF  DisableMove
            case 0x2E: context.UserControl = 1; break;          // UCON   EnableMove
            case 0x2F: break;                                   // MODEL - sets the object's model and eye height
            case 0x9E: context.UserControl = 0; break;          // MOVQ   ExitField
            case 0x57: context.EncounterRatio = args[0]; break; // ENCRATE
            case 0x67:                                          // TWIST  SetControlDirection
                context.TwistArrow = args[0];
                context.TwistStick = args[1];
                break;
            case 0x90: context.IdleTimer = -1; break;           // SLEEPINH
            case 0xEE: context.IdleTimer = 0; break;            // SLEEPON
            case 0x6A: context.DashInhibited = 1; break;        // DASHOFF
            case 0xF0: context.DashInhibited = 0; break;        // DASHON
            default:
                throw new NotSupportedException($"field opcode 0x{opcode:X2} — teach me");
        }
    }
}

