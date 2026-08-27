using System.Xml;
using Alexandria.Cards;
using Alexandria.Formats;

namespace ff9tool;

public static class TetraCommand
{
    public static void Run()
    {
        var rng = new Random();
        var pool = CardPool.Load(FF9Data.CardData, FF9Data.CardTitles, FF9Data.TripleTriadCsv);
        Directory.CreateDirectory(FF9Data.SaveDir);
        var album = CardAlbum.Load(FF9Data.AlbumPath, pool);

        while (album.Cards.Count < CardMatch.HandSize)
            album.Cards.Add(CardDeal.Create(pool[rng.Next(0, pool.Count)], rng));
        
        Console.WriteLine($"— album: {album.Cards.Count} cards, " +
                          $"{album.Wins}W {album.Losses}L {album.Draws}D —");

        var hand = album.Cards.OrderBy(_ => rng.Next()).Take(CardMatch.HandSize).ToList();
        foreach (var card in hand)
            album.Cards.Remove(card);

        var match = new CardMatch(hand, CardDeal.Deal(pool, CardMatch.HandSize, rng), rng);
        
        Console.WriteLine($"— {match.Turn} goes first —");

        while (!match.IsOver)
        {
            Draw(match.Board);
            Console.WriteLine($"  You {match.Board.Score(CardSide.Player)}" +
                              $"  ·  Them {match.Board.Score(CardSide.Opponent)}");

            var brain = new CardOpponent();
            var outcome = match.Turn == CardSide.Player ? PlayerTurn(match) : OpponentTurn(match, brain, rng);
            Narrate(outcome);
        }

        Draw(match.Board);
        Console.WriteLine(match.Result switch
        {
            MatchResult.Win => $"\n*** You win, {match.Board.Score(CardSide.Player)}–{match.Board.Score(CardSide.Opponent)} ***",
            MatchResult.Lose => $"\n*** You lose, {match.Board.Score(CardSide.Player)}–{match.Board.Score(CardSide.Opponent)} ***",
            _ => "\n*** A draw ***"
        });

        match.AwardLevels(pool, rng);

        foreach (var played in match.PlayerCards)
            album.Cards.Add(played.Card);

        if (match.Spoils.Count > 0)
        {
            var taken = match.Spoils[rng.Next(0, match.Spoils.Count)];
            if (match.Result == MatchResult.Win)
            {
                album.Cards.Add(taken.Card);
                Console.WriteLine($"  You take {taken.Card.Face} {taken.Card.Name}!");
            }
            else
            {
                album.Cards.Remove(taken.Card);
                Console.WriteLine($"  They take {taken.Card.Face} {taken.Card.Name}.");
            }
        }

        switch (match.Result)
        {
            case MatchResult.Win: album.Wins++; break;
            case MatchResult.Lose: album.Losses++; break;
            default: album.Draws++; break;
        }

        album.Save(FF9Data.AlbumPath);
        Console.WriteLine($"  album: {album.Cards.Count} cards, " +
                          $"{album.Wins}W {album.Losses}L {album.Draws}D");
    }

    private static PlayOutcome PlayerTurn(CardMatch match)
    {
        var hand = match.Hand(CardSide.Player);
        for (var i = 0; i < hand.Count; i++)
            Console.WriteLine($"  [{i + 1}] {hand[i].Face} {Arrows.Glyphs(hand[i].Arrows),-8} {hand[i].Name}");
        
        var card = hand[Ask("Play which card? ", hand.Count) - 1];
        Int32 x, y;
        do
        {
            x = Ask("  column (0-3)? ", CardBoard.Width, from: 0);
            y = Ask("  row (0-3)? ", CardBoard.Height, from: 0);
        } while (!match.Board.IsFree(x, y));

        if (!match.Board.IsFree(x, y))
            (x, y) = FirstFree(match.Board);

        return match.Play(x, y, card, Choose);
    }

    private static (Int32 X, Int32 Y) FirstFree(CardBoard board)
        => (from y in Enumerable.Range(0, CardBoard.Height)
            from x in Enumerable.Range(0, CardBoard.Width)
            where board.IsFree(x, y)
            select (x, y)).First();

    private static PlayedCard Choose(IReadOnlyList<PlayedCard> options)
    {
        if (options.Count == 1)
            return options[0];

        Console.WriteLine("  it can be fought for:");
        for (var i = 0; i < options.Count; i++)
            Console.WriteLine($"    [{i + 1}] {options[i]}");
        return options[Ask("  fight which? ", options.Count) - 1];
    }

    private static PlayOutcome OpponentTurn(CardMatch match, CardOpponent brain, Random rng)
    {
        var (card, x, y) = brain.Choose(match.Board, match.Hand(CardSide.Opponent),
            match.Board.Score(CardSide.Opponent), match.Board.Score(CardSide.Player), rng);
        
        Console.WriteLine($"  They play {card.Face} {Arrows.Glyphs(card.Arrows)} {card.Name} at {x},{y}");
        return match.Play(x, y, card, o => o[0]);
    }

    private static void Narrate(PlayOutcome outcome)
    {
        foreach (var battle in outcome.Battles)
            Console.WriteLine($"    fight: {battle.AttackStart:X2} vs {battle.DefenseStart:X2}" +
                              $" → rolled {battle.AttackRoll:X2} vs {battle.DefenseRoll:X2}" +
                              $" — {(battle.AttackerWins ? "won" : "lost")}");
        if (outcome.AttackerTaken)
            Console.WriteLine("    the card is taken!");
        if (outcome.Flipped.Count > 0)
            Console.WriteLine($"    {outcome.Flipped.Count} card(s) change hands");
    }

    private static void Draw(CardBoard board)
    {
        Console.WriteLine("\n        0          1          2          3");
        for (var y = 0; y < CardBoard.Height; y++)
        {
            List<String> faces = [], arrows = [];
            for (var x = 0; x < CardBoard.Width; x++)
            {
                if (board.IsBlocked(x, y))
                {
                    faces.Add("########");
                    arrows.Add("");
                }
                else if (board[x, y] is { } card)
                {
                    faces.Add($"{card.Card.Face} {(card.Side == CardSide.Player ? "you" : "them")}");
                    arrows.Add(Arrows.Glyphs(card.Arrows));
                }
                else
                {
                    faces.Add("........");
                    arrows.Add("");
                }
            }
            Console.WriteLine($"  {y}   " + String.Join(" ", faces.Select(f => f.PadRight(10))));
            Console.WriteLine("      " + String.Join(" ", arrows.Select(a => a.PadRight(10))));
        }
    }

    /// <summary>Reads a number in [from, from+count).</summary>
    private static Int32 Ask(String question, Int32 count, Int32 from = 1)
    {
        while (true)
        {
            Console.WriteLine(question);
            var line = Console.ReadLine();
            if (line is null)
                return from;
            if (Int32.TryParse(line, out var pick) && pick >= from && pick < from + count)
                return pick;
        }
    }
}

