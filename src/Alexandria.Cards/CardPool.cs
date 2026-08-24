using Alexandria.Formats;
using Alexandria.Rules;

namespace Alexandria.Cards;

public static class CardPool
{
    public const Int32 TotalCards = 100;
    private const Int32 BytesPerCard = 5;

    public static IReadOnlyList<TetraCard> Load(String cardData, String cardTitles, String triadCsv)
    {
        var bytes = File.ReadAllBytes(cardData);
        var names = StringsTable.Load(cardTitles);
        var icons = MemoriaCSV.ReadRows(triadCsv)
            .ToDictionary(f => Int32.Parse(f[1]), f => Enum.Parse<CardIcon>(f[6], ignoreCase: true));
        return
        [
            .. Enumerable.Range(0, TotalCards).Select(id =>
            {
                var at = id * BytesPerCard;
                return new TetraCard(
                    id,
                    names.GetValueOrDefault(id, $"card {id}"),
                    bytes[at],
                    (CardType)bytes[at + 1],
                    bytes[at + 2],
                    bytes[at + 3],
                    icons.GetValueOrDefault(id, CardIcon.Mystery));
            })
        ];
    }
}

