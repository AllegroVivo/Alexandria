namespace Alexandria.Battle;

[Flags]
public enum Element : Byte
{
    None     = 0,
    Fire     = 1,
    Cold     = 2,      // Ice in the UI
    Thunder  = 4,
    Earth    = 8,      // the "grounded" bit — misses floating targets
    Aqua     = 16,     // Water
    Wind     = 32,
    Holy     = 64,
    Darkness = 128     // Shadow — Doomsday's element
}

