namespace Alexandria.Rules;

[Flags]
public enum CharacterCategory : Byte
{
    None = 0,
    Male = 1,
    Female = 2,
    Gaia = 4,
    Terra = 8,
    SubPC = 16, // a guest: Cinna, Marcus, Blank, Beatrix
    Reserve1 = 32,
    Reserve2 = 64,
    Reserve3 = 128
}
