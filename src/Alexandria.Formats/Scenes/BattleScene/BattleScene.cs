namespace Alexandria.Formats.Scenes.BattleScene;

public sealed record BattleScene
{
    public required BattleSceneHeader Header;
    public IReadOnlyList<BattleSceneFormation> Formations = [];
    public IReadOnlyList<BattleSceneMonsterParam> Monsters = [];
    public IReadOnlyList<BattleSceneAttack> Attacks = [];
    
    public static BattleScene Read(BinaryReader reader)
    {
        var header = BattleSceneHeader.Read(reader);
        
        List<BattleSceneFormation> formations = [];
        for (var i = 0; i < header.FormationCount; i++)
            formations.Add(BattleSceneFormation.Read(reader, header));
        
        List<BattleSceneMonsterParam> monsters = [];
        for (var i = 0; i < header.EnemyTypeCount; i++)
            monsters.Add(BattleSceneMonsterParam.Read(reader));

        List<BattleSceneAttack> attacks = [];
        for (var i = 0; i < header.AttackCount; i++)
            attacks.Add(BattleSceneAttack.Read(reader));
        
        var battleScene = new BattleScene
        {
            Header = header,
            Formations = formations,
            Monsters = monsters,
            Attacks = attacks
        };
        return battleScene;
    }
}

