namespace Alexandria.Formats.Scenes.BattleScene;

public sealed record BattleSceneMonsterParam(
    UInt32 ResistStatus,
    UInt32 AutoStatus,
    UInt32 InitialStatus,
    UInt16 MaxHP,
    UInt16 MaxMP,
    UInt16 WinGil,
    UInt16 WinExp,
    IReadOnlyList<Byte> WinItems,
    IReadOnlyList<UInt16> WinItemRates,
    IReadOnlyList<Byte> StealItems,
    IReadOnlyList<UInt16> StealItemRates,
    UInt16 Radius,
    Int16 Geo,
    IReadOnlyList<UInt16> Mot,
    IReadOnlyList<UInt16> Mesh,
    UInt16 Flags,
    UInt16 AP,
    BattleStats MonsterStats,
    Byte GuardElement,
    Byte AbsorbElement,
    Byte HalfElement,
    Byte WeakElement,
    Byte Level,
    Byte Category,
    Byte HitRate,
    Byte PDefense,
    Byte PEvade,
    Byte MDefense,
    Byte MEvade,
    Byte BlueMagic,
    IReadOnlyList<Byte> Bones,
    UInt16 DieSfx,
    Byte Konran,
    Byte MesCount,
    IReadOnlyList<Byte> IconBones,
    IReadOnlyList<SByte> IconYs,
    IReadOnlyList<SByte> IconZs,
    UInt16 StartSFX,
    UInt16 ShadowX,
    UInt16 ShadowZ,
    Byte ShadowBone,
    Byte WinCard,
    Int16 ShadowOffsetX,
    Int16 ShadowOffsetZ,
    Byte ShadowBone2)
{
    public static readonly UInt16[] DefaultWinItemRates = [256, 96, 32, 1];
    public static readonly UInt16[] DefaultStealItemRates = [256, 64, 16, 1];
    public static readonly UInt16 DefaultWinCardRate = 32;

    public static BattleSceneMonsterParam Read(BinaryReader reader)
    {
        var resistStatus = reader.ReadUInt32();
        var autoStatus = reader.ReadUInt32();
        var initialStatus = reader.ReadUInt32();
        var maxHP = reader.ReadUInt16();
        var maxMP = reader.ReadUInt16();
        var winGil = reader.ReadUInt16();
        var winExp = reader.ReadUInt16();

        List<Byte> winItems = [];
        List<UInt16> winItemRates = [];
        for (var i = 0; i < 4; i++)
        {
            winItems.Add(reader.ReadByte());
            winItemRates.Add(DefaultWinItemRates[i]);
        }
        List<Byte> stealItems = [];
        List<UInt16> stealItemRates = [];
        for (var i = 0; i < 4; i++)
        {
            stealItems.Add(reader.ReadByte());
            stealItemRates.Add(DefaultStealItemRates[i]);
        }
        
        var radius = reader.ReadUInt16();
        var geo = reader.ReadInt16();
        
        List<UInt16> mot = [];
        List<UInt16> mesh = [];
        for (var i = 0; i < 6; i++)
            mot.Add(reader.ReadUInt16());
        for (var i = 0; i < 2; i++)
            mesh.Add(reader.ReadUInt16());
        
        var flags = reader.ReadUInt16();
        var ap = reader.ReadUInt16();
        
        var monsterStats = BattleStats.Read(reader);

        var guardElement = reader.ReadByte();
        var absorbElement = reader.ReadByte();
        var halfElement = reader.ReadByte();
        var weakElement = reader.ReadByte();
        
        var level = reader.ReadByte();
        var category = reader.ReadByte();
        var hitRate = reader.ReadByte();
        var pDefense = reader.ReadByte();
        var pEvade = reader.ReadByte();
        var mDefense = reader.ReadByte();
        var mEvade = reader.ReadByte();
        var blueMagic = reader.ReadByte();

        List<Byte> bones = [];
        for (var i = 0; i < 4; i++)
            bones.Add(reader.ReadByte());

        var dieSfx = reader.ReadUInt16();
        var konran = reader.ReadByte();
        var mesCount = reader.ReadByte();

        List<Byte> iconBones = [];
        List<SByte> iconYs = [];
        List<SByte> iconZs = [];
        for (var i = 0; i < 6; i++)
            iconBones.Add(reader.ReadByte());
        for (var i = 0; i < 6; i++)
            iconYs.Add(reader.ReadSByte());
        for (var i = 0; i < 6; i++)
            iconZs.Add(reader.ReadSByte());

        var startSfx = reader.ReadUInt16();
        var shadowX = reader.ReadUInt16();
        var shadowZ = reader.ReadUInt16();
        var shadowBone = reader.ReadByte();
        var winCard = reader.ReadByte();
        var shadowOffsetX = reader.ReadInt16();
        var shadowOffsetZ = reader.ReadInt16();
        var shadowBone2 = reader.ReadByte();
        reader.ReadByte();  // Pad0
        reader.ReadUInt16();  // Pad1
        reader.ReadUInt16();  // Pad2

        return new BattleSceneMonsterParam(
            resistStatus, autoStatus, initialStatus, maxHP, maxMP, winGil,
            winExp, winItems, winItemRates, stealItems, stealItemRates,
            radius, geo, mot, mesh, flags, ap, monsterStats, guardElement, 
            absorbElement, halfElement, weakElement, level, category, hitRate, 
            pDefense, pEvade, mDefense, mEvade, blueMagic, bones, dieSfx,
            konran, mesCount, iconBones, iconYs, iconZs, startSfx, shadowX,
            shadowZ, shadowBone, winCard, shadowOffsetX, shadowOffsetZ,
            shadowBone2);
    }
}

