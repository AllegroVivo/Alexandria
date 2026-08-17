namespace Alexandria.Formats.Scripts;

/// <summary>Where a Cell's payload points (mirror: EBin.VariableSource).</summary>
public enum VarSource
{
    Global = 0,
    Map = 1,
    Instance = 2,
    Null = 3,
    Object = 4,
    System = 5,
    Member = 6,
    Int26 = 7
}

/// <summary>How wide a variable access is (mirror: EBin.VariableType). Vars are
/// spec-typed PER ACCESS: the same offset can be three Byte counters to one
/// instruction and an Int24 table to the next.</summary>
public enum VarType
{
    SBit = 0,
    Bit = 1,
    Int24 = 2,
    UInt24 = 3,
    SByte = 4,
    Byte = 5,
    Int16 = 6,
    UInt16 = 7
}

/// <summary>
/// Executes .eb bytecode (mirror: EBin.expr / EvaluateValueExpression /
/// SetVariableValue / jumpToCommand). The calc stack holds tagged Cells — a
/// 26-bit payload under source bits 26-28 and type bits 29-31 — emptied at
/// every Eval start exactly as vanilla's is, so an Eval's leftover feeds only
/// the branch that follows it. Variable pools are byte-addressed with
/// per-access widths; Int24 is the workhorse. Unknown opcodes throw with
/// their address.
/// </summary>
public sealed class EbVM(EbEntry entry, IEbHost host, EbState? state = null)
{
    private readonly Byte[] _vars = new Byte[256];      // instance vars (entry.VarCount bytes used)
    private readonly EbState _state = state ?? new EbState();
    private readonly Stack<Cell> _calc = new();

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
                case Op.Nop:
                    break;

                case Op.Jump: // jump: signed offset
                {
                    var offset = (Int16)(code[pos] | code[pos + 1] << 8);
                    pos += 2;
                    pos += offset;
                    break;
                }

                case Op.IfNot: // ifnot: UNSIGNED offset (vanilla quirk)
                {
                    var condition = Evaluate(Pop());
                    var offset = (UInt16)(code[pos] | code[pos + 1] << 8);
                    pos += 2;
                    if (condition == 0)
                        pos += offset;
                    break;
                }

                case Op.If: // if: signed offset
                {
                    var condition = Evaluate(Pop());
                    var offset = (Int16)(code[pos] | code[pos + 1] << 8);
                    pos += 2;
                    if (condition != 0)
                        pos += offset;
                    break;
                }
                
                case Op.Return:
                    return;
                
                case Op.Eval:
                    _calc.Clear(); // mirror: expr() empties the calc stack at every statement start
                    RunExpression(code, ref pos);
                    break;

                case Op.SwitchCase: // switch with explicit case values
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

                case Op.SwitchRange: // switch over min..min+count−1
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

                case Op.Wait: // WAIT: park N frames and yield — a run-to-completion call treats them as elapsed
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

                case Op.BattleCommand: // BTLCMD: the script acts
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

                case Op.BattleDialog: // MESB "BattleDialog": battle text for ~60 frames — does NOT block (returns 0, no wait)
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

            if (op == ExprOp.End)
                return;

            if (op >= 0x80)
            {
                if (op == ExprOp.MemoriaEscape)
                    throw new NotSupportedException("Memoria extended-function escape 0xD3");

                var index = (Int32)code[pos++];
                if ((op & 0x20) != 0)
                    index |= code[pos++] << 8;
                Push(Cell.FromSpec(op, index));
                continue;
            }

            Operator(op, code, ref pos);
        }
    }

    private void Operator(Int32 op, Byte[] code, ref Int32 pos)
    {
        switch (op)
        {
            case ExprOp.PostPlus or ExprOp.PostMinus or ExprOp.PrePlus or ExprOp.PreMinus: // post/pre increment/decrement
            {
                var spec = Peek();
                var current = Evaluate(Pop());
                Push(spec);
                var updated = op is ExprOp.PostPlus or ExprOp.PrePlus ? current + 1 : current - 1;
                WriteThrough(Pop(), updated);
                PushValue(op is ExprOp.PrePlus or ExprOp.PreMinus ? updated : current);
                break;
            }

            case ExprOp.PostPlusA or ExprOp.PostMinusA or ExprOp.PrePlusA or ExprOp.PreMinusA: // ++/-- a stat of every unit in a mask (OperatorAll1)
            {
                var (statId, mask) = PopMemberOperands();
                var result = 0;
                for (var line = 0; line < 8; line++)
                {
                    if ((mask >> line & 1) == 0)
                        continue;
                    _memberTarget = line;
                    var current = host.ReadStat(1 << line, statId);
                    var written = op is ExprOp.PostPlusA or ExprOp.PrePlusA ? current + 1 : current - 1;
                    host.WriteStat(1 << line, statId, written);
                    result = op is ExprOp.PostPlusA or ExprOp.PostMinusA ? current : written;
                }
                PushValue(result);
                break;
            }
            
            case ExprOp.SinglePlus: PushValue(+Evaluate(Pop())); break;
            case ExprOp.SingleMinus: PushValue(-Evaluate(Pop())); break;
            case ExprOp.Not: PushValue(+Evaluate(Pop()) == 0 ? 1 : 0); break;

            case ExprOp.NotE: // B_NOT_E: the mask of units whose stat is zero (OperatorExtract1)
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
            
            case ExprOp.Comp: PushValue(~Evaluate(Pop())); break;

            case >= ExprOp.Mult and <= ExprOp.OrOr
                and not ExprOp.LtE and not ExprOp.GtE and not ExprOp.LeE
                and not ExprOp.GeE and not ExprOp.EqE and not ExprOp.NeE:
            {
                var right = Evaluate(Pop());
                var left = Evaluate(Pop());
                PushValue(op switch
                {
                    ExprOp.Mult => left * right,
                    ExprOp.Div => left / right,
                    ExprOp.Rem => left % right,
                    ExprOp.Plus => left + right,
                    ExprOp.Minus => left - right,
                    ExprOp.ShiftLeft => left << right,
                    ExprOp.ShiftRight => left >> right,
                    ExprOp.Lt => left < right ? 1 : 0,
                    ExprOp.Gt => left > right ? 1 : 0,
                    ExprOp.Le => left <= right ? 1 : 0,
                    ExprOp.Ge => left >= right ? 1 : 0,
                    ExprOp.Eq => left == right ? 1 : 0,
                    ExprOp.Ne => left != right ? 1 : 0,
                    ExprOp.And => left & right,
                    ExprOp.Xor => left ^ right,
                    ExprOp.Or => left | right,
                    ExprOp.AndAnd => left != 0 && right != 0 ? 1 : 0,
                    ExprOp.OrOr => left != 0 || right != 0 ? 1 : 0,
                    _ => throw new NotSupportedException($"operator {op}")
                });
                break;
            }

            case ExprOp.LtE or ExprOp.GtE or ExprOp.LeE or ExprOp.GeE or ExprOp.EqE or ExprOp.NeE
                or ExprOp.AndE or ExprOp.NandE or ExprOp.XorE or ExprOp.OrE:
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
                        ExprOp.LtE => v < cmp,
                        ExprOp.GtE => v > cmp,
                        ExprOp.LeE => v <= cmp,
                        ExprOp.GeE => v >= cmp,
                        ExprOp.EqE => v == cmp,
                        ExprOp.NeE => v != cmp,
                        ExprOp.AndE => (v & cmp) != 0,
                        ExprOp.NandE => (v & cmp) == 0,
                        ExprOp.XorE => (v ^ cmp) != 0,
                        _ => (v | cmp) != 0
                    };
                    if (hit)
                        result |= 1 << line;
                }
                PushValue(result);
                break;
            }
            
            case ExprOp.Member: // B_MEMBER: push a stat reference
                Push(Cell.MemberRef(code[pos++]));
                break;
            
            case ExprOp.Count: PushValue(Int32.PopCount(Evaluate(Pop())) is var n ? n : n); break;

            case ExprOp.Pick: // B_PICK: the stat of the LOWEST unit in a mask (mirror: OperatorPick)
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
            
            case ExprOp.Let: // B_LET: value stays on the stack
            {
                var value = Evaluate(Pop());
                WriteThrough(Pop(), value);
                PushValue(value);
                break;
            }

            case ExprOp.LetA or (>= ExprOp.MultLetA and <= ExprOp.ShiftRightLetA)
                or ExprOp.AndLetA or ExprOp.XorLetA or ExprOp.OrLetA: // _A: assign to every unit (OperatorAll)
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
                        ExprOp.LetA => value,
                        ExprOp.MultLetA => current * value,
                        ExprOp.DivLetA => value == 0 ? current : current / value, // /0 and %0 write the read back
                        ExprOp.RemLetA => value == 0 ? current : current % value,
                        ExprOp.PlusLetA => current + value,
                        ExprOp.MinusLetA => current - value,
                        ExprOp.ShiftLeftLetA => current << value,
                        ExprOp.ShiftRightLetA => current >> value,
                        ExprOp.AndLetA => current & value,
                        ExprOp.XorLetA => current ^ value,
                        _ => current | value
                    };
                    host.WriteStat(1 << line, statId, result);
                }
                PushValue(result);
                break;
            }

            case >= ExprOp.MultLet and <= ExprOp.ShiftRightLet: // arithmetic compound assignment
            case ExprOp.AndLet or ExprOp.XorLet or ExprOp.OrLet: // bitwise compound assignment
            {
                var value = Evaluate(Pop());
                var spec = Peek();
                var current = Evaluate(Pop());
                Push(spec);
                var result = op switch
                {
                    ExprOp.MultLet => current * value,
                    ExprOp.DivLet => current / value,
                    ExprOp.RemLet => current % value,
                    ExprOp.PlusLet => current + value,
                    ExprOp.MinusLet => current - value,
                    ExprOp.ShiftLeftLet => current << value,
                    ExprOp.ShiftRightLet => current >> value,
                    ExprOp.AndLet => current & value,
                    ExprOp.XorLet => current ^ value,
                    _ => current | value
                };
                WriteThrough(Pop(), result);
                PushValue(result);
                break;
            }

            case ExprOp.LetE or ExprOp.AndLetE or ExprOp.XorLetE or ExprOp.OrLetE: // _E extract-assign: dest gets a truthiness mask (OperatorExtractLet)
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
                    ExprOp.AndLetE => Evaluate(dest) & result,
                    ExprOp.XorLetE => Evaluate(dest) ^ result,
                    ExprOp.OrLetE => Evaluate(dest) | result,
                    _ => result
                };
                WriteThrough(dest, result);
                PushValue(result);
                break;
            }
            
            case ExprOp.Cast8: PushValue((SByte)Evaluate(Pop())); break;
            case ExprOp.Cast8U: PushValue((Byte)Evaluate(Pop())); break;
            case ExprOp.Cast16: PushValue((Int16)Evaluate(Pop())); break;
            case ExprOp.Cast16U: PushValue((UInt16)Evaluate(Pop())); break;

            case ExprOp.Lmax or ExprOp.Lmin: // B_LMAX/B_LMIN: 1 << line of the best unit (mirror: OperatorExtract1)
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
                    if (op == ExprOp.Lmax ? best < v : best > v)
                    {
                        best = v;
                        bestLine = line;
                    }
                }
                PushValue(1 << bestLine);
                break;
            }
            
            case ExprOp.Select: PushValue(host.RandomBit(Evaluate(Pop()))); break;
            case ExprOp.SysList: Push(Cell.SysSlot(code[pos++])); break;
            case ExprOp.Sysvar: PushValue(host.Sysvar(code[pos++])); break; // evaluated at decode time
            
            case ExprOp.Const: // B_CONST: i16
                PushValue((Int16)(code[pos] | code[pos + 1] << 8));
                pos += 2;
                break;

            case ExprOp.Const4: // B_CONST4: 26-bit signed
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
    private void Push(Cell cell) => _calc.Push(cell);
    private Cell Pop() => _calc.Pop();
    private Cell Peek() => _calc.Peek();
    private void PushValue(Int32 value) => _calc.Push(Cell.Value(value));

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
        if (member.Source != VarSource.Member)
            throw new NotSupportedException($"member-set operand source {member.Source} — teach me");
        var mask = Evaluate(Pop()) & 0xFF;
        return (member.StatID, mask);
    }

    private static Int32 LowestBit(Int32 mask)
    {
        for (var line = 0; line < 8; line++)
            if ((mask >> line & 1) != 0)
                return line;
        return -1;
    }

    private Int32 Evaluate(Cell cell) => cell.Source switch
    {
        VarSource.Global => Read(_state.Global, cell),
        VarSource.Map => Read(_state.Map, cell),
        VarSource.Instance => Read(_vars, cell),
        VarSource.System => host.SysList(cell.Slot),
        VarSource.Member => host.ReadStat(1 << MemberLine(), cell.StatID),
        VarSource.Int26 => cell.SignedValue,
        var source => throw new NotSupportedException($"variable source {source} — teach me")
    };

    private void WriteThrough(Cell cell, Int32 value)
    {
        switch (cell.Source)
        {
            case VarSource.Member: // bare stat write: whichever unit the last loop touched
                host.WriteStat(1 << MemberLine(), cell.StatID, value);
                return;
            case VarSource.System:
                host.SetSysList(cell.Slot, value);
                return;
        }

        var pool = cell.Source switch
        {
            VarSource.Global => _state.Global,
            VarSource.Map => _state.Map,
            VarSource.Instance => _vars,
            var source => throw new NotSupportedException($"write to source {source}")
        };

        var ofs = cell.Offset;
        switch (cell.Type)
        {
            case VarType.SBit or VarType.Bit:
                if (value == 0) pool[ofs >> 3] &= (Byte)~(1 << (ofs & 7));
                else pool[ofs >> 3] |= (Byte)(1 << (ofs & 7));
                break;
            case VarType.Int24 or VarType.UInt24: // 3 bytes LE
                pool[ofs] = (Byte)value;
                pool[ofs + 1] = (Byte)(value >> 8);
                pool[ofs + 2] = (Byte)(value >> 16);
                break;
            case VarType.SByte or VarType.Byte:
                pool[ofs] = (Byte)value;
                break;
            default: // Int16/UInt16 — 2 bytes LE
                pool[ofs] = (Byte)value;
                pool[ofs + 1] = (Byte)(value >> 8);
                break;
        }
    }

    private static Int32 Read(Byte[] pool, Cell cell)
    {
        var ofs = cell.Offset;
        return cell.Type switch
        {
            VarType.SBit or VarType.Bit => (pool[ofs >> 3] >> (ofs & 7)) & 1,
            VarType.Int24 => pool[ofs] | pool[ofs + 1] << 8 | (SByte)pool[ofs + 2] << 16,
            VarType.UInt24 => pool[ofs] | pool[ofs + 1] << 8 | pool[ofs + 2] << 16,
            VarType.SByte => (SByte)pool[ofs],
            VarType.Byte => pool[ofs],
            VarType.Int16 => (Int16)(pool[ofs] | pool[ofs + 1] << 8),
            _ => pool[ofs] | pool[ofs + 1] << 8
        };
    }
    
    /// <summary>Statement opcodes, named as the disassembler prints them
    /// (mirror: EBin.event_code_binary; vanilla mnemonics in comments).</summary>
    private static class Op
    {
        public const Int32 Nop = 0x00;
        public const Int32 Jump = 0x01;          // bra — SIGNED i16 offset
        public const Int32 IfNot = 0x02;         // beq — UNSIGNED u16 offset (vanilla quirk)
        public const Int32 If = 0x03;            // bne — SIGNED i16 offset
        public const Int32 Return = 0x04;
        public const Int32 Eval = 0x05;          // EXPR
        public const Int32 SwitchCase = 0x06;    // explicit case values
        public const Int32 SwitchRange = 0x0B;   // min..min+count−1
        public const Int32 Wait = 0x22;          // WAIT — park N frames
        public const Int32 BattleCommand = 0x38; // BTLCMD — the script acts
        public const Int32 BattleDialog = 0xD0;  // MESB — HUD text, never blocks
    }

    /// <summary>Expression operators (mirror: EBin.op_binary, B_ prefix dropped).
    /// The primed families loop the units under a mask: _E extracts a result
    /// mask, _A assigns through every unit — EventEngine.OperatorExtract/All.</summary>
    private static class ExprOp
    {
        public const Int32 End = 0x7F;
        
        public const Int32 PostPlus = 4;
        public const Int32 PostMinus = 5;
        public const Int32 PrePlus = 6;
        public const Int32 PreMinus = 7;
        public const Int32 PostPlusA = 8;
        public const Int32 PostMinusA = 9;
        public const Int32 PrePlusA = 10;
        public const Int32 PreMinusA = 11;
        public const Int32 SinglePlus = 12;
        public const Int32 SingleMinus = 13;
        public const Int32 Not = 14;
        public const Int32 NotE = 15;
        public const Int32 Comp = 16;
        public const Int32 Mult = 17;
        public const Int32 Div = 18;
        public const Int32 Rem = 19;
        public const Int32 Plus = 20;
        public const Int32 Minus = 21;
        public const Int32 ShiftLeft = 22;
        public const Int32 ShiftRight = 23;
        public const Int32 Lt = 24;
        public const Int32 Gt = 25;
        public const Int32 Le = 26;
        public const Int32 Ge = 27;
        public const Int32 LtE = 28;
        public const Int32 GtE = 29;
        public const Int32 LeE = 30;
        public const Int32 GeE = 31;
        public const Int32 Eq = 32;
        public const Int32 Ne = 33;
        public const Int32 EqE = 34;
        public const Int32 NeE = 35;
        public const Int32 And = 36;
        public const Int32 Xor = 37;
        public const Int32 Or = 38;
        public const Int32 AndAnd = 39;
        public const Int32 OrOr = 40;
        public const Int32 Member = 41;
        public const Int32 Count = 42;
        public const Int32 Pick = 43;
        public const Int32 Let = 44;
        public const Int32 LetA = 45;
        public const Int32 LetE = 46;
        public const Int32 MultLet = 47;
        public const Int32 DivLet = 48;
        public const Int32 RemLet = 49;
        public const Int32 PlusLet = 50;
        public const Int32 MinusLet = 51;
        public const Int32 ShiftLeftLet = 52;
        public const Int32 ShiftRightLet = 53;
        public const Int32 MultLetA = 54;
        public const Int32 DivLetA = 55;
        public const Int32 RemLetA = 56;
        public const Int32 PlusLetA = 57;
        public const Int32 MinusLetA = 58;
        public const Int32 ShiftLeftLetA = 59;
        public const Int32 ShiftRightLetA = 60;
        public const Int32 AndLet = 61;
        public const Int32 XorLet = 62;
        public const Int32 OrLet = 63;
        public const Int32 AndLetA = 64;
        public const Int32 XorLetA = 65;
        public const Int32 OrLetA = 66;
        public const Int32 AndLetE = 67;
        public const Int32 XorLetE = 68;
        public const Int32 OrLetE = 69;
        public const Int32 Cast8 = 70;
        public const Int32 Cast8U = 71;
        public const Int32 Cast16 = 72;
        public const Int32 Cast16U = 73;
        public const Int32 Lmax = 75;
        public const Int32 Lmin = 76;
        public const Int32 Select = 77;        // RandomBit
        public const Int32 AndE = 84;
        public const Int32 NandE = 85;
        public const Int32 XorE = 86;
        public const Int32 OrE = 87;
        public const Int32 SysList = 121;
        public const Int32 Sysvar = 122;
        public const Int32 Const = 125;        // i16
        public const Int32 Const4 = 126;       // 26-bit signed
        public const Int32 MemoriaEscape = 0xD3;
    }

    /// <summary>One calc-stack cell, EBin's exact layout: a 26-bit payload under
    /// source bits 26-28 and type bits 29-31. Computed results are Int26 cells;
    /// everything else is a lazy reference, evaluated only when popped.</summary>
    private readonly record struct Cell(Int32 Bits)
    {
        public VarSource Source => (VarSource)(Bits >> 26 & 7);
        public VarType Type => (VarType)(Bits >> 29 & 7);

        public Int32 SignedValue => Bits << 6 >> 6; // Int26 pyload, sign-extended
        public Int32 Offset => Bits & 0xFFFF; // pool byte offset
        public Int32 StatID => Bits & 0xFF; // Member payload (write side uses t0 & 0xFF)
        public Int32 Slot => Bits&0xFF; // SysList slot

        public static Cell Value(Int32 v) => new(v & 0x3FFFFFF | (Int32)VarSource.Int26 << 26);
        public static Cell MemberRef(Int32 statId) => new((Int32)VarSource.Member << 26 | statId);
        public static Cell SysSlot(Int32 slot) => new((Int32)VarSource.System << 26 | slot);

        /// <summary>A var-spec byte ≥0x80: bits 0-1 source, 2-4 type, 5 = 16-bit index.</summary>
        public static Cell FromSpec(Int32 spec, Int32 index)
            => new((spec & 3) << 26 | (spec & 0x1C) << 27 | index);
        
    }
}
