namespace Alexandria.Formats.Scripts;

/// <summary>
/// Executes .eb bytecode (mirror: EBin.expr / EvaluateValueExpression /
/// SetVariableValue / jumpToCommand). The calc stack holds TAGGED cells -
/// source bits 26-28 over a 26-bit payload - and persists across instructions,
/// so an EXPR's leftover condition feeds the following branch, as in the engine.
/// Variable pools are byte-addressed with per-type widths; Int24 is the workhorse.
/// Unsupported opcodes throw with their address.
/// </summary>
public sealed class EbVM(EbEntry entry, IEbHost host)
{
    private readonly Byte[] _vars = new Byte[256];      // instance vars (entry.VarCount bytes used)
    private readonly Byte[] _map = new Byte[256];
    private readonly Byte[] _global = new Byte[2048];
    private readonly Stack<Int32> _calc = new();

    private const Int32 Int26Tag = 7 << 26;

    private Int32 _memberTarget = -1;

    /// <summary>Read a Byte instance var - counters and flags use single-byte cells.</summary>
    public Int32 Var8(Int32 offset) => _vars[offset];
     
    /// <summary>Read an Int24 instance var - the test window into VM state.</summary>
    public Int32 Var24(Int32 offset)
        => _vars[offset] | _vars[offset + 1] << 8 | (SByte)_vars[offset + 2] << 16;

    public void RunFunction(UInt16 tag)
    {
        var fn = entry.Functions.Single(f => f.Tag == tag);
        var code = fn.Code;
        var pos = 0;

        while (pos < code.Length)
        {
            var op = 0;
            while (pos < code.Length && code[pos] == 0xFF)
            {
                op += 0x100;
                pos++;
            }
            op += code[pos++];

            switch (op)
            {
                case 0x00:
                    break;

                case 0x01: // jump: signed offset
                {
                    var offset = (Int16)(code[pos] | code[pos + 1] << 8);
                    pos += 2;
                    pos += offset;
                    break;
                }

                case 0x02: // ifnot: UNSIGNED offset (vanilla quirk)
                {
                    var condition = Evaluate(Pop());
                    var offset = (UInt16)(code[pos] | code[pos + 1] << 8);
                    pos += 2;
                    if (condition == 0)
                        pos += offset;
                    break;
                }

                case 0x03: // if: signed offset
                {
                    var condition = Evaluate(Pop());
                    var offset = (Int16)(code[pos] | code[pos + 1] << 8);
                    pos += 2;
                    if (condition != 0)
                        pos += offset;
                    break;
                }
                
                case 0x04:
                    return;
                
                case 0x05:
                    _calc.Clear(); // mirror: expr() empties the calc stack at every statement start
                    RunExpression(code, ref pos);
                    break;

                case 0x06: // switch with explicit case values
                {
                    var basePos = pos;
                    var selector = Evaluate(Pop()) & 0xFFFF;
                    var count = code[pos++];
                    var def = code[pos] | code[pos + 1] << 8;
                    pos += 2;
                    var target = def;
                    for (var i = 0; i < count; i++)
                    {
                        var value = code[pos] | code[pos + 1] << 8;
                        var offset = code[pos + 2] | code[pos + 3] << 8;
                        pos += 4;
                        if (value == selector)
                            target = offset;
                    }
                    pos = basePos + target + 3;
                    break;
                }

                case 0x0B: // switch over min..min+count−1
                {
                    var basePos = pos;
                    var selector = Evaluate(Pop());
                    var count = code[pos];
                    var min = (Int16)(code[pos + 1] | code[pos + 2] << 8);
                    var def = code[pos + 3] | code[pos + 4] << 8;
                    pos += 5;
                    var target = def;
                    for (var i = 0; i < count; i++)
                    {
                        var offset = code[pos] | code[pos + 1] << 8;
                        pos += 2;
                        if (selector == min + i)
                            target = offset;
                    }
                    pos = basePos + target;
                    break;
                }

                case 0x22: // WAIT: park N frames and yield — a run-to-completion call treats them as elapsed
                {
                    var flag = code[pos++];
                    if ((flag & 1) != 0)
                    {
                        RunExpression(code, ref pos);
                        Pop();
                    }
                    else
                        pos++;
                    break;
                }

                case 0x38: // BTLCMD: the script acts
                {
                    var flag = code[pos++];
                    Int32 attackId;
                    if ((flag & 1) != 0)
                    {
                        RunExpression(code, ref pos);
                        attackId = Evaluate(Pop());
                    }
                    else
                        attackId = code[pos++];
                    host.Attack(attackId);
                    break;
                }

                case 0xD0: // MESB "BattleDialog": battle text for ~60 frames — does NOT block (returns 0, no wait)
                {
                    var flag = code[pos++];
                    Int32 textId;
                    if ((flag & 1) != 0)
                    {
                        RunExpression(code, ref pos);
                        textId = Evaluate(Pop());
                    }
                    else
                    {
                        textId = (Int16)(code[pos] | code[pos + 1] << 8);
                        pos += 2;
                    }
                    host.BattleDialog(textId);
                    break;
                }
                    
                default:
                    throw new NotSupportedException(
                        $"opcode 0x{op:X2} at @{fn.EntryPoint + pos - 1} — teach me");
            }
        }
    }
    
    // ---- The expression machine (mirror: EBin.expr) ----
    private void RunExpression(Byte[] code, ref Int32 pos)
    {
        while (true)
        {
            var op = code[pos++];

            if (op == 0x7F)
                return;

            if (op >= 0x80)
            {
                if (op == 0xD3)
                    throw new NotSupportedException("Memoria extended-function escape 0xD3");

                var index = (Int32)code[pos++];
                if ((op & 0x20) != 0)
                    index |= code[pos++] << 8;
                Push((op & 3) << 26 | (op & 0x1C) << 27 | index);
                continue;
            }

            Operator(op, code, ref pos);
        }
    }

    private void Operator(Int32 op, Byte[] code, ref Int32 pos)
    {
        switch (op)
        {
            case 4 or 5 or 6 or 7: // post/pre increment/decrement
            {
                var spec = Peek();
                var current = Evaluate(Pop());
                Push(spec);
                var updated = op is 4 or 6 ? current + 1 : current - 1;
                WriteThrough(Pop(), updated);
                PushValue(op is 6 or 7 ? updated : current);
                break;
            }

            case 8 or 9 or 10 or 11: // ++/-- a stat of every unit in a mask (OperatorAll1)
            {
                var (statId, mask) = PopMemberOperands();
                var result = 0;
                for (var line = 0; line < 8; line++)
                {
                    if ((mask >> line & 1) == 0)
                        continue;
                    _memberTarget = line;
                    var current = host.ReadStat(1 << line, statId);
                    var written = op is 8 or 10 ? current + 1 : current - 1;
                    host.WriteStat(1 << line, statId, written);
                    result = op is 8 or 9 ? current : written;
                }
                PushValue(result);
                break;
            }
            
            case 12: PushValue(+Evaluate(Pop())); break;
            case 13: PushValue(-Evaluate(Pop())); break;
            case 14: PushValue(+Evaluate(Pop()) == 0 ? 1 : 0); break;

            case 15: // B_NOT_E: the mask of units whose stat is zero (OperatorExtract1)
            {
                var (statId, mask) = PopMemberOperands();
                var result = 0;
                for (var line = 0; line < 8; line++)
                {
                    if ((mask >> line & 1) == 0)
                        continue;
                    _memberTarget = line;
                    if (host.ReadStat(1 << line, statId) == 0)
                        result |= 1 << line;
                }
                PushValue(result);
                break;
            }
            
            case 16: PushValue(~Evaluate(Pop())); break;

            case >= 17 and <= 40 and not 28 and not 29 and not 30 and not 31 and not 34 and not 35: // plain binary ops (no _E variants)
            {
                var right = Evaluate(Pop());
                var left = Evaluate(Pop());
                PushValue(op switch
                {
                    17 => left * right, 18 => left / right, 19 => left % right,
                    20 => left + right, 21 => left - right,
                    22 => left << right, 23 => left >> right,
                    24 => left < right ? 1 : 0, 25 => left > right ? 1 : 0,
                    26 => left <= right ? 1 : 0, 27 => left >= right ? 1 : 0,
                    32 => left == right ? 1 : 0, 33 => left != right ? 1 : 0,
                    36 => left & right, 37 => left ^ right, 38 => left | right,
                    39 => left != 0 && right != 0 ? 1 : 0,
                    40 => left != 0 || right != 0 ? 1 : 0,
                    _ => throw new NotSupportedException($"operator {op}")
                });
                break;
            }

            case 28 or 29 or 30 or 31 or 34 or 35 or 84 or 85 or 86 or 87: // _E: compare across a mask
            {
                var cmp = Evaluate(Pop());
                var (statId, mask) = PopMemberOperands();
                var result = 0;
                for (var line = 0; line < 8; line++)
                {
                    if ((mask >> line & 1) == 0)
                        continue;
                    _memberTarget = line;
                    var v = host.ReadStat(1 << line, statId);
                    var hit = op switch
                    {
                        28 => v < cmp, 29 => v > cmp, 30 => v <= cmp, 31 => v >= cmp,
                        34 => v == cmp, 35 => v != cmp,
                        84 => (v & cmp) != 0, 85 => (v & cmp) == 0,
                        86 => (v ^ cmp) != 0, _ => (v | cmp) != 0
                    };
                    if (hit)
                        result |= 1 << line;
                }
                PushValue(result);
                break;
            }
            
            case 41: // B_MEMBER: push a stat reference
                Push(6 << 26 | code[pos++]);
                break;
            
            case 42: PushValue(Int32.PopCount(Evaluate(Pop())) is var n ? n : n); break;

            case 43: // B_PICK: the stat of the LOWEST unit in a mask (mirror: OperatorPick)
            {
                var (statId, mask) = PopMemberOperands();
                var line = LowestBit(mask);
                if (line < 0)
                {
                    PushValue(0);
                    break;
                }
                _memberTarget = line;
                PushValue(host.ReadStat(1 << line, statId));
                break;
            }
            
            case 44: // B_LET: value stays on the stack
            {
                var value = Evaluate(Pop());
                WriteThrough(Pop(), value);
                PushValue(value);
                break;
            }

            case 45 or (>= 54 and <= 60) or 64 or 65 or 66: // _A: assign to every unit (OperatorAll)
            {
                var value = Evaluate(Pop());
                var (statId, mask) = PopMemberOperands();
                var result = 0;
                for (var line = 0; line < 8; line++)
                {
                    if ((mask >> line & 1) == 0)
                        continue;
                    _memberTarget = line;
                    var current = op == 45 ? 0 : host.ReadStat(1 << line, statId);
                    result = op switch
                    {
                        45 => value,
                        54 => current * value,
                        55 => value == 0 ? current : current / value,
                        56 => value == 0 ? current : current % value,
                        57 => current + value, 58 => current - value,
                        59 => current << value, 60 => current >> value,
                        64 => current & value, 65 => current ^ value, _ => current | value
                    };
                    host.WriteStat(1 << line, statId, result);
                }
                PushValue(result);
                break;
            }

            case >= 47 and <= 53: // arithmetic compound assignment
            case >= 61 and <= 63: // bitwise compound assignment
            {
                var value = Evaluate(Pop());
                var spec = Peek();
                var current = Evaluate(Pop());
                Push(spec);
                var result = op switch
                {
                    47 => current * value, 48 => current / value, 49 => current % value,
                    50 => current + value, 51 => current - value,
                    52 => current << value, 53 => current >> value,
                    61 => current & value, 62 => current ^ value, _ => current | value
                };
                WriteThrough(Pop(), result);
                PushValue(result);
                break;
            }

            case 46 or 67 or 68 or 69: // _E extract-assign: dest gets a truthiness mask (OperatorExtractLet)
            {
                var (statId, mask) = PopMemberOperands();
                var result = 0;
                for (var line = 0; line < 8; line++)
                {
                    if ((mask >> line & 1) == 0)
                        continue;
                    _memberTarget = line;
                    if (host.ReadStat(1 << line, statId) != 0)
                        result |= 1 << line;
                }
                var dest = Pop();
                result = op switch
                {
                    67 => Evaluate(dest) & result,
                    68 => Evaluate(dest) ^ result,
                    69 => Evaluate(dest) | result,
                    _ => result
                };
                WriteThrough(dest, result);
                PushValue(result);
                break;
            }
            
            case 70: PushValue((SByte)Evaluate(Pop())); break;
            case 71: PushValue((Byte)Evaluate(Pop())); break;
            case 72: PushValue((Int16)Evaluate(Pop())); break;
            case 73: PushValue((UInt16)Evaluate(Pop())); break;

            case 75 or 76: // B_LMAX/B_LMIN: 1 << line of the best unit (mirror: OperatorExtract1)
            {
                var (statId, mask) = PopMemberOperands();
                var best = op == 75 ? (Int32)Int16.MinValue : Int16.MaxValue;
                var bestLine = 0;
                for (var line = 0; line < 8; line++)
                {
                    if ((mask >> line & 1) == 0)
                        continue;
                    _memberTarget = line;
                    var v = host.ReadStat(1 << line, statId);
                    if (op == 75 ? best < v : best > v)
                    {
                        best = v;
                        bestLine = line;
                    }
                }
                PushValue(1 << bestLine);
                break;
            }
            
            case 77: PushValue(host.RandomBit(Evaluate(Pop()))); break;
            case 121: Push(5 << 26 | code[pos++]); break; // System-tagged cell
            case 122: PushValue(host.Sysvar(code[pos++])); break; // evaluated at decode time
            
            case 125: // B_CONST: i16
                PushValue((Int16)(code[pos] | code[pos + 1] << 8));
                pos += 2;
                break;

            case 126: // B_CONST4: 26-bit signed
            {
                var v = code[pos] | code[pos + 1] << 8 | code[pos + 2] << 16 | code[pos + 3] << 24;
                pos += 4;
                PushValue(((v & 0x3FFFFFF) << 6) >> 6);
                break;
            }
            
            default:
                throw new NotSupportedException($"expression operator {op} — teach me");
        }
    }
    
    // ---- Tagged cells (mirror: EvaluateValueExpression / SetVariableValue) ----
    private void Push(Int32 cell) => _calc.Push(cell);
    private Int32 Pop() => _calc.Pop();
    private Int32 Peek() => _calc.Peek();
    private void PushValue(Int32 value) => _calc.Push((value & 0x3FFFFFF) | Int26Tag);

    private Int32 MemberLine() => _memberTarget >= 0
        ? _memberTarget
        : throw new InvalidOperationException("bare stat cell before any PICK/_E/_A set the member target");

    /// <summary>The operand pair under every member-set operator: the stat cell on
    /// top, the unit mask beneath (a SysList cell reads live, anything else was
    /// computed). Vanilla peeks the mask in place and re-pops the stat cell once
    /// per unit; popping both once and reusing them is the same dance flattened.</summary>
    private (Int32 StatID, Int32 Mask) PopMemberOperands()
    {
        var member = Pop();
        if ((member >> 26 & 7) != 6)
            throw new NotSupportedException($"member-set operand source {(member >> 26) & 7} — teach me");
        var mask = Evaluate(Pop()) & 0xFF;
        return (member & 0xFF, mask);
    }

    private static Int32 LowestBit(Int32 mask)
    {
        for (var line = 0; line < 8; line++)
            if ((mask >> line & 1) != 0)
                return line;
        return -1;
    }

    private Int32 Evaluate(Int32 cell) => ((cell >> 26) & 7) switch
    {
        0 => Read(_global, cell),
        1 => Read(_map, cell),
        2 => Read(_vars, cell),
        5 => host.SysList(cell & 0xFF),
        6 => host.ReadStat(1 << MemberLine(), cell & 0xFF),
        7 => (cell << 6) >> 6, // Int26, sign-extended
        var source => throw new NotSupportedException($"variable source {source} — teach me")
    };

    private void WriteThrough(Int32 cell, Int32 value)
    {
        if (((cell >> 26) & 7) == 6) // bare stat write: whichever unit the last loop touched
        {
            host.WriteStat(1 << MemberLine(), cell & 0xFF, value);
            return;
        }
        
        Byte[]? pool = ((cell >> 26) & 7) switch
        {
            0 => _global, 1 => _map, 2 => _vars,
            5 => null,
            var source => throw new NotSupportedException($"write to source {source}")
        };
        if (pool is null)
        {
            host.SetSysList(cell & 0xFF, value);
            return;
        }

        var type = (cell >> 29) & 7;
        var ofs = cell & 0xFFFF;
        switch (type)
        {
            case 0 or 1: // SBit/Bit
                if (value == 0) pool[ofs >> 3] &= (Byte)~(1 << (ofs & 7));
                else pool[ofs >> 3] |= (Byte)(1 << (ofs & 7));
                break;
            case 2 or 3: // Int24/UInt24 — 3 bytes LE
                pool[ofs] = (Byte)value;
                pool[ofs + 1] = (Byte)(value >> 8);
                pool[ofs + 2] = (Byte)(value >> 16);
                break;
            case 4 or 5:
                pool[ofs] = (Byte)value;
                break;
            default: // Int16/UInt16
                pool[ofs] = (Byte)value;
                pool[ofs + 1] = (Byte)(value >> 8);
                break;
        }
    }

    private static Int32 Read(Byte[] pool, Int32 cell)
    {
        var type = (cell >> 29) & 7;
        var ofs = cell & 0xFFFF;
        return type switch
        {
            0 or 1 => (pool[ofs >> 3] >> (ofs & 7)) & 1,
            2 => pool[ofs] | pool[ofs + 1] << 8 | (SByte)pool[ofs + 2] << 16,
            3 => pool[ofs] | pool[ofs + 1] << 8 | pool[ofs + 2] << 16,
            4 => (SByte)pool[ofs],
            5 => pool[ofs],
            6 => (Int16)(pool[ofs] | pool[ofs + 1] << 8),
            _ => pool[ofs] | pool[ofs + 1] << 8
        };
    }
}
