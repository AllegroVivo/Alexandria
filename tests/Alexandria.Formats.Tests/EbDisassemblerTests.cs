using Alexandria.Formats.Scripts;

namespace Alexandria.Formats.Tests;

public class EbDisassemblerTests
{
    [Fact]
    public void Constants_and_arithmetic_render_infix()
    {
        // push 10000, push 5, B_PLUS, end
        Byte[] expr = [0x7D, 0x10, 0x27, 0x7D, 0x05, 0x00, 0x14, 0x7F];
        var pos = 0;
        Assert.Equal("(10000 + 5)", ExprDecoder.Decode(expr, ref pos));
        Assert.Equal(expr.Length, pos);
    }

    [Fact]
    public void Variable_specs_decode_source_and_index()
    {
        // varspec 0xC0 index 7 (global[7]), B_CONST 1, B_PLUS, end
        Byte[] expr = [0xC0, 0x07, 0x7D, 0x01, 0x00, 0x14, 0x7F];
        var pos = 0;
        Assert.Equal("(global[7] + 1)", ExprDecoder.Decode(expr, ref pos));
    }

    [Fact]
    public void Ozmas_actual_mystery_bytes_decode()
    {
        // lifted verbatim from EVT_BATTLE_CH_E093.eb file offset 2693:
        // SysList slot 1, member 36 (HP), B_PICK, B_CONST 10000, B_MINUS, end
        Byte[] expr = [0x79, 0x01, 0x29, 0x24, 0x2B, 0x7D, 0x10, 0x27, 0x15, 0x7F];
        var pos = 0;
        Assert.Equal("(SV_FunctionEnemy->HP - 10000)", ExprDecoder.Decode(expr, ref pos));
    }

    [Fact]
    public void The_death_trigger_reads_in_plain_text()
    {
        using var reader = new BinaryReader(File.OpenRead(FF9Data.SceneScript("EVT_BATTLE_CH_E093")));
        var ozma = EbScript.Read(reader).Entries[1];

        var lines = ozma.Functions.SelectMany(f => EbDisassembler.Disassemble(f)).ToList();

        Assert.Contains(lines, l => l.Contains("HP") && l.Contains("10000"));
    }
    
    [Fact]
    public void Attack_constants_render_with_their_seq_names()
    {
        using var reader = new BinaryReader(File.OpenRead(FF9Data.SceneScript("EVT_BATTLE_CH_E093")));
        var counter = EbScript.Read(reader).Entries[1].Functions.Single(f => f.Tag == 6);

        var lines = EbDisassembler.Disassemble(counter,
            new Dictionary<Int32, String> { [9] = "Curaga", [14] = "Berserk" });

        Assert.Contains(lines, l => l.Contains("Attack(9 \"Curaga\")"));
        Assert.Contains(lines, l => l.Contains("Attack(14 \"Berserk\")"));
    }
    
    [Fact]
    public void Ozmas_battle_text_names_and_quest_line()
    {
        var text = BattleText.Load(Path.Combine(FF9Data.Text, "211.mes"));
        Assert.Equal("Ozma", text[0]);
        Assert.Equal("Curaga", text[10]);   // 1 enemy name + attack index 9
        Assert.Equal("The spiritual power raised the attack range.", text[16]);
    }
    
    [Fact]
    public void Ozmas_loop_hunts_levels_divisible_by_four()
    {
        using var reader = new BinaryReader(File.OpenRead(FF9Data.SceneScript("EVT_BATTLE_CH_E093")));
        var loop = EbScript.Read(reader).Entries[1].Functions.Single(f => f.Tag == 1);

        var lines = EbDisassembler.Disassemble(loop);

        Assert.Contains(lines, l => l.Contains("LEVEL % 4"));
        Assert.Contains(lines, l => l.Contains("ATB =* (SV_FunctionEnemy->MAX_ATB - 1)"));
    }
}
