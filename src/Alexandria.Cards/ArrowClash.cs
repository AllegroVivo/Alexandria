namespace Alexandria.Cards;

/// <summary>What happens along one edge between a card being played and the neighbor it
/// touches (mirror: CardArrow.CheckDirection's 2 / 1 / -1 / 0).</summary>
public enum ArrowClash
{
    /// <summary>Neither card points at the other. Nothing happens.</summary>
    None,

    /// <summary>Only the DEFENDER points back, so it is safe.</summary>
    Blocked,

    /// <summary>Only the ATTACKER points. The neighbor flips.</summary>
    Flip,

    /// <summary>Both point at each other, so a battle is fought.</summary>
    Battle
}

