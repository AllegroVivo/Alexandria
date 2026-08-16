namespace Alexandria.Formats.Scripts;

/// <summary>The world as a script sees it. Bite 1 uses only Random8; the rest are
/// the battle bindings the interpreter grows into (stat reads, target masks).</summary>
public interface IEbHost
{
    Int32 Random8(); // sysvar 0 (mirror: Comn.random8)
    Int32 Sysvar(Int32 id);
    Int32 SysList(Int32 slot);
    void SetSysList(Int32 slot, Int32 value);
    Int32 ReadStat(Int32 targetMask, Int32 statId); // B_PICK / B_MEMBER — bite 2
    void WriteStat(Int32 targetMask, Int32 statId, Int32 value);
    Int32 RandomBit(Int32 mask); // B_SELECT — bite 2

    void Attack(Int32 attackId); // BTLCMD — target rides in SysList[0]
}
