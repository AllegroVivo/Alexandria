using Alexandria.Battle;
using Alexandria.Formats.Memoria;
using Alexandria.Formats.Scenes.BattleScene;
using Alexandria.Rules;

namespace ff9tool;

public static class BattleCommand
{
    private const String DefaultScene = @"C:\Dev\Memoria\FF9Data\BattleMap\BattleScene\EVT_BATTLE_AC_E028F";
    private const String DataRoot = @"C:\Dev\Memoria\FF9Data\Data";

    public static void Run(String[] args)
    {
        var sceneDir = args.Length > 0 ? args[0] : DefaultScene;

        using var reader = new BinaryReader(File.OpenRead(Path.Combine(sceneDir, "dbfile0000.raw16")));
        var scene = BattleScene.Read(reader);
        var names = MemoriaJsonParser.ReadEnemies(Path.Combine(sceneDir, "Enemies.json"));

        var enemies = scene.Monsters
            .Select((m, i) =>
            {
                var unit = BattleUnit.FromEnemy(names[i].DisplayName, m);
                unit.AttackPower = 10;
                return unit;
            })
            .ToList();

        var zidane = BuildZidane(enemies.Max(e => e.Level));
        
        Console.WriteLine($"— {String.Join(" and ", enemies.Select(e => e.Name))} appeared! —");

        var engine = new BattleEngine([zidane], enemies, EncounterKind.Normal, new Random());
        while (engine.TickUntilReady() is { } ready)
        {
            if (engine.IsEnemy(ready))
                Narrate(engine.ExecuteEnemyTurn(ready));
            else
                Narrate(engine.ExecuteAttack(ready, PromptTarget(engine)));
        }
        
        Console.WriteLine(engine.Victory ? "\n*** VICTORY! ***" : "\n*** The party has fallen... ***");
    }

    private static BattleUnit BuildZidane(Int32 level)
    {
        var baseStats = CharacterBaseStats.Load(Path.Combine(DataRoot, @"Characters\BaseStats.csv"))
            .First(c => c.Name == "Zidane");
        var table = LevelingTable.Load(Path.Combine(DataRoot, @"Characters\Leveling.csv"));
        var grown = GrowthSimulator.LevelUpTo(level, GrowthBonuses.None);
        var sheet = StatGrowth.AtLevel(baseStats, table, level, grown);

        return new BattleUnit
        {
            Name = "Zidane", Level = level,
            MaxHP = sheet.MaxHP, CurrentHP = sheet.MaxHP,
            MaxMP = sheet.MaxMP, CurrentMP = sheet.MaxMP,
            Speed = sheet.Speed, Strength = sheet.Strength,
            Magic = sheet.Magic, Spirit = sheet.Spirit,
            // modest starter kit until equipment wiring: Dagger-ish and leathers
            PhysicalDefense = 6, PhysicalEvade = 5,
            MagicalDefense = 6, MagicalEvade = 3,
            AttackPower = 12
        };
    }

    private static BattleUnit PromptTarget(BattleEngine engine)
    {
        var z = engine.Players[0];
        Console.WriteLine($"\n  Zidane  HP {z.CurrentHP}/{z.MaxHP}");
        var living = engine.Enemies.Where(e => e.IsAlive).ToList();
        for(var i = 0;i < living.Count;i++)
            Console.WriteLine($"  [{i + 1}] {living[i].Name}  HP {living[i].CurrentHP}/{living[i].MaxHP}");

        while (true)
        {
            Console.Write("Attack which target? ");
            if (Int32.TryParse(Console.ReadLine(), out var pick) && pick >= 1 && pick <= living.Count)
                return living[pick - 1];
        }
    }

    private static void Narrate(BattleEvent e)
    {
        var line = e.Result.Outcome switch
        {
            AttackOutcome.Evaded => $"{e.Actor.Name} attacks {e.Target.Name} — evaded!",
            AttackOutcome.CriticalHit => $"{e.Actor.Name} lands a CRITICAL on {e.Target.Name} for {e.Result.Damage}!",
            _ => $"{e.Actor.Name} hits {e.Target.Name} for {e.Result.Damage}."
        };
        Console.WriteLine(line + (e.Target.IsAlive ? "" : $"  {e.Target.Name} falls!"));
    }
}

