using Alexandria.Formats.Scripts;

namespace Alexandria.Battle;

public sealed class FieldHost(WorldState world, Random rng) : IEbHost
{
    public Int32 Random8() => rng.Rand8();

    public Int32 Sysvar(Int32 id) => id switch
    {
        0 => Random8(),
        6 => world.Gil,
        16 => world.Frogs,
        33 => world.Gil,
        _ => throw new NotSupportedException($"field sysvar {id} — teach me")
    };

    public Int32 SysList(Int32 slot) => 0;
    public void SetSysList(Int32 slot, Int32 value) { }
    public Int32 RandomBit(Int32 mask) => mask;
    public void Spawn(Int32 entry, Int32 uid) { }

    public Int32 ReadStat(Int32 targetMask, CharacterStat stat)
        => throw new NotSupportedException($"field stat read {stat} — teach me");
    public void WriteStat(Int32 targetMask, CharacterStat stat, Int32 value)
        => throw new NotSupportedException($"field stat write {stat} — teach me");

    public void Attack(Int32 attackId) => throw new NotSupportedException("Attack on the field.");
    public void AttackSpecial(Int32 attackId) => throw new NotSupportedException("AttackSpecial on the field");
    public void BattleSet(Int32 verb, Int32 value) => throw new NotSupportedException("BattleSet on the field");
    public void BattleDialog(Int32 textId) { }
    public void SetTextVariable(Int32 scriptId, Int32 value) { }
}

