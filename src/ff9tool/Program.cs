using System.Text.Json;
using System.Text.RegularExpressions;
using Alexandria.Formats.Scenes.BattleScene;

var path = args[0];


if (File.Exists(path))
{
    using var reader = new BinaryReader(File.OpenRead(path));
    var battleScene = BattleScene.Read(reader);
    Console.WriteLine(battleScene);

    foreach (var formation in battleScene.Formations)
        Console.WriteLine(formation);
    foreach (var monster in battleScene.Monsters)
        Console.WriteLine(monster);
}
else
{
    List<MonsterInfo> loaded = [];
    List<String> failures = [];
    foreach (var dir in Directory.EnumerateDirectories(path, "EVT_BATTLE_*"))
    {
        var dbFilePath = Path.Combine(dir, "dbfile0000.raw16");
        var enemiesFilePath = Path.Combine(dir, "Enemies.json");
        try
        {
            using var reader = new BinaryReader(File.OpenRead(dbFilePath));
            var battleScene = BattleScene.Read(reader);
            var enemiesRaw = File.ReadAllText(enemiesFilePath);
            var nameMatches = Regex.Matches(
                enemiesRaw, @"""name"": ""\[STRT=[^\]]*\](.*?)""");
            for (var i = 0; i < nameMatches.Count; i++)
            {
                loaded.Add(new MonsterInfo
                {
                    DisplayName = nameMatches[i].Groups[1].Value,
                    MaxHP = battleScene.Monsters[i].MaxHP,
                    SceneFolder = dir
                });
            }
        }
        catch (Exception ex)
        {
            failures.Add($"{Path.GetFileName(dbFilePath)}: {ex.Message}");
        }
    }

    loaded = [.. loaded.OrderByDescending(m => m.MaxHP)];
    var final = loaded.Take(10).ToList();
    Console.WriteLine($"{loaded.Count} loaded; {failures.Count} failures.");
    foreach (var monster in final)
        Console.WriteLine(monster);
}

public sealed record MonsterInfo
{
    public required String DisplayName;
    public required UInt16 MaxHP;
    public required String SceneFolder;
}

