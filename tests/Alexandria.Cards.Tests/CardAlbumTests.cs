using Alexandria.Cards;

namespace Alexandria.Cards.Tests;

public class CardAlbumTests
{
    private static List<TetraCard> Pool() =>
    [
        .. Enumerable.Range(0, 3).Select(id =>
            new TetraCard(id, $"Card {id}", 0x80, CardType.Physical, 0x80, 0x80, CardIcon.Summon))
    ];

    [Fact]
    public void A_missing_album_is_empty_rather_than_an_error()
        => Assert.Empty(CardAlbum.Load(Path.Combine(Path.GetTempPath(), "no-such-album.json"), Pool()).Cards);

    [Fact]
    public void Levelled_stats_survive_a_round_trip_but_names_come_from_the_pool()
    {
        var pool = Pool();
        var path = Path.Combine(Path.GetTempPath(), $"album-{Guid.NewGuid():N}.json");
        try
        {
            var album = new CardAlbum { Wins = 3, Losses = 1, Draws = 2 };
            album.Cards.Add(pool[1] with
            {
                Name = "junk",                       // deliberately wrong — the pool must win
                Attack = 0x5F,
                Type = CardType.Assault,
                Arrows = CardArrows.Up | CardArrows.DownLeft
            });
            album.Save(path);

            var loaded = CardAlbum.Load(path, pool);

            Assert.Equal(3, loaded.Wins);
            Assert.Equal(0x5F, loaded.Cards[0].Attack);
            Assert.Equal(CardType.Assault, loaded.Cards[0].Type);
            Assert.Equal(CardArrows.Up | CardArrows.DownLeft, loaded.Cards[0].Arrows);
            Assert.Equal("Card 1", loaded.Cards[0].Name);      // rehydrated, not stored
            Assert.Equal(CardIcon.Summon, loaded.Cards[0].Icon);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
