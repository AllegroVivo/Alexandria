using Alexandria.Formats.Scenes.BattleScene;

namespace Alexandria.Battle;

/// <summary>A living combatant. Mutable on purpose: battle is state that changes.</summary>
public sealed class BattleUnit
{
    public required String Name { get; init; }
    public required Int32 Level { get; init; }
    public required Int32 MaxHP { get; set; }
    public required Int32 MaxMP { get; set; }
    public Int32 CurrentHP { get; set; }
    public Int32 CurrentMP { get; set; }
    public required Int32 Speed { get; init; }
    public required Int32 Strength { get; set; }
    public required Int32 Magic { get; set; }
    public required Int32 Spirit { get; init; }
    public required Int32 PhysicalDefense { get; set; }
    public required Int32 PhysicalEvade { get; set; }
    public required Int32 MagicalDefense { get; set; }
    public required Int32 MagicalEvade { get; set; }
    public Int32 ATBGauge { get; set; }
    public BattleStatus Statuses { get; set; }
    
    /// <summary>Basic attack power: weapon power for players, attack data for enemies. Placeholder default.</summary>
    public Int32 AttackPower { get; set; } = 10;
    
    public Dictionary<BattleStatus, Int32> OprCounters { get; } = [];
    public Dictionary<BattleStatus, Int32> ContiCounters { get; } = [];
    
    public IEnemyBrain? Brain { get; set; }
    
    public List<BattleAbility> Abilities { get; } = [];

    public Boolean IsAlive => CurrentHP > 0;
    
    /// <summary>ENEMY.FLG_DIE_ATK (bit 0 of SB2 Flags): this enemy runs a death
    /// script (tag 9) when killed — the boss "death scene" flag.</summary>
    public Boolean DiesFighting { get; set; }

    /// <summary>Vanilla gauge size: fast units have SHORTER gauges (btl_para.GetMaxATB, /4).</summary>
    public Int32 ATBMax => (60 - Speed) * 40;

    public Boolean ATBReady => ATBGauge >= ATBMax;
    
    public Element GuardElements { get; set; }
    public Element AbsorbElements { get; set; }
    public Element HalfElements { get; set; }
    public Element WeakElements { get; set; }
    public Element BonusElements { get; set; }   // Elem-Atk boost gear (p_up_attr)
    public Element WeaponElement { get; set; }   // damage element of the equipped weapon
    
    public BattleStatus PermanentStatuses { get; set; }
    public BattleStatus ImmuneStatuses { get; set; }
    
    public Int32 StandAnimation { get; set; }
    public Int32 Row { get; set; }
    public Int32 Category { get; set; }
    public Int32 ModelType { get; set; }
    public Int32 ModelID { get; set; }

    /// <summary>bi.t_gauge - whether this unit has a Trance gauge at all. Enemies get one
    /// only when they are not immune to Trance (btl_init.cs:64); players have one unless
    /// they are a temporary character or the story flag is off.</summary>
    public Boolean HasTrance { get; set; } = true;
    
    public const Int32 TranceMax = 255;
    
    /// <summary>btl.trance, 0–TranceMax.</summary>
    public Int32 TranceGauge { get; set; }

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
        MagicalEvade = m.MEvade,
        GuardElements = (Element)m.GuardElement,
        AbsorbElements = (Element)m.AbsorbElement,
        HalfElements = (Element)m.HalfElement,
        WeakElements = (Element)m.WeakElement,
        Row = 2,
        Category = m.Category,
        ImmuneStatuses = (BattleStatus)m.ResistStatus,
        DiesFighting = (m.Flags & 1) != 0,
        ModelID = m.Geo,
        HasTrance = ((BattleStatus)m.ResistStatus & BattleStatus.Trance) == 0
    };

    public Boolean Has(BattleStatus status) => (Statuses & status) != 0;
}

