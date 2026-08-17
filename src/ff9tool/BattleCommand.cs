using Alexandria.Battle;
using Alexandria.Formats.Memoria;
using Alexandria.Formats.Scenes.BattleScene;
using Alexandria.Rules;
using Alexandria.Formats.Scripts;

namespace ff9tool;

public static class BattleCommand
{
    private const String DefaultScene = @"C:\Dev\Memoria\FF9Data\BattleMap\BattleScene\EVT_BATTLE_AC_E028F";
    private const String DataRoot = @"C:\Dev\Memoria\FF9Data\Data";

    public static void Run(String[] args)
    {
        var sceneDir = args.Length > 0 ? args[0] : DefaultScene;
        var db = RulesDB.Load(DataRoot);
        var rng = new Random();

        using var reader = new BinaryReader(File.OpenRead(Path.Combine(sceneDir, "dbfile0000.raw16")));
        var scene = BattleScene.Read(reader);
        var names = MemoriaJsonParser.ReadEnemies(Path.Combine(sceneDir, "Enemies.json"));
        var rosters = MemoriaJsonParser.ReadPlayableSequences(Path.Combine(sceneDir, "Scripts.json"));
        var attackNames = Directory.EnumerateFiles(sceneDir, "*.seq")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToDictionary(s => Int32.Parse(s[..2]), s => s[3..]);

        var enemies = scene.Monsters
            .Select((m, i) =>
            {
                var unit = BattleUnit.FromEnemy(names[i].DisplayName, m);
                unit.AttackPower = scene.Attacks[names[i].DefaultAttack].Power;
                unit.Attacks.AddRange(rosters[i].Where(n => n < scene.Attacks.Count).Select(n =>
                {
                    var a = scene.Attacks[n];
                    return new EnemyAttack(
                        attackNames.GetValueOrDefault(n, $"Attack {n}"),
                        a.ScriptID, a.Power, (Element)a.Elements, a.Rate,
                        db.StatusSets[a.AddStatusNo].StatusMask, a.MP, n);
                }));
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
        var host = new EngineHost(party, enemies, rng);
        using (var eb = new BinaryReader(File.OpenRead(
                   Path.Combine(sceneDir, Path.GetFileName(sceneDir) + ".eb"))))
        {
            var script = EbScript.Read(eb);
            for (var i = 0; i < enemies.Count; i++)
                enemies[i].Brain = new ScriptedBrain(script.Entries[i + 1], host, selfLine: 4 + i);
        }

        var sceneIDS = SceneIDs.Load(@"C:\Dev\Memoria\FF9Data\SceneData.csv");
        var mesPath = Path.Combine(@"C:\Dev\Memoria\FF9Data\Text\Battle",
            sceneIDS[Path.GetFileName(sceneDir)["EVT_BATTLE_".Length..]] + ".mes");
        var battleText = File.Exists(mesPath) ? BattleText.Load(mesPath) : null;
        
        var spellbook = party.ToDictionary(
            p => p.Name,
            p => Spellbook.For(db, p.Name,
                    APEarned.LearnedActives(db.Learnables.GetValueOrDefault(p.Name, []), p.Level))
                .Where(s => s.ScriptID is 9 or 11).ToList());

        Console.WriteLine($"— {String.Join(" and ", enemies.Select(e => e.Name))} appeared! —");

        var statusTable = db.StatusData.Values
            .Where(s => s.Bit < 32)
            .ToDictionary(
                s => (BattleStatus)(1u << s.Bit),
                s => new StatusTiming(s.OprCount, s.ContiCount,
                    (BattleStatus)s.ClearOnApply, (BattleStatus)s.ImmunityProvided));

        var engine = new BattleEngine(party, enemies, EncounterKind.Normal, rng, statusTable: statusTable);
        while (engine.TickUntilReady() is { } ready)
        {
            NarrateTicks(engine);

            if (engine.IsEnemy(ready))
            {
                Narrate(engine.ExecuteEnemyTurn(ready));
                foreach (var id in host.PendingDialogs)
                    Console.WriteLine($"  『{(battleText is not null && id < battleText.Count ? battleText[id] : $"battle text {id}")}』");
                host.PendingDialogs.Clear();
            }
            else
                Narrate(TakePlayerTurn(db, engine, ready, spellbook));

            foreach (var reaction in engine.PendingReactions)
            {
                Console.WriteLine("  — counterattack! —");
                Narrate(reaction);
            }
            engine.PendingReactions.Clear();
        }
        NarrateTicks(engine);

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
            AttackPower = kit.WeaponPower, WeaponElement = (Element)kit.WeaponElement,
            BonusElements = (Element)kit.BonusElements, GuardElements = (Element)kit.GuardElements,
            AbsorbElements = (Element)kit.AbsorbElements, HalfElements = (Element)kit.HalfElements,
            WeakElements = (Element)kit.WeakElements
        };
    }

    private static void Narrate(BattleEvent e)
    {
        if (e.Attack is not null)
            Console.WriteLine($"{e.Actor.Name} {(e.Attack.ScriptID == 9 ? "casts" : "uses")} {e.Attack.Name}!");

        var line = e.Result.Outcome switch
        {
            AttackOutcome.Miss => $"{e.Actor.Name}'s attack misses {e.Target.Name}!",
            AttackOutcome.Evaded => $"{e.Actor.Name} attacks {e.Target.Name} — evaded!",
            AttackOutcome.CriticalHit => $"{e.Actor.Name} lands a CRITICAL on {e.Target.Name} for {e.Result.Damage}!",
            AttackOutcome.Guarded => $"{e.Target.Name} nulls it — no effect!",
            AttackOutcome.Absorbed => $"{e.Target.Name} drinks it in — absorbs {e.Result.Damage} HP!",
            AttackOutcome.Hit when e.Result.Damage == 0 => $"{e.Target.Name} succumbs!",
            _ => $"{e.Actor.Name} hits {e.Target.Name} for {e.Result.Damage}."
        };
        if (e.StatusesAdded != 0 && e.Result.Damage > 0)
            line += $"  {e.Target.Name} is afflicted with {e.StatusesAdded}!";
        Console.WriteLine(line + (e.Target.IsAlive ? "" : $"  {e.Target.Name} falls!"));
    }

    private static void NarrateTicks(BattleEngine engine)
    {
        foreach (var tick in engine.PendingStatusEvents)
        {
            Console.WriteLine(tick switch
            {
                { Expired: true } => $"{tick.Unit.Name}'s {tick.Status} wears off.",
                { HPDelta: < 0 } =>
                    $"{tick.Unit.Name} takes {-tick.HPDelta} poison damage!{(tick.Unit.IsAlive ? "" : $"  {tick.Unit.Name} falls!")}",
                _ => $"{tick.Unit.Name} regenerates {tick.HPDelta} HP."
            });
        }
        engine.PendingStatusEvents.Clear();
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
                    : engine.ExecuteSpell(actor, target, spell.Power, spell.MP,
                        db.StatusSets[spell.StatusIndex].StatusMask, spell.Rate,
                        (Element)spell.Elements);
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
