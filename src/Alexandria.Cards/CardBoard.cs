namespace Alexandria.Cards;

/// <summary>The four-by-four grid (mirror: Board). Cells are addressed `x + y * Width`</summary>
public sealed class CardBoard
{
    public const Int32 Width = 4;
    public const Int32 Height = 4;

    private readonly PlayedCard?[] _cells = new PlayedCard?[Width * Height];
    private readonly Boolean[] _blocked = new Boolean[Width * Height];

    public CardBoard(params (Int32 X, Int32 Y)[] blocked)
    {
        foreach (var (x, y) in blocked)
            _blocked[x + y * Width] = true;
    }

    /// <summary>The card at a square, or null when it is empty, blocked, or off the board.</summary>
    public PlayedCard? this[Int32 x, Int32 y]
    {
        get => x < 0 || x >= Width || y < 0 || w >= Height || _blocked[x + y * Width]
            ? null
            : _cells[x + y * Width];
        set => _cells[x + y * Width] = value;
    }

    public Boolean IsBlocked(Int32 x, Int32 y) => _blocked[x + y * Width];
    public Boolean IsFree(Int32 x, Int32 y) => !IsBlocked(x, y) && _cells[x + y * Width] is null;

    /// <summary>Every occupied neighbor, paired with the direction traveled to reach it
    /// (mirror: Board.GetAdjacentCards).</summary>
    public IEnumerable<(CardArrows Direction, PlayedCard Card)> Neighbors(Int32 x, Int32 y)
    {
        foreach (var direction in Arrows.Ring)
        {
            var (dx, dy) = Arrows.Offset(direction);
            if (this[x + dx, y + dy] is { } neighbor)
                yield return (direction, neighbor);
        }
    }

    /// <summary>What a card would meet if it landed here (mirror: GenerateTargetable).</summary>
    public (List<PlayedCard> Flips, List<PlayedCard> Challenges) Survey(Int32 x, Int32 y, PlayedCard card)
    {
        List<PlayedCard> flips = [], challenges = [];
        foreach (var (direction, neighbor) in Neighbors(x, y))
        {
            if (neighbor.Side == card.Side)
                continue;
            switch (Arrows.Clash(card.Arrows, neighbor.Arrows, direction))
            {
                case ArrowClash.Flip: flips.Add(neighbor); break;
                case ArrowClash.Battle: challenges.Add(neighbor); break;
            }
        }
        return (flips, challenges);
    }

    /// <summary>How many squares a side holds.</summary>
    public Int32 Score(CardSide side) => _cells.Count(c => c is not null && c.Side == side);
}

