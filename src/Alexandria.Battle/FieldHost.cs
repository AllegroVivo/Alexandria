using Alexandria.Formats.Scripts;
using Alexandria.Rules;

namespace Alexandria.Battle;

public sealed class FieldHost(WorldState world, FieldContext context, Random rng) : IEbHost
{
    private readonly Dictionary<Int32, FieldObject> _objects = [];
    private readonly List<(Int32 Entry, Int32 Uid)> _spawns = [];
        
    public Int32 Random8() => rng.Rand8();

    public Int32 Sysvar(Int32 id) => id switch
    {
        0 => Random8(),
        2 => context.UserControl,
        3 => 0, 
        6 => world.Gil,
        9 => context.LastChoice,
        12 => 0, 
        13 => 0,
        16 => world.Frogs,
        20 => 0,
        33 => world.Gil,
        _ => throw new NotSupportedException($"field sysvar {id} — teach me")
    };

    public Int32 SysList(Int32 slot) => 0;
    public void SetSysList(Int32 slot, Int32 value) { }
    public Int32 RandomBit(Int32 mask) => mask;
    public void Spawn(Int32 entry, Int32 uid) => _spawns.Add((entry, uid));

    public IReadOnlyList<(Int32 Entry, Int32 Uid)> DrawnSpawns()
    {
        var taken = _spawns.ToArray();
        _spawns.Clear();
        return taken;
    }

    public Int32 ReadStat(Int32 targetMask, CharacterStat stat)
        => throw new NotSupportedException($"field stat read {stat} — teach me");
    public void WriteStat(Int32 targetMask, CharacterStat stat, Int32 value)
        => throw new NotSupportedException($"field stat write {stat} — teach me");

    public Int32 PartyCheck(Int32 character) => context.Party.Contains(character) ? 1 : 0;

    public void Attack(Int32 attackId) => throw new NotSupportedException("Attack on the field.");
    public void AttackSpecial(Int32 attackId) => throw new NotSupportedException("AttackSpecial on the field");
    public void BattleSet(Int32 verb, Int32 value) => throw new NotSupportedException("BattleSet on the field");
    public void BattleDialog(Int32 textId) { }
    public void SetTextVariable(Int32 scriptId, Int32 value) { }

    private FieldObject Object(Int32 uid)
        => _objects.TryGetValue(uid, out var o) ? o : _objects[uid] = new FieldObject();

    private Int32 Resolve(Int32 uid) => uid switch
    {
        250 => context.ControlUID,
        >= 251 and <= 254 => context.PartyUID[uid - 251],
        _ => uid
    };

    public Int32 Distance(Int32 fromUid, Int32 toUid)
    {
        var from = Object(Resolve(fromUid));
        var to = Object(Resolve(toUid));
        Double deltaX = to.X - from.X, deltaZ = to.Z - from.Z;
        return (Int32)Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
    }

    public Int32 ItemCount(Int32 item) => world.Items[(ItemID)item];

    /// <summary>A property of another object (mirror: EBin.getvobj). Uids 250-254 are indirect:
    /// 250 is whoever the player is driving, 251-254 the party slots. 255 (self) is resolved by
    /// the VM before it gets here.</summary>
    public Int32 ReadObjectStat(Int32 uid, CharacterStat stat)
    {
        var target = Resolve(uid);
        var obj = Object(target);

        return (Int32)stat switch
        {
            0 => obj.X,
            1 => obj.Y,
            2 => obj.Z,
            3 => obj.Angle,
            4 => obj.Flags,
            5 => target,          // getvobj type 5: the object's own uid
            6 => obj.Level,
            7 => obj.AnimFrame,
            _ => throw new NotSupportedException($"object {target} property {stat} — teach me")
        };
    }
    
    /// <summary>Opcodes the VM sized but does not model. Nine of them write the field context
    /// (mirror: EventContext); everything else is still unlearned and says so.</summary>
    public void FieldOp(Int32 opcode, Int32 uid, IReadOnlyList<Int32> args)
    {
        switch (opcode)
        {
            case 0x20: Object(uid).Window = args[0]; break;     // MESN: speak without waiting.
            case 0x29: break;                                   // QUAD: nothing asks where a region is yet
            case 0x2C: context.ControlUID = uid; break;         // CC - the player now drives this object
            case 0x2D: context.UserControl = 0; break;          // UCOFF  DisableMove
            case 0x2E: context.UserControl = 1; break;          // UCON   EnableMove
            case 0x2F: break;                                   // MODEL - sets the object's model and eye height
            case 0x36: Object(uid).Angle = args[0]; break;      // DIRE - turn the running object
            case 0x4B: break;                                   // RADIUS - pathing size, collision and talk radii
            case 0x54: Object(uid).Window = args[0]; break;     // WAITMES: wait for that window to be dismissed.
            case 0x57: context.EncounterRatio = args[0]; break; // ENCRATE
            case 0x67:                                          // TWIST  SetControlDirection
                context.TwistArrow = args[0];
                context.TwistStick = args[1];
                break;
            case 0x6A: context.DashInhibited = 1; break;        // DASHOFF
            case 0x90: context.IdleTimer = -1; break;           // SLEEPINH
            case 0x93:                                          // CFLAG: the low six bits are the script's; the top two belong to the engine
                Object(uid).Flags = Object(uid).Flags & ~63 | args[0] & 63;
                break;
            case 0x9E: context.UserControl = 0; break;          // MOVQ   ExitField
            case 0xAA: context.MenuEnabled = true; break;       // MENUON
            case 0xAB: context.MenuEnabled = false; break;      // MENUOFF
            case 0xD4: break;                                   // ATTACHOFFSET - a graphics attachment offset
            case 0xEE: context.IdleTimer = 0; break;            // SLEEPON
            case 0xF0: context.DashInhibited = 0; break;        // DASHON
            default:
                throw new NotSupportedException($"field opcode 0x{opcode:X2} — teach me");
        }
    }
}

