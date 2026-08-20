using Alexandria.Battle;
using Alexandria.Formats;
using Alexandria.Formats.Memoria;
using Alexandria.Formats.Scenes.BattleScene;
using Alexandria.Rules;
using Alexandria.Formats.Scripts;

namespace ff9tool;

public static class BattleCommand
{
    private static readonly String DefaultScene = FF9Data.Scene("EVT_BATTLE_AC_E028F");
    private static readonly HashSet<AbilityScript> Spells =
    [
        AbilityScript.MagicAttack, AbilityScript.MagicRecovery, AbilityScript.MagicCureStatus,
        AbilityScript.MagicApplyNegativeStatus, AbilityScript.MagicApplyPositiveStatus,
        AbilityScript.MagicGravityDamage, AbilityScript.Revive, AbilityScript.Death,
        AbilityScript.DrainHp, AbilityScript.DrainMp, AbilityScript.Meteorite,
        AbilityScript.FlareStar, AbilityScript.LvHoly, AbilityScript.Mini, AbilityScript.Summon
    ];

    public static void Run(String[] args)
    {
        var sceneDir = args.Length > 0 ? args[0] : DefaultScene;
        var db = RulesDB.Load(FF9Data.Data);
        var rng = new Random();

        using var reader = new BinaryReader(File.OpenRead(Path.Combine(sceneDir, "dbfile0000.raw16")));
        var scene = BattleScene.Read(reader);
        var names = MemoriaJsonParser.ReadEnemies(Path.Combine(sceneDir, "Enemies.json"));
        var rosters = MemoriaJsonParser.ReadPlayableSequences(Path.Combine(sceneDir, "Scripts.json"));
        var attackNames = Directory.EnumerateFiles(sceneDir, "*.seq")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToDictionary(s => Int32.Parse(s[..2]), s => s[3..]);

        var animSequences = Directory.EnumerateFiles(sceneDir, "*.seq")
            .ToDictionary(f => Int32.Parse(Path.GetFileNameWithoutExtension(f)[..2]),
                f => AnimationSequenceScript.Read(f).StandAnimation);

        var pattern = args.Length > 1 ? Int32.Parse(args[1]) : 0;
        var formation = scene.Formations[pattern];
        var placements = formation.Monsters.Take(Math.Min(formation.MonsterCount, (Byte)4)).ToList();

        var labels = placements.Select((put, i) =>
        {
            var baseName = names[put.TypeNumber].DisplayName;
            if (placements.Count(p => p.TypeNumber == put.TypeNumber) == 1)
                return baseName;
            var ordinal = placements.Take(i + 1).Count(p => p.TypeNumber == put.TypeNumber);
            return $"{baseName} {(Char)('A' + ordinal - 1)}";
        }).ToList();

        var enemies = placements
            .Select((put, line) =>
            {
                var type = put.TypeNumber;
                var unit = BattleUnit.FromEnemy(labels[line], scene.Monsters[type]);
                unit.AttackPower = scene.Attacks[names[type].DefaultAttack].Power;
                unit.Abilities.AddRange(rosters[type].Where(n => n < scene.Attacks.Count).Select(n =>
                {
                    var a = scene.Attacks[n];
                    return new BattleAbility(
                        attackNames.GetValueOrDefault(n, $"Attack {n}"),
                        (AbilityScript)a.ScriptID, a.Power, (Element)a.Elements, a.Rate,
                        (BattleStatus)db.StatusSets[a.AddStatusNo].StatusMask, a.MP, n, a.Target,
                        SetsStandAnimation: animSequences.GetValueOrDefault(n));
                }));
                return unit;
            })
            .ToList();

        var threat = (Byte)enemies.SelectMany(e => e.Abilities)
            .Aggregate(0, (mask, a) => mask | (Int32)a.Elements);

        var level = enemies.Max(e => e.Level);
        var partyNames = args.Length > 2 ? args[2].Split(',') : ["Zidane", "Garnet", "Steiner", "Vivi"];
        var party = partyNames.Select(n => BuildCharacter(db, n, level, threat)).ToList();
        var host = new EngineHost(party, enemies, rng) { Pattern = pattern };
        var world = new WorldState
        {
            Tonberries = 12,
            Frogs = 45,
            Thefts = 180,
            Gil = 250_000,
            Kills = { [EnemyCategory.Dragon] = 40 }
        };
        var state = world.Scripts;
        EbVM? director;
        using (var eb = new BinaryReader(File.OpenRead(
                   Path.Combine(sceneDir, Path.GetFileName(sceneDir) + ".eb"))))
        {
            var script = EbScript.Read(eb);
            director = new EbVM(script.Entries[0], host, state);
            director.RunFunction(0);

            for (var line = 0; line < enemies.Count; line++)
            {
                var entryIndex = line < host.SpawnedEntries.Count ? host.SpawnedEntries[line] : line + 1;
                enemies[line].Brain = new ScriptedBrain(script.Entries[entryIndex], host, selfLine: 4 + line, state);
            }
            
            director.Request(1, 7);
        }

        var battleText = FF9Data.BattleTextFor(Path.GetFileName(sceneDir));
        var spellbook = party.ToDictionary(
            p => p.Name,
            p => Spellbook.For(db, p.Name,
                    APEarned.LearnedActives(db.Learnables.GetValueOrDefault(p.Name, []), p.Level),
                    Loadout.Best(db, p.Name, threat))
                .Where(s => BattleEngine.CanResolve(s.Script)).ToList());

        var enemyNames = enemies.Select(e => e.Name).ToList();
        var roll = enemyNames.Count == 1
            ? enemyNames[0]
            : $"{String.Join(", ", enemyNames.Take(enemyNames.Count - 1))} and {enemyNames[^1]}";
        Console.WriteLine($"— {roll} appeared! —");

        var statusTable = db.StatusData.Values
            .Where(s => s.Bit < 32)
            .ToDictionary(
                s => (BattleStatus)(1u << s.Bit),
                s => new StatusTiming(s.OprCount, s.ContiCount,
                    (BattleStatus)s.ClearOnApply, (BattleStatus)s.ImmunityProvided));

        var engine = new BattleEngine(party, enemies, EncounterKind.Normal, rng,
            statusTable: statusTable, host: host, director: director, world: world);

        foreach (var gem in new[] { "Opal", "Topaz", "Peridot", "Aquamarine", "Garnet", "Lapis Lazuli", "Sapphire" })
            engine.Gems[gem] = 20;
        
        while (engine.TickUntilReady() is { } ready)
        {
            NarrateTicks(engine);
            
            if (engine.IsEnemy(ready))
            {
                Narrate(engine.ExecuteEnemyTurn(ready));
                foreach (var id in host.PendingDialogs)
                    Console.WriteLine($"  『{(battleText is not null && id < battleText.Count ? battleText[id] : $"battle text {id}")}』");
                host.PendingDialogs.Clear();
                foreach (var (attackId, targetMask) in host.PendingSpecials)
                    Console.WriteLine($"  ‹{attackNames.GetValueOrDefault(attackId, $"attack {attackId}")}›");
                host.PendingSpecials.Clear();
            }
            else
                Narrate(TakePlayerTurn(db, engine, ready, spellbook));
            
            foreach (var reaction in engine.PendingReactions)
            {
                Console.WriteLine("  — counterattack! —");
                Narrate(reaction);
            }
            engine.PendingReactions.Clear();

            foreach (var hit in engine.PendingMultiHits)
                Narrate(hit);
            engine.PendingMultiHits.Clear();
        }
        NarrateTicks(engine);

        Console.WriteLine(host.Result switch
        {
            1 or 2 => "\n*** VICTORY! ***",
            7 => "\n*** The enemy escapes! ***",
            3 or 6 => "\n*** The party has fallen... ***",
            _ => engine.Victory ? "\n*** VICTORY! ***" : "\n*** The party has fallen... ***"
        });
        if (host.GilEarned > 0)
            Console.WriteLine($"Received {host.GilEarned} gil!");
    }

    private static BattleUnit BuildCharacter(RulesDB db, String name, Int32 level, Byte threat = 0)
    {
        var baseStats = db.BaseStats.First(c => c.Name == name);
        var slots = Loadout.Best(db, name, threat);
        var kit = EquipmentKit.Resolve(slots, db.Items, db.Weapons, db.Armors, db.GrowthStats);
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
            WeakElements = (Element)kit.WeakElements,
            PlayerCategory = db.Parameters[baseStats.ID].Category
        };
    }

    private static void Narrate(BattleEvent e)
    {
        if (e.Ability is not null)
            Console.WriteLine($"{e.Actor.Name} {(Spells.Contains(e.Ability.Script) ? "casts" : "uses")} {e.Ability.Name}!");

        var line = e.Result.Outcome switch
        {
            AttackOutcome.Miss => $"{e.Actor.Name}'s attack misses {e.Target.Name}!",
            AttackOutcome.Evaded => $"{e.Actor.Name} attacks {e.Target.Name} — evaded!",
            AttackOutcome.CriticalHit => $"{e.Actor.Name} lands a CRITICAL on {e.Target.Name} for {e.Result.Damage}!",
            AttackOutcome.Guarded => $"{e.Target.Name} nulls it — no effect!",
            AttackOutcome.Absorbed => $"{e.Target.Name} drinks it in — absorbs {e.Result.Damage} HP!",
            AttackOutcome.Hit when e.Result.Damage == 0 && e.StatusesAdded != 0 => $"{e.Target.Name} is now {e.StatusesAdded}.",
            AttackOutcome.Hit when e.Result.Damage == 0 => $"{e.Target.Name} is unaffected.",
            AttackOutcome.NoEffect when e.Ability is { } a && !Enum.IsDefined(a.Script)
                => $"{e.Actor.Name} is posturing — pure theatre.",
            AttackOutcome.NoEffect => $"{e.Actor.Name}'s {e.Ability?.Name} fizzles (script {e.Ability?.Script} not implemented).",
            AttackOutcome.Healed when e.Result.Damage > 0 => $"{e.Target.Name} recovers {e.Result.Damage} HP!",
            AttackOutcome.Healed => $"{e.Target.Name} is restored.",
            AttackOutcome.MPDamage => $"{e.Target.Name} loses {e.Result.Damage} MP!",
            AttackOutcome.Scanned =>
                $"{e.Target.Name}: HP {e.Target.CurrentHP}/{e.Target.MaxHP}  MP {e.Target.CurrentMP}/{e.Target.MaxMP}  " +
                $"Lv{e.Target.Level}  Str {e.Target.Strength} Mag {e.Target.Magic}  " +
                $"Def {e.Target.PhysicalDefense}/{e.Target.MagicalDefense}  " +
                $"weak {e.Target.WeakElements}, absorbs {e.Target.AbsorbElements}",
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
                var target = spell.DefaultAlly
                    ? PromptAlly(engine, spell.ForDead, "On whom? ")
                    : PromptEnemy(engine, "On which target? ");
                return engine.ExecuteAbility(actor, target, new BattleAbility(
                    spell.Name, spell.Script, spell.Power, (Element)spell.Elements, spell.Rate,
                    (BattleStatus)db.StatusSets[spell.StatusIndex].StatusMask, spell.MP, -1, spell.TargetType,
                    spell.ForDead));
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

    private static BattleUnit PromptAlly(BattleEngine engine, Boolean forDead, String question)
    {
        var choices = engine.Players.Where(p => forDead ? !p.IsAlive : p.IsAlive).ToList();
        if (choices.Count == 0)
            choices = [.. engine.Players];
        for (var i = 0; i < choices.Count; i++)
            Console.WriteLine($"  [{i + 1}] {choices[i].Name}  HP {choices[i].CurrentHP}/{choices[i].MaxHP}");
        return Prompt(choices, p => p.Name, question);
    }
}
