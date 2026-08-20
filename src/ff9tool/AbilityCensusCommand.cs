using Alexandria.Battle;
using Alexandria.Formats.Memoria;
using Alexandria.Formats.Scenes.BattleScene;
using Alexandria.Rules;

namespace ff9tool;

/// <summary>Which ability scripts the game's rosters actually need.</summary>
public static class AbilityCensusCommand
{
    private const String SceneRoot = @"C:\Dev\Memoria\FF9Data\BattleMap\BattleScene";

    public static void Run()
    {
        var uses = new Dictionary<AbilityScript, Int32>();
        var examples = new Dictionary<AbilityScript, String>();
        Int32 scenes = 0, attacks = 0, resolved = 0;

        foreach (var dir in Directory.EnumerateDirectories(SceneRoot))
        {
            var name = Path.GetFileName(dir);
            using var raw = new BinaryReader(File.OpenRead(Path.Combine(dir, "dbfile0000.raw16")));
            var scene = BattleScene.Read(raw);
            var rosters = MemoriaJsonParser.ReadPlayableSequences(Path.Combine(dir, "Scripts.json"));
            var seqNames = Directory.EnumerateFiles(dir, "*.seq")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToDictionary(s => Int32.Parse(s[..2]), s => s[3..]);
            scenes++;

            foreach (var roster in rosters.Values)
            foreach (var n in roster.Where(n => n < scene.Attacks.Count))
            {
                var script = (AbilityScript)scene.Attacks[n].ScriptID;
                attacks++;
                if (BattleEngine.ResolvableScripts.Contains(script))
                    resolved++;
                uses[script] = uses.GetValueOrDefault(script) + 1;
                examples.TryAdd(script, $"{seqNames.GetValueOrDefault(n, $"attack {n}")} — {name["EVT_BATTLE_".Length..]}");
            }
        }

        Console.WriteLine($"{scenes} scenes, {attacks} roster attacks");
        Console.WriteLine($"{resolved}/{attacks} ({100.0 * resolved / attacks:F1}%) resolve with an implemented script\n");
        Console.WriteLine("script usage, most-used first:");
        foreach (var (script, count) in uses.OrderByDescending(u => u.Value))
            Console.WriteLine(
                $"  {count,5}× {(Int32)script,3} " +
                $"{(Enum.IsDefined(script) ? script.ToString() : "(no script — choreography)"),-30} " +
                $"{(BattleEngine.ResolvableScripts.Contains(script) ? "✓" : " ")}  " +
                $"e.g. {examples[script]}");
    }
}
