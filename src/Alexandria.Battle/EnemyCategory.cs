namespace Alexandria.Battle;

[Flags]
public enum EnemyCategory : Byte
{
    None = 0, // "Other" in the reference: fits no bucket
    Humanoid = 1,
    Beast = 2,
    Devil = 4,
    Dragon = 8,
    Undead = 16,
    Stone = 32,
    Soul = 64,
    Flight = 128
}
