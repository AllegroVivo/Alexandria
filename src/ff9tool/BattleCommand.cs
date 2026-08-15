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
        var db = RulesDB.Load(DataRoot);

        using var reader = new BinaryReader(File.OpenRead(Path.Combine(sceneDir, "dbfile0000.raw16")));
        var scene = BattleScene.Read(reader);
        var names = MemoriaJsonParser.ReadEnemies(Path.Combine(sceneDir, "Enemies.json"));

        var enemies = scene.Monsters
            .Select((m, i) =>
            {
                var unit = BattleUnit.FromEnemy(names[i].DisplayName, m);
                unit.AttackPower = scene.Attacks[names[i].DefaultAttack].Power;
                return unit;
            })
            .ToList();

        var level = enemies.Max(e => e.Level);
        var party = new List<BattleUnit>
        {
            BuildCharacter(db, "Zidane", level),
            BuildCharacter(db, "Steiner", level),
            BuildCharacter(db, "Vivi", level)
        };
        var spellbook = new Dictionary<String, List<BattleAction>>
        {
            ["Vivi"] =
            [
                .. CharacterAbilities
                    .Load(Path.Combine(DataRoot, @"Characters\Abilities\Vivi.csv"))
                    .Where(a => a.Ability.Kind == AbilityKind.Active)
                    .Select(a => db.Actions[a.Ability.ID])
                    .Where(s => s.ScriptID is 9 or 11)
            ]
        };
        
        Console.WriteLine($"— {String.Join(" and ", enemies.Select(e => e.Name))} appeared! —");

        var engine = new BattleEngine(party, enemies, EncounterKind.Normal, new Random());
        while (engine.TickUntilReady() is { } ready)
        {
            if (engine.IsEnemy(ready))
                Narrate(engine.ExecuteEnemyTurn(ready));
            else
                Narrate(TakePlayerTurn(db, engine, ready, spellbook));
        }
        
        Console.WriteLine(engine.Victory ? "\n*** VICTORY! ***" : "\n*** The party has fallen... ***");
    }

    private static BattleUnit BuildCharacter(RulesDB db, String name, Int32 level)
    {
        var baseStats = db.BaseStats.First(c => c.Name == name);
        var kit = EquipmentKit.Resolve(db.DefaultEquipment.First(e => e.Name == name), 
            db.Items, db.Weapons, db.Armors, db.GrowthStats);
        var grown = GrowthSimulator.LevelUpTo(level, GrowthBonuses.None, kit.GrowthPerLevel);
        var sheet = StatGrowth.AtLevel(baseStats, db.Leveling, level, grown);

        return new BattleUnit
        {
            Name = name, Level = level,
            MaxHP = sheet.MaxHP, CurrentHP = sheet.MaxHP,
            MaxMP = sheet.MaxMP, CurrentMP = sheet.MaxMP,
            Speed = sheet.Speed, Strength = sheet.Strength,
            Magic = sheet.Magic, Spirit = sheet.Spirit,
            PhysicalDefense = kit.PhysicalDefense, PhysicalEvade = kit.PhysicalEvade,
            MagicalDefense = kit.MagicalDefense, MagicalEvade = kit.MagicalEvade,
            AttackPower = kit.WeaponPower
        };
    }

    private static void Narrate(BattleEvent e)
    {
        var line = e.Result.Outcome switch
        {
            AttackOutcome.Miss => $"{e.Actor.Name}'s attack misses {e.Target.Name}!",
            AttackOutcome.Evaded => $"{e.Actor.Name} attacks {e.Target.Name} — evaded!",
            AttackOutcome.CriticalHit => $"{e.Actor.Name} lands a CRITICAL on {e.Target.Name} for {e.Result.Damage}!",
            AttackOutcome.Hit when e.Result.Damage == 0 => $"{e.Target.Name} succumbs!",
            _ => $"{e.Actor.Name} hits {e.Target.Name} for {e.Result.Damage}."
        };
        Console.WriteLine(line + (e.Target.IsAlive ? "" : $"  {e.Target.Name} falls!"));
    }

    private static BattleEvent TakePlayerTurn(RulesDB db, BattleEngine engine,
        BattleUnit actor, Dictionary<String, List<BattleAction>> spellbook)
    {
        Console.WriteLine();
        foreach (var p in engine.Players)
            Console.WriteLine($"  {p.Name,-8} HP {p.CurrentHP}/{p.MaxHP}  MP {p.CurrentMP}/{p.MaxMP}{(p == actor ? "   ◄ ready" : "")}");

        var castable = (spellbook.GetValueOrDefault(actor.Name) ?? [])
            .Where(s => s.MP <= actor.CurrentMP).ToList();

        if (castable.Count > 0)
        {
            Console.Write($"{actor.Name}: [a]ttack or [c]ast? ");
            if ((Console.ReadLine() ?? "").Trim().StartsWith("c", StringComparison.OrdinalIgnoreCase))
            {
                for (var i = 0; i < castable.Count; i++)
                    Console.WriteLine($"  [{i + 1}] {castable[i].Name}  (MP {castable[i].MP})");
                
                var spell = Prompt(castable, s => s.Name, $"{actor.Name} casts which spell? ");

                var target = PromptEnemy(engine, $"On which target? ");
                Console.WriteLine($"{actor.Name} casts {spell.Name}!");
                return spell.ScriptID == 11
                    ? engine.ExecuteStatusSpell(actor, target,
                        db.StatusSets[spell.StatusIndex].StatusMask, spell.Rate, spell.MP)
                    : engine.ExecuteSpell(actor, target, spell.Power, spell.MP);
            }
        }
        return engine.ExecuteAttack(actor, PromptEnemy(engine, $"{actor.Name} attacks which target? "));
    }

    private static T Prompt<T>(List<T> options, Func<T, String> label, String question)
    {
        while (true)
        {
            Console.Write(question);
            if (Int32.TryParse(Console.ReadLine(), out var pick) && pick >= 1 && pick <= options.Count)             
                return options[pick - 1];
        }
    }

    private static BattleUnit PromptEnemy(BattleEngine engine, String question)
    {
        var living = engine.Enemies.Where(e => e.IsAlive).ToList();
        for (var i = 0; i < living.Count; i++)
            Console.WriteLine($"  [{i + 1}] {living[i].Name}  HP {living[i].CurrentHP}/{living[i].MaxHP}");
        return Prompt(living, e => e.Name, question);
    }
}

