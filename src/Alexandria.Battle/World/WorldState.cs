using System.Text.Json;
using System.Text.Json.Serialization;
using Alexandria.Formats.Scripts;
using Alexandria.Rules;

namespace Alexandria.Battle;

public sealed class WorldState
{
    public EbState Scripts { get; } = new();
    
    public Int32 Frogs { get; set; }
    public KillCounts Kills { get; } = new();

    public HashSet<String> SummonsSeen { get; } = [];
    
    public Inventory Items { get; } = new();
    
    public Int32 Gil
    {
        get;
        set => field = Math.Clamp(value, 0, 9_999_999);
    }

    public Int32 Thefts
    {
        get;
        set => field = Math.Clamp(value, 0, 9999);
    }

    public Int32 Tonberries
    {
        get => Scripts.Global[192];
        set => Scripts.Global[192] = (Byte)Math.Clamp(value, 0, 255);
    }

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Reads a save, or hands back a new game if there isn't one yet.</summary>
    public static WorldState Load(String path)
    {
        var world = new WorldState();
        if (!File.Exists(path))
            return world;

        var saved = JsonSerializer.Deserialize<SaveFile>(File.ReadAllText(path), Format) ?? new SaveFile();
        world.Gil = saved.Gil;
        world.Frogs = saved.Frogs;
        world.Thefts = saved.Thefts;

        if (saved.Global is not null)
            saved.Global.AsSpan(0, Math.Min(saved.Global.Length, world.Scripts.Global.Length))
                .CopyTo(world.Scripts.Global);

        foreach (var tally in saved.Kills)
            if (tally.Category != EnemyCategory.None && Enum.IsDefined(tally.Category))
                world.Kills[tally.Category] = tally.Count;

        foreach (var name in saved.SummonsSeen)
            world.SummonsSeen.Add(name);

        foreach (var entry in saved.Items)
            world.Items.Add(entry.Item, entry.Count);

        return world;
    }

    /// <summary>Writes what a save holds (mirror: JsonParser.cs:579 and :927-943 — the event
    /// blob base64'd, gil, frog_no, steal_no and the eight category tallies).</summary>
    public void Save(String path)
    {
        var file = new SaveFile
        {
            Gil = Gil,
            Frogs = Frogs,
            Thefts = Thefts,
            Global = Scripts.Global,
            Kills =
            [
                .. Enum.GetValues<EnemyCategory>()
                    .Where(c => c != EnemyCategory.None)
                    .Select(c => new KillRecord(c, Kills[c]))
            ],
            SummonsSeen = [.. SummonsSeen.Order()],
            Items = [.. Items.Contents.Select(i => new ItemRecord(i.Key, i.Value))]
        };
        File.WriteAllText(path, JsonSerializer.Serialize(file, Format));
    }

    private sealed record KillRecord(EnemyCategory Category, Int16 Count);
    private sealed record ItemRecord(ItemID Item, Int32 Count);
    
    private sealed class SaveFile
    {
        public Int32 Gil { get; set; }
        public Int32 Frogs { get; set; }
        public Int32 Thefts { get; set; }
        public Byte[]? Global { get; set; }
        public List<KillRecord> Kills { get; set; } = [];
        public List<ItemRecord> Items { get; set; } = [];
        public List<String> SummonsSeen { get; set; } = [];
    }
}

