using Alexandria.Formats;
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
        public Int32 ReadStat(Int32 mask, CharacterStat stat) => throw new NotSupportedException();
        public Int32 RandomBit(Int32 mask) => throw new NotSupportedException();
        public void Attack(Int32 attackId) => throw new NotSupportedException();
        public void BattleDialog(Int32 textId) => throw new NotImplementedException();
        public void WriteStat(Int32 mask, CharacterStat stat, Int32 value) => throw new NotSupportedException();
        public void BattleSet(Int32 verb, Int32 value) => throw new NotSupportedException();
        public void SetTextVariable(Int32 scriptId, Int32 value) => throw new NotSupportedException();
        public void AttackSpecial(Int32 attackId) => throw new NotSupportedException();
        public void Spawn(Int32 entry, Int32 uid) => throw new NotSupportedException();
        public void FieldOp(Int32 opcode, Int32 uid, IReadOnlyList<Int32> args) => throw new NotSupportedException();
        public Int32 PartyCheck(Int32 character) => throw new NotSupportedException("party check outside the field");
        public Int32 ReadObjectStat(Int32 uid, CharacterStat stat)
            => throw new NotSupportedException($"object {uid} property {stat} — teach me");
        public Int32 Distance(Int32 fromUid, Int32 toUid) => throw new NotSupportedException("no objects outside the field");
        public Int32 ItemCount(Int32 item) => throw new NotSupportedException("no inventory outside the field");
    }

    private static EbVM TypeC(params Int32[] rolls)
    {
        using var reader = new BinaryReader(File.OpenRead(FF9Data.SceneScript("EVT_BATTLE_AC_E028F")));
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
    
    [Fact]
    public void A_presentation_opcode_is_stepped_over_and_recorded()
    {
        // SetWalkAnimation(0x0110), then return. 0x34 touches no game state, so the VM may
        // step over it — but must say that it did.
        var fn = new EbFunction(1, 0, [0x34, 0x00, 0x10, 0x01, 0x04]);
        var vm = new EbVM(new EbEntry(0, 0, 4, 0, [fn]), new ScriptedHost());

        vm.RunFunction(1);

        Assert.Contains(0x34, vm.Skipped);
    }

    [Fact]
    public void An_opcode_that_touches_game_state_still_refuses_to_run()
    {
        // AddItem: absent from Presentation, so it throws rather than quietly losing an item
        var fn = new EbFunction(1, 0, [0x48, 0x00, 0xEC, 0x00, 0x01, 0x04]);
        var vm = new EbVM(new EbEntry(0, 0, 4, 0, [fn]), new ScriptedHost());

        Assert.Throws<NotSupportedException>(() => vm.RunFunction(1));
    }
}
