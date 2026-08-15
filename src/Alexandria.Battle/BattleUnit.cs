using Alexandria.Formats.Scenes.BattleScene;

namespace Alexandria.Battle;

/// <summary>A living combatant. Mutable on purpose: battle is state that changes.</summary>
public sealed class BattleUnit
{
    public required String Name { get; init; }
    public required Int32 Level { get; init; }
    public required Int32 MaxHP { get; init; }
    public required Int32 MaxMP { get; init; }
    public Int32 CurrentHP { get; set; }
    public Int32 CurrentMP { get; set; }
    public required Int32 Speed { get; init; }
    public required Int32 Strength { get; init; }
    public required Int32 Magic { get; init; }
    public required Int32 Spirit { get; init; }
    public required Int32 PhysicalDefense { get; init; }
    public required Int32 PhysicalEvade { get; init; }
    public required Int32 MagicalDefense { get; init; }
    public required Int32 MagicalEvade { get; init; }
    public Int32 ATBGauge { get; set; }
    
    /// <summary>Basic attack power: weapon power for players, attack data for enemies. Placeholder default.</summary>
    public Int32 AttackPower { get; set; } = 10;

    public Boolean IsAlive => CurrentHP > 0;

    /// <summary>Vanilla gauge size: fast units have SHORTER gauges (btl_para.GetMaxATB, /4).</summary>
    public Int32 ATBMax => (60 - Speed) * 40;

    public Boolean ATBReady => ATBGauge >= ATBMax;

    public static BattleUnit FromEnemy(String name, BattleSceneMonsterParam m) => new()
    {
        Name = name,
        Level = m.Level,
        MaxHP = m.MaxHP,
        CurrentHP = m.MaxHP,
        MaxMP = m.MaxMP,
        CurrentMP = m.MaxMP,
        Speed = m.MonsterStats.Speed,
        Strength = m.MonsterStats.Strength,
        Magic = m.MonsterStats.Magic,
        Spirit = m.MonsterStats.Spirit,
        PhysicalDefense = m.PDefense,
        PhysicalEvade = m.PEvade,
        MagicalDefense = m.MDefense,
        MagicalEvade = m.MEvade
    };
}

