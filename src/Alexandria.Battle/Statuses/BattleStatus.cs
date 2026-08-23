namespace Alexandria.Battle;

[Flags]
public enum BattleStatus : UInt32
{
    None = 0,
    Petrify = 1u << 0,
    Venom = 1u << 1,
    Virus = 1u << 2,
    Silence = 1u << 3,
    Blind = 1u << 4,
    Trouble = 1u << 5,
    Zombie = 1u << 6,
    EasyKill = 1u << 7,
    Death = 1u << 8,
    LowHP = 1u << 9,
    Confuse = 1u << 10,
    Berserk = 1u << 11,
    Stop = 1u << 12,
    AutoLife = 1u << 13,
    Trance = 1u << 14,
    Defend = 1u << 15,
    Poison = 1u << 16,
    Sleep = 1u << 17,
    Regen = 1u << 18,
    Haste = 1u << 19,
    Slow = 1u << 20,
    Float = 1u << 21,
    Shell = 1u << 22,
    Protect = 1u << 23,
    Heat = 1u << 24,
    Freeze = 1u << 25,
    Vanish = 1u << 26,
    Doom = 1u << 27,
    Mini = 1u << 28,
    Reflect = 1u << 29,
    Jump = 1u << 30,
    GradualPetrify = 1u << 31,

    PreventATBConfirm = Venom | Sleep | Freeze,
    CannotAct = Petrify | Venom | Stop | Freeze,
    CannotTrance = Petrify | Venom | Zombie | Death | Stop | Trance | Freeze,
    NoInput = Petrify | Venom | Death | Confuse | Berserk | Sleep | Stop | Freeze | Jump,
    CannotEscape = Petrify | Venom | Zombie | Death | Stop | Sleep | Freeze | Jump,

    AnyPositive = AutoLife | Trance | Defend | Regen | Haste | Float | Shell
                  | Protect | Vanish | Reflect,
    AnyNegative = Petrify | Venom | Virus | Silence | Blind | Trouble | Zombie
                  | Death | Confuse | Stop | Poison | Sleep | Slow | Heat | Freeze
                  | Doom | Mini | GradualPetrify
}

