using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace Alexandria.Cards;

public sealed class CardAlbum
{
    public List<TetraCard> Cards { get; init; } = [];
    public Int32 Wins { get; set; }
    public Int32 Losses { get; set; }
    public Int32 Draws { get; set; }

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Reads an album, rehydrating each card's name and icon from the pool.</summary>
    public static CardAlbum Load(String path, IReadOnlyList<TetraCard> pool)
    {
        if (!File.Exists(path))
            return new CardAlbum();

        var saved = JsonSerializer.Deserialize<AlbumFile>(File.ReadAllText(path), Format) ?? new AlbumFile();
        return new CardAlbum
        {
            Wins = saved.Wins,
            Losses = saved.Losses,
            Draws = saved.Draws,
            Cards =
            [
                .. saved.Cards.Where(c => c.ID >= 0 && c.ID < pool.Count)
                    .Select(c => pool[c.ID] with
                    {
                        Attack = c.Atk,
                        Type = c.Type,
                        PhysicalDefense = c.PDef,
                        MagicalDefense = c.MDef,
                        Arrows = (CardArrows)c.arrows
                    })
            ]
        };
    }

    public void Save(String path)
    {
        var file = new AlbumFile
        {
            Wins = Wins,
            Losses = Losses,
            Draws = Draws,
            Cards =
            [
                .. Cards.Select(c => new CardRecord(
                    c.ID, c.Attack, c.Type, c.PhysicalDefense, c.MagicalDefense, (Byte)c.Arrows))
            ]
        };
        File.WriteAllText(path, JsonSerializer.Serialize(file, Format));
    }

    private sealed record CardRecord(Int32 ID, Byte Atk, CardType Type, Byte PDef, Byte MDef, Byte arrows);

    private sealed class AlbumFile
    {
        public Int32 Wins { get; set; }
        public Int32 Losses { get; set; }
        public Int32 Draws { get; set; }
        public List<CardRecord> Cards { get; set; } = [];
    }
}

