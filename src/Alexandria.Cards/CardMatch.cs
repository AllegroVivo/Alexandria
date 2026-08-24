namespace Alexandria.Cards;

/// <summary>One game of Tetra Master (mirror: QuadMistGame's START and PLAY states).</summary>
public sealed class CardMatch
{
    public const Int32 HandSize = 5;

    private readonly Random _rng;
    private readonly List<TetraCard> _player;
    private readonly List<TetraCard> _opponent;

    private readonly List<(PlayedCard Card, CardSide Owner)> _played = [];
    
    public CardBoard Board { get; }
    public CardSide Turn { get; private set; }

    public IReadOnlyList<PlayedCard> PlayerCards
        => [.. _played.Where(p => p.Owner == CardSide.Player).Select(p => p.Card)];

    public CardMatch(IReadOnlyList<TetraCard> player, IReadOnlyList<TetraCard> opponent, Random rng)
    {
        _rng = rng;
        _player = [.. player];
        _opponent = [.. opponent];
        Board = new CardBoard([.. RollBlocks(rng)]);
        Turn = rng.Next(0, 2) == 0 ? CardSide.Player : CardSide.Opponent;  // coin toss
    }

    public IReadOnlyList<TetraCard> Hand(CardSide side) => side == CardSide.Player ? _player : _opponent;

    public Boolean IsOver => _player.Count == 0 && _opponent.Count == 0;

    public MatchResult Result =>
        Board.Score(CardSide.Player).CompareTo(Board.Score(CardSide.Opponent)) switch
        {
            > 0 => MatchResult.Win,
            < 0 => MatchResult.Lose,
            _ => MatchResult.Draw
        };

    public PlayOutcome Play(Int32 x, Int32 y, TetraCard card,
        Func<IReadOnlyList<PlayedCard>, PlayedCard?> choose)
    {
        (Turn == CardSide.Player ? _player : _opponent).Remove(card);
        var played = new PlayedCard(card, Turn);
        _played.Add((played, Turn));
        var outcome = CardPlay.Play(Board, x, y, played, _rng, choose);
        Turn = Turn == CardSide.Player ? CardSide.Opponent : CardSide.Player;
        return outcome;
    }

    /// <summary>How many squares get blocked off (mirror: the nested ternary at
    /// START_STATE.SETUP).</summary>
    public static Int32 RollBlockCount(Random rng) => rng.Next(0, 100) switch
    {
        < 3 => 0,
        < 7 => 1,
        < 12 => 2,
        < 17 => 3,
        < 27 => 4,
        < 97 => 5,
        _ => 6
    };

    private static IEnumerable<(Int32 x, Int32 y)> RollBlocks(Random rng)
    {
        var count = RollBlockCount(rng);
        var squares = Enumerable.Range(0, CardBoard.Width * CardBoard.Height).ToList();
        for (var i = 0; i < count; i++)
        {
            var pick = squares[rng.Next(0, squares.Count)];
            squares.Remove(pick);
            yield return (pick % CardBoard.Width, pick / CardBoard.Width);
        }
    }

    /// <summary>What the winner may keep one of (mirror: matchResult.selectable): the
    /// LOSER'S OWN cards that the winner ended up holding.</summary>
    public IReadOnlyList<PlayedCard> Spoils => Result switch
    {
        MatchResult.Win => [.. Captured(CardSide.Opponent, CardSide.Player)],
        MatchResult.Lose => [.. Captured(CardSide.Player, CardSide.Opponent)],
        _ => []
    };

    private IEnumerable<PlayedCard> Captured(CardSide from, CardSide by)
        => _played.Where(p => p.Owner == from && p.Card.Side == by).Select(p => p.Card);

    /// <summary>Mirror: RestoreCards. On a PLAYER WIN only, every card ending up in the
    /// player's hands creeps one stat upward.</summary>
    public void AwardLevels(IReadOnlyList<TetraCard> pool, Random rng)
    {
        if (Result != MatchResult.Win)
            return;
        foreach (var (played, _) in _played.Where(p => p.Card.Side == CardSide.Player))
            played.LevelUp(pool[played.Card.ID], rng);
    }
}

