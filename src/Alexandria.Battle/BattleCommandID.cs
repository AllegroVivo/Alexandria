namespace Alexandria.Battle;

/// <summary>The commands a reaction script can be told about (mirror: Memoria's
/// BattleCommandId, trimmed to what this engine actually issues).</summary>
public enum BattleCommandID
{
    Attack = 1,
    WhiteMagicGarnet = 17,
    BlackMagic = 22,
    EnemyAtk = 47,
    EnemyCounter = 53
}

