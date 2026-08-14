using Alexandria.Formats.Scenes.BattleScene;

var path = args[0];
using var reader = new BinaryReader(File.OpenRead(path));
var battleScene = BattleScene.Read(reader);
Console.WriteLine(battleScene);

foreach (var formation in battleScene.Formations)
    Console.WriteLine(formation);
foreach (var monster in battleScene.Monsters)
    Console.WriteLine(monster);

