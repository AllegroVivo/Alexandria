namespace Alexandria.Formats.Scripts;

/// <summary>
/// Linear disassembler for one .eb function (mirror: EBin.next/jumpToCommand +
/// EventEngine.DoEventCode). Instruction lengths are self-describing: native low
/// opcodes have bespoke layouts; handler opcodes read one flag byte then one
/// field per argument — an inline immediate (width fixed per opcode) when the
/// flag bit is 0, an expression stream when it is 1. An opcode we cannot size
/// halts the function honestly; the census aggregates blockers so we know which
/// opcodes to teach it next.
/// </summary>
public static class EbDisassembler
{
    private static readonly Dictionary<Int32, (String Name, Int32[] ArgWidths)> Known = new()
    {
        [0x38] = ("Attack", [1]), // BTLCMD — target comes from SV_Target
        [0x4A] = ("BattleSet", [1, 2]), // BTLSET
        [0x1B] = ("ContinueBattleMusic", [1]),
        [0x4C] = ("AttachObject", [1, 1, 1]),
        [0x66] = ("SetTextVariable", [1, 2]),
        [0xD0] = ("BattleDialog", [2]),
        [0xE5] = ("AttackSpecial", [1]),
        [0xEB] = ("CloseAllWindows", []),
        [0xEC] = ("FadeFilter", [1, 1, 1, 1, 1, 1]),
    };

    public static List<String> Disassemble(EbFunction fn) => Disassemble(fn, out _);

    public static List<String> Disassemble(EbFunction fn, out Int32? blockedOpcode)
    {
        blockedOpcode = null;
        var lines = new List<String>();
        var code = fn.Code;
        var pos = 0;

        while (pos < code.Length)
        {
            var at = fn.EntryPoint + pos; // matches engine's ip values
            var op = 0;
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

                case 0x22: // WAIT: flag byte + one 1-byte-or-expression arg
                {
                    var flag = code[pos++];
                    var frames = (flag & 1) != 0
                        ? ExprDecoder.Decode(code, ref pos) : code[pos++].ToString();
                    lines.Add($"@{at:D4}: wait {frames}");
                    break;
                }

                default:
                {
                    if (!Known.TryGetValue(op, out var sig))
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
                    lines.Add($"@{at:D4}: {sig.Name}({String.Join(", ", args)})");
                    break;
                }
            }
        }
        
        return lines;
    }
}
