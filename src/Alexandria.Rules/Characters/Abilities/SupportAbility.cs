namespace Alexandria.Rules;

/// <summary>The 64 support abilities, as ONE flag set.
/// Mirror: the reference splits these across SupportAbility1 and SupportAbility2, two UInt32
/// words, because the PS1 save held two fields — the bit POSITION is the id in AbilityGems.csv
/// and the word is just where it landed. A UInt64 holds the same information without the seam.
/// Comments carry the display name and the magic-stone cost from AbilityGems.csv.</summary>
[Flags]
public enum SupportAbility : UInt64
{
    None = 0,

    AutoReflect = 1UL << 0,       // Auto-Reflect, 15 gems
    AutoFloat = 1UL << 1,         // Auto-Float, 6 gems
    AutoHaste = 1UL << 2,         // Auto-Haste, 9 gems
    AutoRegen = 1UL << 3,         // Auto-Regen, 10 gems
    AutoLife = 1UL << 4,          // Auto-Life, 12 gems
    HP10 = 1UL << 5,              // HP+10%, 4 gems
    HP20 = 1UL << 6,              // HP+20%, 8 gems
    MP10 = 1UL << 7,              // MP+10%, 4 gems
    MP20 = 1UL << 8,              // MP+20%, 8 gems
    Accuracy = 1UL << 9,          // Accuracy+, 2 gems
    Distract = 1UL << 10,         // Distract, 5 gems
    LongReach = 1UL << 11,        // Long Reach, 16 gems
    MPAttack = 1UL << 12,         // MP Attack, 5 gems
    BirdKiller = 1UL << 13,       // Bird Killer, 3 gems
    BugKiller = 1UL << 14,        // Bug Killer, 2 gems
    StoneKiller = 1UL << 15,      // Stone Killer, 4 gems
    UndeadKiller = 1UL << 16,     // Undead Killer, 2 gems
    DragonKiller = 1UL << 17,     // Dragon Killer, 3 gems
    DevilKiller = 1UL << 18,      // Devil Killer, 2 gems
    BeastKiller = 1UL << 19,      // Beast Killer, 4 gems
    ManEater = 1UL << 20,         // Man Eater, 2 gems
    HighJump = 1UL << 21,         // High Jump, 4 gems
    MasterThief = 1UL << 22,      // Master Thief, 5 gems
    StealGil = 1UL << 23,         // Steal Gil, 5 gems
    Healer = 1UL << 24,           // Healer, 2 gems
    AddStatus = 1UL << 25,        // Add Status, 3 gems
    GambleDefence = 1UL << 26,    // Gamble Defence, 1 gems
    Chemist = 1UL << 27,          // Chemist, 4 gems
    PowerThrow = 1UL << 28,       // Power Throw, 19 gems
    PowerUp = 1UL << 29,          // Power Up, 3 gems
    ReflectNull = 1UL << 30,      // Reflect-Null, 7 gems
    Reflectx2 = 1UL << 31,        // Reflectx2, 17 gems
    MagElemNull = 1UL << 32,      // Mag Elem Null, 13 gems
    Concentrate = 1UL << 33,      // Concentrate, 10 gems
    HalfMP = 1UL << 34,           // Half MP, 11 gems
    HighTide = 1UL << 35,         // High Tide, 8 gems
    Counter = 1UL << 36,          // Counter, 8 gems
    Cover = 1UL << 37,            // Cover, 6 gems
    ProtectGirls = 1UL << 38,     // Protect Girls, 4 gems
    Eye4Eye = 1UL << 39,          // Eye 4 Eye, 5 gems
    BodyTemp = 1UL << 40,         // Body Temp, 4 gems
    Alert = 1UL << 41,            // Alert, 4 gems
    Initiative = 1UL << 42,       // Initiative, 5 gems
    LevelUp = 1UL << 43,          // Level Up, 7 gems
    AbilityUp = 1UL << 44,        // Ability Up, 3 gems
    Millionaire = 1UL << 45,      // Millionaire, 5 gems
    FleeGil = 1UL << 46,          // Flee-Gil, 3 gems
    GuardianMog = 1UL << 47,      // Guardian Mog, 3 gems
    Insomniac = 1UL << 48,        // Insomniac, 5 gems
    Antibody = 1UL << 49,         // Antibody, 4 gems
    BrightEyes = 1UL << 50,       // Bright Eyes, 4 gems
    Loudmouth = 1UL << 51,        // Loudmouth, 4 gems
    RestoreHP = 1UL << 52,        // Restore HP, 8 gems
    Jelly = 1UL << 53,            // Jelly, 4 gems
    ReturnMagic = 1UL << 54,      // Return Magic, 9 gems
    AbsorbMP = 1UL << 55,         // Absorb MP, 6 gems
    AutoPotion = 1UL << 56,       // Auto-Potion, 3 gems
    Locomotion = 1UL << 57,       // Locomotion, 4 gems
    ClearHeaded = 1UL << 58,      // Clear Headed, 5 gems
    Boost = 1UL << 59,            // Boost, 12 gems
    OdinSword = 1UL << 60,        // Odin’s Sword, 5 gems
    Mug = 1UL << 61,              // Mug, 3 gems
    Bandit = 1UL << 62,           // Bandit, 5 gems
    Void = 1UL << 63,             // Void, 20 gems
}
