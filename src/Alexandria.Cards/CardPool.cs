using Alexandria.Formats;
using Alexandria.Rules;

namespace Alexandria.Cards;

public static class CardPool
{
    public const Int32 TotalCards = 100;

    public static IReadOnlyList<TetraCard> Load(String triadCsv, String cardTitles)
    {
        var names = StringsTable.Load(cardTitles);
        return
        [
            .. MemoriaCSV.ReadRows(triadCsv).Select(f =>
            {
                var id = Int32.Parse(f[1]);
                return new TetraCard(
                    id,
                    names.GetValueOrDefault(id, f[0]),
                    Band(f[2]),
                    CardType.Physical,
                    Band(f[5]),
                    Band(f[3]),
                    Enum.Parse<CardIcon>(f[6], ignoreCase: true));
            })
        ];
    }

    private static Byte Band(String shown) => (Byte)((Byte.Parse(shown) << 4) | 0x0F);
}

