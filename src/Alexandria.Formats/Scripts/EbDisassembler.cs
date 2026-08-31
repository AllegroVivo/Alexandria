using System.Diagnostics;

namespace Alexandria.Formats.Scripts;

/// <summary>
/// Linear disassembler for one .eb function (mirror: EBin.next/jumpToCommand +
/// EventEngine.DoEventCode). Instruction lengths are self-describing: native low
/// opcodes have bespoke layouts; handler opcodes read one flag byte then one
/// field per argument - an inline immediate (width fixed per opcode) when the
/// flag bit is 0, an expression stream when it is 1. An opcode we cannot size
/// halts the function honestly; the census aggregates blockers so we know which
/// opcodes to teach it next.
/// </summary>
public static class EbDisassembler
{
    public static List<String> Disassemble(EbFunction fn,
        IReadOnlyDictionary<Int32, String>? attackNames = null,
        IReadOnlyList<String>? battleText = null)
        => Disassemble(fn, out _, attackNames, battleText);

    public static List<String> Disassemble(EbFunction fn, out Int32? blockedOpcode,
        IReadOnlyDictionary<Int32, String>? attackNames = null,
        IReadOnlyList<String>? battleText = null)
    {
        blockedOpcode = null;
        var lines = new List<String>();
        var code = fn.Code;
        var pos = 0; 
        var op = 0;

        try
        {
            while (pos < code.Length)
            {
                var at = fn.EntryPoint + pos; // matches engine's ip values
                op = 0;
                while (pos < code.Length && code[pos] == 0xFF)
                {
                    op += 0x100; // each 0xFF prefix is an opcode page
                    pos++;
                }
                op += code[pos++];

                switch (op)
                {
                    case 0x00:
                        lines.Add($"@{at:D4}: nop");
                        break;

                    case 0x01: // signed offset - 0x02's is unsigned, vanilla quirk
                    {
                        var offset = (Int16)(code[pos] | code[pos + 1] << 8);
                        pos += 2;
                        lines.Add($"@{at:D4}: jump -> @{fn.EntryPoint + pos + offset:D4}");
                        break;
                    }

                    case 0x02:
                    {
                        var offset = (UInt16)(code[pos] | code[pos + 1] << 8);
                        pos += 2;
                        lines.Add($"@{at:D4}: ifnot -> @{fn.EntryPoint + pos + offset:D4}");
                        break;
                    }

                    case 0x03:
                    {
                        var offset = (Int16)(code[pos] | code[pos + 1] << 8);
                        pos += 2;
                        lines.Add($"@{at:D4}: if -> @{fn.EntryPoint + pos + offset:D4}");
                        break;
                    }

                    case 0x04:
                        lines.Add($"@{at:D4}: return");
                        break;

                    case 0x05:
                        lines.Add($"@{at:D4}: eval {ExprDecoder.Decode(code, ref pos)}");
                        break;

                    case 0x06: // switch with explicit case values
                    {
                        var basePos = pos;
                        var count = code[pos++];
                        var def = code[pos] | code[pos + 1] << 8;
                        pos += 2;
                        var cases = new List<String>();
                        for (var i = 0; i < count; i++)
                        {
                            var val = code[pos] | code[pos + 1] << 8;
                            var ofs = code[pos + 2] | code[pos + 3] << 8;
                            pos += 4;
                            cases.Add($"{val} -> @{fn.EntryPoint + basePos + ofs + 3:D4}");
                        }
                        lines.Add($"@{at:D4}: switch {{ {String.Join(", ", cases)}, default -> @{fn.EntryPoint + basePos + def + 3:D4} }}");
                        break;
                    }

                    case 0x0B: // switch over min..min+count−1
                    {
                        var basePos = pos;
                        var count = code[pos];
                        var min = (Int16)(code[pos + 1] | code[pos + 2] << 8);
                        var def = code[pos + 3] | code[pos + 4] << 8;
                        pos += 5;
                        var cases = new List<String>();
                        for (var i = 0; i < count; i++)
                        {
                            var ofs = code[pos] | code[pos + 1] << 8;
                            pos += 2;
                            cases.Add($"{min + i} -> @{fn.EntryPoint + basePos + ofs:D4}");
                        }
                        lines.Add($"@{at:D4}: switch {{ {String.Join(", ", cases)}, default -> @{fn.EntryPoint + basePos + def:D4} }}");
                        break;
                    }

                    case 0x07 or 0x08 or 0x09: // NEW family: two raw bytes, no flag byte
                    {
                        var entry = code[pos++];
                        var uid = code[pos++];
                        lines.Add($"@{at:D4}: spawn entry {entry}, uid {uid}");
                        break;
                    }

                    case 0x10 or 0x12 or 0x14: // REQ / REQSW / REQEW
                    {
                        var flag = code[pos++];
                        var level = ReadArg(code, ref pos, 1, ref flag);
                        var entry = ReadArg(code, ref pos, 1, ref flag);
                        var tag = code[pos++];
                        var name = op switch
                        {
                            0x10 => "RunScriptAsync", 0x12 => "RunScript", _ => "RunScriptSync"
                        };
                        lines.Add($"@{at:D4}: {name}(entry {entry}, tag{tag}, level {level})");
                        break;
                    }

                    case 0x16 or 0x18 or 0x1A: // REPLY / REPLYSW / REPLYEW 
                    {
                        var flag = code[pos++];
                        var level = ReadArg(code, ref pos, 1, ref flag);
                        var tag = code[pos++];
                        var name = op switch
                        {
                            0x16 => "RunScriptObjectAsync", 0x18 => "RunScriptObject", _ => "RunScriptObjectSync"
                        };
                        lines.Add($"@{at:D4}: {name}(tag{tag}, level {level})");
                        break;
                    }

                    case 0x22: // WAIT: flag byte + one 1-byte-or-expression arg
                    {
                        var flag = code[pos++];
                        var frames = (flag & 1) != 0
                            ? ExprDecoder.Decode(code, ref pos) : code[pos++].ToString();
                        lines.Add($"@{at:D4}: wait {frames}");
                        break;
                    }

                    case 0x29: // QUAD/SetRegion: a vertex COUNT, then an X and a Z per vertex.
                    {
                        var flag = code[pos++];
                        var countText = ReadArg(code, ref pos, 1, ref flag);
                        if (!Int32.TryParse(countText, out var count))
                        {
                            // a computed count leaves us unable to size the rest honestly
                            blockedOpcode = op;
                            lines.Add($"@{at:D4}: !! SetRegion with a computed vertex count ({code.Length - pos} bytes undecoded)");
                            return lines;
                        }

                        var vertices = new List<String>();
                        for (var i = 0; i < count; i++)
                            vertices.Add($"({ReadArg(code, ref pos, 2, ref flag)}, {ReadArg(code, ref pos, 2, ref flag)})");
                        lines.Add($"@{at:D4}: SetRegion {String.Join(" ", vertices)}");
                        break;
                    }

                    case 0x4E: // WATCH: a variable-length variable spec, terminated by a zero byte
                    {
                        var start = pos;
                        while (pos < code.Length && code[pos] != 0)
                            pos++;
                        if (pos < code.Length)
                            pos++;
                        lines.Add($"@{at:D4}: Watch({pos - start - 1} bytes)");
                        break;
                    }

                    default:
                    {
                        if (!Known.TryGetValue(op, out var sig)
                            && !EbOpcodeTable.Generated.TryGetValue(op, out sig))
                        {
                            blockedOpcode = op;
                            lines.Add($"@{at:D4}: !! unknown opcode 0x{op:X2} ({code.Length - pos} bytes undecoded)");
                            return lines;
                        }
                        if (sig.ArgWidths.Length == 0)
                        {
                            lines.Add($"@{at:D4}: {sig.Name}()");
                            break;
                        }

                        var flag = code[pos++];
                        var args = new List<String>();
                        foreach (var width in sig.ArgWidths)
                        {
                            if ((flag & 1) != 0)
                                args.Add(ExprDecoder.Decode(code, ref pos));
                            else
                            {
                                var v = 0;
                                for (var b = 0; b < width; b++)
                                    v |= code[pos++] << (8 * b);
                                args.Add(v.ToString());
                            }
                            flag >>= 1;
                        }

                        if (attackNames is not null && op is 0x38 or 0xE5
                                                    && Int32.TryParse(args[0], out var atk)
                                                    && attackNames.TryGetValue(atk, out var atkName))
                            args[0] = $"{args[0]} \"{atkName}\"";

                        if (op == 0x4A && Int32.TryParse(args[0], out var bs)
                                       && BattleSetNames.TryGetValue(bs, out var bsName))
                            args[0] = $"{args[0]} \"{bsName}\"";

                        if (battleText is not null)
                        {
                            var textArg = op switch { 0xD0 => 0, 0x1F => 2, _ => -1 };
                            if (textArg >= 0 && Int32.TryParse(args[textArg], out var mes)
                                             && mes < battleText.Count)
                                args[textArg] = $"{args[textArg]} \"{battleText[mes]}\"";
                        }

                        lines.Add($"@{at:D4}: {sig.Name}({String.Join(", ", args)})");
                        break;
                    }
                }
            }
        }
        catch (IndexOutOfRangeException)
        {
            blockedOpcode = op;
            lines.Add($"!! 0x{op:X2} ran past the end of the function ({code.Length} bytes)");
        }
        
        return lines;
    }

    /// <summary>One handler argument: an expression when its flag bit is set, otherwise a literal
    /// of the given width. The flag shifts after every read (mirror: getv1's `gArgFlag >>= 1`).</summary>
    private static String ReadArg(Byte[] code, ref Int32 pos, Int32 width, ref Byte flag)
    {
        String value;
        if ((flag & 1) != 0)
        {
            value = ExprDecoder.Decode(code, ref pos);
        }
        else
        {
            var v = 0;
            for (var b = 0; b < width; b++)
                v |= code[pos++] << (8 * b);
            value = v.ToString();
        }
        flag >>= 1;
        return value;
    }
    
    /// <summary>Hand-written shapes: opcodes whose case in the reference shares a label with a
    /// D-variant, branches, loops, or reads a raw byte.</summary>
    private static readonly Dictionary<Int32, (String Name, Int32[] ArgWidths)> Known = new()
    {
        [0x1C] = ("TerminateEntry", [1]),             // DELETE - an ordinary handler opcode, flag byte and all
        [0x1D] = ("CreateObject", [2, 2]),            // POS - X then Z; DPOS 0xBF adds a leading object arg
        [0x2F] = ("SetModel", [2, 1]),                // MODEL - model id, then head height
        [0x36] = ("TurnInstant", [1]),                // DIRE - angle; DDIR 0x87 adds a leading object arg
        [0x38] = ("Attack", [1]),                     // BTLCMD - target comes from SV_Target
        [0x3E] = ("SetAnimationSpeed", [1]),          // ASPEED - one arg on BOTH paths; the extra getv1 is a map hack
        [0x40] = ("RunAnimation", [2]),               // ANIM - grouped with DANIM 0xBD, which takes an extra leading object arg
        [0x4A] = ("BattleSet", [1, 2]),               // BTLSET - our name, because BattleSetNames keys off it
        [0x50] = ("WaitTurn", []),                    // WAITTURN - DWAITTURN 0xBC adds a leading object arg
        [0x54] = ("WaitWindow", [1]),                 // WAITMES - one arg on BOTH paths; the second getv1 is a map hack
        [0x56] = ("TimedTurn", [1, 1]),               // TURN - angle, then turn speed; DTURN 0xBB adds a leading object arg
        [0x95] = ("WindowSyncEx", [1, 1, 1, 2]),      // MESA - talker, window, UI flags, text
        [0x9E] = ("ExitField", []),                   // MOVQ
        [0xA1] = ("MoveInstantXZY", [2, 2, 2]),       // POS3 - DPOS3 0xAD adds a leading object arg
        [0xB2] = ("Party", [1, 2]),                   // PARTYMENU - minimum size, then a locked-member mask
        [0xB3] = ("RunSPSCode", [1, 1, 2, 2, 2]),     // SPS - id, code, then three; SPS2 differs only in arg 3's width
        [0xC0] = ("EnableTextureAnimation", [1, 1]),  // TEXPLAY - the reference's own case comment declares "2, { 1, 1 }"
        [0xC1] = ("RunTextureAnimation", [1, 1]),     // TEXPLAY1 - shares TEXPLAY's block
        [0xC5] = ("RunSoundCode", [2, 2]),            // FLDSND0 - one shared block, 0xC5..0xC8 taking 0..3 extra args
        [0xC6] = ("RunSoundCode1", [2, 2, 3]),        // FLDSND1 - note the 3-byte arg
        [0xC7] = ("RunSoundCode2", [2, 2, 3, 1]),     // FLDSND2
        [0xD5] = ("HideAllObjects", []),              // PUSHHIDE - its loop walks live objects, not operands
        [0xD6] = ("ShowAllObjects", []),              // POPSHOW - sibling of PUSHHIDE
        [0xDD] = ("RemoveParty", [1]),                // PARTYDELETE
        [0xF1] = ("SetHP", [1, 2]),                   // SETHP - see SETMP
        [0xF2] = ("SetMP", [1, 2]),                   // SETMP - vanilla reads arg2 only when the character resolves,
                                                      //   with no else; the bytes are always there, so decode both
        [0xFE] = ("SetCharacterData", [1, 1, 1, 1, 1]), // JOIN - five args; the else branch reads args 4-5 too,
                                                      //   because a fixed byte layout cannot depend on runtime state
    };

    private static readonly Dictionary<Int32, String> BattleSetNames = new()
    {
        [32] = "DisableATB", [33] = "EndBattle", [34] = "GameOver",
        [35] = "EnableATB", [36] = "RunCamera", [37] = "ChangeField",
        [38] = "AddGil", [39] = "DragonKill", [40] = "FadeSong",
        [43] = "EffectCounter", [44] = "BonusAP"
    };
}
