using System.Numerics;

namespace Alexandria.Cards;

public static class Arrows
{
    /// <summary>The eight directions in ring order, clockwise from Up.</summary>
    public static readonly CardArrows[] Ring =
    [
        CardArrows.Up, CardArrows.UpRight, CardArrows.Right, CardArrows.DownRight,
        CardArrows.Down, CardArrows.DownLeft, CardArrows.Left, CardArrows.UpLeft
    ];

    /// <summary>How many arrows a new card gets, as percentages indexed by the count
    /// (mirror: CardPool.probability).</summary>
    private static readonly Int32[] ArrowCountOdds = [1, 8, 25, 31, 18, 9, 5, 2, 1];

    extension(CardArrows arrows)
    {
        /// <summary>Every arrow turned to face the other way (mirror: CardArrow.Reverse).</summary>
        public CardArrows Reversed => (CardArrows)(((Byte)arrows << 4) | ((Byte)arrows >> 4));
        public Int32 Count => BitOperations.PopCount((Byte)arrows);
        public Boolean Points(CardArrows direction) => (arrows & direction) != 0;
    }
    
    /// <summary>The grid step for one direction. (mirror: CardArrow.ToOffset).</summary>
    public static (Int32 X, Int32 Y) Offset(CardArrows direction) => direction switch
    {
        CardArrows.Up        => (0, -1),
        CardArrows.UpRight   => (1, -1),
        CardArrows.Right     => (1, 0),
        CardArrows.DownRight => (1, 1),
        CardArrows.Down      => (0, 1),
        CardArrows.DownLeft  => (-1, 1),
        CardArrows.Left      => (-1, 0),
        CardArrows.UpLeft    => (-1, -1),
        _                    => (0, 0)
    };

    public static String Glyphs(CardArrows arrows) => String.Concat(
        Ring.Where(d => arrows.Points(d)).Select(d => d switch
        {
            CardArrows.Up        => '↑',
            CardArrows.UpRight   => '↗',
            CardArrows.Right     => '→',
            CardArrows.DownRight => '↘',
            CardArrows.Down      => '↓',
            CardArrows.DownLeft  => '↙',
            CardArrows.Left      => '←',
            _                    => '↖'
        }));

    /// <summary>What happens along one edge (mirror: CardArrow.CheckDirection). The
    /// defender's arrows are REVERSED before testing.</summary>
    public static ArrowClash Clash(CardArrows attacker, CardArrows defender, CardArrows direction)
        => (attacker.Points(direction), defender.Reversed.Points(direction)) switch
        {
            (true, true) => ArrowClash.Battle,
            (true, false) => ArrowClash.Flip,
            (false, true) => ArrowClash.Blocked,
            _ => ArrowClash.None
        };

    /// <summary>Roll a newly minted card's arrows (mirror: CardPool.RandomCardArrow):
    /// draw a COUNT from the fixed distribution, then pick that many directions out of
    /// a shuffled ring.</summary>
    public static CardArrows Roll(Random rng)
    {
        var roll = rng.Next(0, 100);
        Int32 count = 0, running = 0;
        while (count < ArrowCountOdds.Length && roll >= running + ArrowCountOdds[count])
        {
            running += ArrowCountOdds[count];
            count++;
        }

        var ring = (CardArrows[])Ring.Clone();
        for (var i = 0; i < ring.Length; i++)
        {
            var j = rng.Next(0, 8);
            (ring[i], ring[j]) = (ring[j], ring[i]);
        }

        var arrows = CardArrows.None;
        for (var i = 0; i < count; i++)
            arrows |= ring[i];
        return arrows;
    }
}

