namespace Alexandria.Cards;

/// <summary>One card battle, kept whole so the caller can narrate it
/// (mirror: BattleCalculation).</summary>
public sealed record CardBattle(Int32 AttackStart, Int32 DefenseStart, Int32 AttackRoll, Int32 DefenseRoll)
{
    public Boolean AttackerWins => AttackRoll > DefenseRoll;

    public static (Int32 Attack, Int32 Defense) Stats(TetraCard attacker, TetraCard defender)
    {
        (Int32 Attack, Int32 Defense) raw = attacker.Type switch
        {
            CardType.Physical => (attacker.Attack, defender.PhysicalDefense),
            CardType.Magic => (attacker.Attack, defender.MagicalDefense),
            CardType.Flexible => (attacker.Attack, Math.Min(defender.PhysicalDefense, defender.MagicalDefense)),
            _ => (Math.Max(attacker.Attack, Math.Max(attacker.PhysicalDefense, attacker.MagicalDefense)),
                Math.Min(defender.Attack, Math.Min(defender.PhysicalDefense, defender.MagicalDefense)))
        };
        
        return (Math.Max(1, raw.Attack), Math.Max(1, raw.Defense));
    }

    public static CardBattle Fight(TetraCard attacker, TetraCard defender, Random rng)
    {
        var (attack, defense) = Stats(attacker, defender);
        return new CardBattle(attack, defense, rng.Next(0, attack), rng.Next(0, defense));
    }
}

