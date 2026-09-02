namespace Alexandria.Formats.Scripts;

/// <summary>The world as a script sees it. Bite 1 uses only Random8; the rest are
/// the battle bindings the interpreter grows into (stat reads, target masks).</summary>
public interface IEbHost
{
    Int32 Random8(); // sysvar 0 (mirror: Comn.random8)
    Int32 Sysvar(Int32 id);
    Int32 SysList(Int32 slot);
    void SetSysList(Int32 slot, Int32 value);
    Int32 ReadStat(Int32 targetMask, CharacterStat stat); // B_PICK / B_MEMBER — bite 2
    void WriteStat(Int32 targetMask, CharacterStat stat, Int32 value);
    Int32 RandomBit(Int32 mask); // B_SELECT — bite 2
    void Attack(Int32 attackId); // BTLCMD — target rides in SysList[0]
    void BattleDialog(Int32 textId); // MESB 0xD0 — fire-and-forget HUD text, never blocks
    void BattleSet(Int32 verb, Int32 value); // BTLSET 0x4A — the stage verbs
    void SetTextVariable(Int32 scriptId, Int32 value); // MESVALUE 0x66
    void AttackSpecial(Int32 attackId); // BTLSEQ 0xE5 — target rides in SysList[0]
    void Spawn(Int32 entry, Int32 uid); // NEW/NEW2/NEW3 0x07-0x09 — declare a script object
    void FieldOp(Int32 opcode, Int32 uid, IReadOnlyList<Int32> args);
    Int32 ReadObjectStat(Int32 uid, CharacterStat stat);
    /// <summary>Whether this character is one of the four active party members.</summary>
    Int32 PartyCheck(Int32 character);
    /// <summary>The distance between two objects on the ground plane.</summary>
    Int32 Distance(Int32 fromUid, Int32 toUid);
    /// <summary>How many of this item the party holds.</summary>
    Int32 ItemCount(Int32 item);
}
