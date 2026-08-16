using Alexandria.Formats.Scripts;

public class EbVMTests
{
    private sealed class ScriptedHost(params Int32[] rolls) : IEbHost
    {
        private readonly Queue<Int32> _rolls = new(rolls);
        public Int32 Random8() => _rolls.Dequeue();
        public Int32 Sysvar(Int32 id) => id == 0 ? Random8() : throw new NotSupportedException();
        public Int32 SysList(Int32 slot) => throw new NotSupportedException();
        public void SetSysList(Int32 slot, Int32 value) => throw new NotSupportedException();
        public Int32 ReadStat(Int32 mask, Int32 stat) => throw new NotSupportedException();
        public Int32 RandomBit(Int32 mask) => throw new NotSupportedException();
        public void Attack(Int32 attackId) => throw new NotSupportedException();
        public void WriteStat(Int32 mask, Int32 stat, Int32 value) => throw new NotSupportedException();
    }

    private static EbVM TypeC(params Int32[] rolls)
    {
        using var reader = new BinaryReader(File.OpenRead(
            @"C:\Dev\Memoria\FF9Data\BattleMap\BattleScene\EVT_BATTLE_AC_E028F\EVT_BATTLE_AC_E028F.eb"));
        return new EbVM(EbScript.Read(reader).Entries[1], new ScriptedHost(rolls));
    }

    [Fact]
    public void Init_loads_the_constant_tables()
    {
        var vm = TypeC(0, 0, 0);
        vm.RunFunction(0);

        Assert.Equal(794688, vm.Var24(3));      // the attack-id table, verbatim
        Assert.Equal(3195648, vm.Var24(9));     // the MP table
        Assert.Equal(4, vm.Var8(22));           // deck size
    }

    [Fact]
    public void Init_shuffles_the_deck_with_the_scripted_rolls()
    {
        // rolls (0,0,0): slot3←0, slot2←1, slot1←2, remainder 3 → packed queue
        // var[15] = 3 | 2<<3 | 1<<6 | 0<<9 = 83
        var vm = TypeC(0, 0, 0);
        vm.RunFunction(0);

        Assert.Equal(83, vm.Var24(15));
    }
}
