using Alexandria.Cards;
using Alexandria.Formats;

namespace ff9tool;

public sealed class CardCensusCommand
{
    public static void Run()
    {
        var cards = CardPool.Load(FF9Data.TripleTriadCsv, FF9Data.CardTitles);
        Console.WriteLine($"{cards.Count} cards, {cards.Select(c => c.Icon).Distinct().Count()} categories\n");

        foreach (var group in cards.GroupBy(c=>c.Icon).OrderBy(g=>g.Key))
        {
            Console.WriteLine($"{group.Key} ({group.Count()})");
            foreach (var card in group.OrderByDescending(c => c.Attack).ThenBy(c => c.ID))
                Console.WriteLine($"  {card.Face}  {card.Name}");
        }
    }
}

