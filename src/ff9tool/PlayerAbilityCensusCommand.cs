using Alexandria.Battle;
using Alexandria.Formats;
using Alexandria.Rules;

namespace ff9tool;

public static class PlayerAbilityCensusCommand
{
    private const String DataRoot = @"C:\Dev\Memoria\FF9Data\Data";

    public static void Run()
    {
        var db = RulesDB.Load(FF9Data.Data);
        var uses = new Dictionary<AbilityScript, Int32>();
        var examples = new Dictionary<AbilityScript, String>();
        var distinct = new HashSet<Int32>();
        Int32 pairs = 0, resolved = 0;

        foreach (var (character, learnables) in db.Learnables.OrderBy(l => l.Key))
        foreach (var learnable in learnables.Where(l => l.Ability.Kind == AbilityKind.Active))
        {
            if (!db.Actions.TryGetValue(learnable.Ability.ID, out var action))
                continue;
            pairs++;
            distinct.Add(action.ID);
            if (BattleEngine.CanResolve(action.Script))
                resolved++;
            uses[action.Script] = uses.GetValueOrDefault(action.Script) + 1;
            examples.TryAdd(action.Script, $"{action.Name} — {character}");
        }
        
        Console.WriteLine($"{db.Learnables.Count} characters, {pairs} learnable actives " +
                          $"({distinct.Count} distinct abilities)");
        Console.WriteLine($"{resolved}/{pairs} ({100.0 * resolved / pairs:F1}%) resolve with an implemented script\n");
        Console.WriteLine("script usage, most-used first:");
        foreach (var (script, count) in uses.OrderByDescending(u => u.Value))
        {
            var label = Enum.IsDefined(script) ? script.ToString() : "(no script)";
            Console.WriteLine(
                $"  {count,4}× {(Int32)script,3} {label,-30} " +
                $"{(BattleEngine.CanResolve(script) ? "✓" : " ")}  " +
                $"e.g. {examples[script]}");
        }
    }
}

