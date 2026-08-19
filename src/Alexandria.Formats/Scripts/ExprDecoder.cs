namespace Alexandria.Formats.Scripts;

/// <summary>
/// Renders one .eb expression stream as text (mirror: EBin.expr/expr_varSpec/
/// expr_jumpToSubCommand). Expressions are RPN: bytes >= 0x80 push variable
/// references, bytes less-than 0x80 are operators; 0x7F ends the stream. We run the
/// engine's stack machine with strings, so postfix bytes print back as infix.
/// Operators marked ' are the _E "member set" variants; assignment pushes its
/// value back (mirror: B_LET), so chained stores render naturally.
/// </summary>
public static class ExprDecoder
{
    public static readonly Dictionary<Byte, (String Symbol, Int32 Arity)> Operators = new()
    {
        [4] = ("++", 1), [5] = ("--", 1), [6] = ("++", 1), [7] = ("--", 1),
        [12] = ("+", 1), [13] = ("-", 1), [14] = ("!", 1), [15] = ("!'", 1), [16] = ("~", 1),
        [17] = ("*", 2), [18] = ("/", 2), [19] = ("%", 2), [20] = ("+", 2), [21] = ("-", 2),
        [22] = ("<<", 2), [23] = (">>", 2),
        [24] = ("<", 2), [25] = (">", 2), [26] = ("<=", 2), [27] = (">=", 2),
        [28] = ("<'", 2), [29] = (">'", 2), [30] = ("<='", 2), [31] = (">='", 2),
        [32] = ("==", 2), [33] = ("!=", 2), [34] = ("=='", 2), [35] = ("!='", 2),
        [36] = ("&", 2), [37] = ("^", 2), [38] = ("|", 2), [39] = ("&&", 2), [40] = ("||", 2),
        [70] = ("(i8)", 1), [71] = ("(u8)", 1), [72] = ("(i16)", 1), [73] = ("(u16)", 1),
        [84] = ("&'", 2), [85] = ("~&'", 2), [86] = ("^'", 2), [87] = ("|'", 2)
    };

    private static readonly String[] CompoundLet = ["*=", "/=", "%=", "+=", "-=", "<<=", ">>="];

    public static String Decode(Byte[] code, ref Int32 pos)
    {
        var stack = new Stack<String>();

        while (pos < code.Length)
        {
            var op = code[pos++];

            if (op == 0x7F)
                break;

            if (op >= 0x80)
            {
                if (op == 0xD3)     // Memoria's extended-function escape
                {
                    var fn = code[pos] | code[pos + 1] << 8;
                    pos += 2;
                    var argCount = code[pos++];
                    var args = new String[argCount];
                    for (var i = argCount - 1; i >= 0; i--)
                        args[i] = Pop(stack);
                    stack.Push($"Memoria#{fn}({String.Join(", ", args)})");
                }
                else
                {
                    stack.Push(VarRef(op, code, ref pos));
                }
                continue;
            }

            if (Operators.TryGetValue(op, out var known))
            {
                if(known.Arity==1)
                {
                    stack.Push($"({known.Symbol}{Pop(stack)})");
                }
                else
                {
                    var right = Pop(stack);
                    stack.Push($"({Pop(stack)} {known.Symbol} {right})");
                }
                continue;
            }

            switch (op)
            {
                case 41:                // B_MEMBER: a stat of the member target
                    stack.Push(MemberName(code[pos++]));
                    break;
                case 42:                // B_COUNT: set bits in a mask
                    stack.Push($"BitCount({Pop(stack)})");
                    break;
                case 43:                // B_PICK: read a stat of the object(s) in a mask
                {
                    var member = Pop(stack);
                    stack.Push($"{Pop(stack)}->{member}");
                    break;
                }
                case 44 or 45 or 46:    // B_LET family: the value stays on the stack
                {
                    var value = Pop(stack);
                    var target = Pop(stack);
                    var symbol = op switch { 44 => "=", 45 => "=*", _ => "='" };
                    stack.Push($"({target} {symbol} {value})");
                    break;
                }
                case >= 47 and <= 53:   // compound assignment
                {
                    var value = Pop(stack);
                    stack.Push($"({Pop(stack)} {CompoundLet[op - 47]} {value})");
                    break;
                }
                case >= 61 and <= 63:
                {
                    var value = Pop(stack);
                    var symbol = op switch { 61 => "&=", 62 => "^=", _ => "|=" };
                    stack.Push($"({Pop(stack)} {symbol} {value})");
                    break;
                }
                case >= 64 and <= 66:
                {
                    var value = Pop(stack);
                    var symbol = op switch { 64 => "&=*", 65 => "^=*", _ => "|=*" };
                    stack.Push($"({Pop(stack)} {symbol} {value})");
                    break;
                }
                case >= 67 and <= 69:
                {
                    var value = Pop(stack);
                    var symbol = op switch { 67 => "&='", 68 => "^='", _ => "|='" };
                    stack.Push($"({Pop(stack)} {symbol} {value})");
                    break;
                }
                case 75:
                {
                    var b = Pop(stack);
                    stack.Push($"max({Pop(stack)}, {b})");
                    break;
                }
                case 76:
                {
                    var b = Pop(stack);
                    stack.Push($"min({Pop(stack)}, {b})");
                    break;
                }
                case 77:                // B_SELECT: one random set bit of a mask
                    stack.Push($"RandomBit({Pop(stack)})");
                    break;
                case 95:
                    stack.Push($"ptr[{code[pos++]}]");
                    break;
                case 120:               // B_OBJSPECA: uid byte first, var byte second
                {
                    var uid = code[pos++];
                    stack.Push($"obj[{uid}].var[{code[pos++]}]");
                    break;
                }
                case 121:
                    stack.Push(SysListName(code[pos++]));
                    break;
                case 122:               // engine evaluates sysvars at decode time
                    stack.Push(SysvarName(code[pos++]));
                    break;
                case 125:               // B_CONST: i16 LE
                    stack.Push(((Int16)(code[pos] | code[pos + 1] << 8)).ToString());
                    pos += 2;
                    break;
                case 126:               // B_CONST4: 26-bit signed
                {
                    var v = code[pos] | code[pos + 1] << 8 | code[pos + 2] << 16 | code[pos + 3] << 24;
                    pos += 4;
                    stack.Push((((v & 0x3FFFFFF) << 6) >> 6).ToString());
                    break;
                }
                default:
                    stack.Push($"?op{op:X2}?");
                    break;
            }
        }

        return String.Join("; ", stack.Reverse());
    }

    private static String Pop(Stack<String> stack) => stack.Count > 0 ? stack.Pop() : "?";

    private static String VarRef(Byte spec, Byte[] code, ref Int32 pos)
    {
        var index = (Int32)code[pos++];
        if ((spec & 0x20) != 0)
            index |= code[pos++] << 8;
        var source = (spec & 3) switch { 0 => "global", 1 => "map", 2 => "var", _ => "mem" };
        return $"{source}[{index}]";
    }

    private static String MemberName(Byte id) => id switch
    {
        35 => "MAX_HP", 36 => "HP", 37 => "MAX_MP", 38 => "MP",
        39 => "MAX_ATB", 40 => "ATB", 41 => "LEVEL",
        42 => "STATUS_IMMUNE_B", 43 => "STATUS_IMMUNE_A",
        44 => "STATUS_AUTO_B", 45 => "STATUS_AUTO_A",
        46 => "STATUS_CURRENT_B", 47 => "STATUS_CURRENT_A",
        48 => "ELEMENT_IMMUNE", 49 => "ELEMENT_ABSORB",
        50 => "ELEMENT_HALF", 51 => "ELEMENT_WEAK",
        52 => "TARGETABLE", 53 => "DISAPPEAR", 54 => "SHADOW",
        57 => "MODEL", 58 => "MODEL_VISIBLE",
        64 => "ROW", 65 => "COLUMN", 66 => "MODEL_TYPE",
        67 => "CATEGORY", 68 => "CLASS", 69 => "STAND_ANIMATION",
        70 => "ENEMY_ID", 71 => "SET_BONUS",
        72 => "STRENGTH", 73 => "MAGIC", 74 => "DEFENCE", 75 => "EVADE",
        76 => "MAGICAL_DEFENCE", 77 => "MAGICAL_EVADE",
        _ => $"stat[{id}]"
    };

    private static String SysListName(Byte slot) => slot switch
    {
        0 => "SV_Target",
        1 => "SV_FunctionEnemy",
        2 => "AllPlayers",
        3 => "AllEnemies",
        4 => "Everyone",
        5 => "SV_5",
        6 => "SV_6",
        _ => $"SysList[{slot}]"
    };

    private static String SysvarName(Byte id) => id switch
    {
        0 => "Random8",
        6 => "Gil", 8 => "MessageSignal", 9 => "LastChoice",
        20 => "PlayTime", 24 => "BattleMap",
        25 => "SeqBusy", 26 => "InBattle", 27 => "BattleResult",
        28 => "CurCommand", 29 => "CurAbility", 30 => "BattlePhase",
        31 => "BattlePattern", 33 => "PartyGil",
        36 => "Attacker", 37 => "CurTargets",
        38 => "CasterWeaponElement", 39 => "CurElement",
        40 => "LoadStatus", 41 => "BonusExp", 44 => "BonusAP",
        _ => $"Sysvar[{id}]"
    };
}
