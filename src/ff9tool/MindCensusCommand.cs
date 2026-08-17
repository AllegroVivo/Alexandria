using System.Diagnostics;
using Alexandria.Battle;
using Alexandria.Formats.Scenes.BattleScene;
using Alexandria.Formats.Scripts;

namespace ff9tool;

/// <summary>The disassembler census's dynamic sibling: every entry with an atb
/// function gets an EbVM over the production EngineHost and takes four turns.
/// Whatever throws lands on the wishlist.</summary>
public static class MindCensusCommand
{
    private const String SceneRoot = @"C:\Dev\Memoria\FF9Data\BattleMap\BattleScene";

    public static void Run()
    {
        Int32 scenes = 0, minds = 0, clean = 0, reactions = 0, reactionsClean = 0;
        var wishlist = new Dictionary<String, Int32>();
        var examples = new Dictionary<String, String>();

        foreach (var dir in Directory.EnumerateDirectories(SceneRoot))
        {
            var name = Path.GetFileName(dir);
            using var eb = new BinaryReader(File.OpenRead(Path.Combine(dir, name + ".eb")));
            var script = EbScript.Read(eb);
            using var raw = new BinaryReader(File.OpenRead(Path.Combine(dir, "dbfile0000.raw16")));
            var scene = BattleScene.Read(raw);
            scenes++;

            var enemies = scene.Monsters
                .Select((m, i) => BattleUnit.FromEnemy($"M{i}", m))
                .ToList();
            if (enemies.Count == 0)
                continue;
            var party = Enumerable.Range(0, 4).Select(SyntheticHero).ToList();

            foreach (var entry in script.Entries)
            {
                if(entry.Functions.All(f => f.Tag != 5))
                    continue;
                minds++;

                var rng = new Random(entry.Index * 1000 + scenes);
                var host = new EngineHost(party, enemies, rng);
                var selfLine = 4 + Math.Min(Math.Max(entry.Index - 1, 0), enemies.Count - 1);
                var vm = new EbVM(entry, host);
                try
                {
                    host.BeginSlice(selfLine);
                    if (entry.Functions.Any(f => f.Tag == 0))
                        vm.RunFunction(0);
                    for (var turn = 0; turn < 4; turn++)
                    {
                        host.BeginSlice(selfLine);
                        host.ChosenAttack = null;
                        vm.RunFunction(5);
                    }
                    clean++;
                }
                catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
                {
                    wishlist[ex.Message] = wishlist.GetValueOrDefault(ex.Message) + 1;
                    examples.TryAdd(ex.Message, $"{name}[{entry.Index}]");
                }

                foreach (var tag in new UInt16[] { 6, 9 })
                {
                    if (entry.Functions.All(f => f.Tag != tag))
                        continue;
                    reactions++;
                    try
                    {
                        host.BeginSlice(selfLine);
                        host.SetSysList(0, 0b0001); // SV_0 = the provoking attacker (player line 0)
                        host.SeedReaction(1, 0);
                        vm.RunFunction(tag);
                        reactionsClean++;
                    }
                    catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
                    {
                        wishlist[ex.Message] = wishlist.GetValueOrDefault(ex.Message) + 1;
                        examples.TryAdd(ex.Message, $"{name}[{entry.Index}] tag{tag}");
                    }
                }
            }
        }
        
        Console.WriteLine($"{scenes} scenes, {minds} minds (entries with an atb function)");
        Console.WriteLine($"{clean}/{minds} ran init + four turns clean");
        Console.WriteLine($"{reactionsClean}/{reactions} reaction functions (counter/death) ran clean");
        Console.WriteLine("wishlist (blocked):");
        foreach (var (message, count) in wishlist.OrderByDescending(w => w.Value).Take(20))
            Console.WriteLine($"  {count,4}× {message}  e.g. {examples[message]}");
    }

    private static BattleUnit SyntheticHero(Int32 i) => new()
    {
        Name = $"P{i}", Level = 30, MaxHP = 1500, MaxMP = 100, CurrentHP = 1500, CurrentMP = 100,
        Speed = 25, Strength = 30, Magic = 25, Spirit = 30,
        PhysicalDefense = 15, PhysicalEvade = 8, MagicalDefense = 12, MagicalEvade = 6
    };
}

