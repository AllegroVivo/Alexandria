using Alexandria.Formats.Memoria;
using Alexandria.Formats.Scenes.BattleScene;

namespace ff9tool;

/// <summary>Which ability scripts the game's rosters actually need.</summary>
public static class AbilityCensusCommand
{
    private const String SceneRoot = @"C:\Dev\Memoria\FF9Data\BattleMap\BattleScene";

    private static readonly HashSet<Int32> Implemented = [
        8, 9, 10, 11, 12, 16, 17, 25, 27, 30, 100, 103, 109
    ];

    public static void Run()
    {
        var uses = new Dictionary<Int32, Int32>();
        var examples = new Dictionary<Int32, String>();
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
                var script = (Int32)scene.Attacks[n].ScriptID;
                attacks++;
                if (Implemented.Contains(script))
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
                $"  {count,5}× {script,3} {ScriptName(script),-28} {(Implemented.Contains(script) ? "✓" : " ")}  e.g. {examples[script]}");
    }

    /// <summary>Vanilla's ability scripts by id, from Memoria.Scripts/Sources/Battle
    /// filenames. Gaps (47, 64, 78-79, 81-82…) have no script at all: attacks carrying
    /// them are pure choreography and resolve to nothing.</summary>
    private static String ScriptName(Int32 id) => id switch
    {
        0 => "BaseWeapon", 1 => "SimpleWeapon", 2 => "WillWeapon", 3 => "DexterityWeapon",
        4 => "MagicWeapon", 5 => "RandomWeapon", 6 => "BloodSwordWeapon", 7 => "LevelWeapon",
        8 => "EnemyPhysicalAttack", 9 => "MagicAttack", 10 => "MagicRecovery",
        11 => "MagicApplyNegativeStatus", 12 => "MagicCureStatus", 13 => "Revive",
        14 => "Death", 15 => "DrainMp", 16 => "DrainHp", 17 => "MagicGravityDamage",
        18 => "Meteorite", 19 => "PhysicalAttack", 20 => "OriginalMagicAttack",
        21 => "GoblinPunch", 22 => "LvDirectHPDamage", 23 => "LvHoly", 24 => "LvReduceDefence",
        25 => "PreciseDirectHPDamage", 26 => "ThousandNeedles", 27 => "DirectHPDamage",
        28 => "LimitGlove", 29 => "DifferentCasterHpAttack", 30 => "WhiteWind",
        31 => "RandomMpDamage", 32 => "Darkside", 33 => "ArmourBreak", 34 => "PowerBreak",
        35 => "MentalBreak", 36 => "MagicBreak", 37 => "Chakra", 38 => "SpareChange",
        39 => "Lancer", 40 => "DragonBreath", 41 => "WhiteDraw", 42 => "Throw",
        43 => "Might", 44 => "Focus", 45 => "Sacrifice", 46 => "SoulBlade",
        48 => "Spear", 49 => "Phoenix", 50 => "SixDragons", 51 => "Curse",
        52 => "AngelSnack", 53 => "LuckySeven", 54 => "WhatIsThat", 55 => "ChangeRow",
        56 => "FleeIteration", 57 => "Flee", 58 => "Steal", 59 => "Scan",
        60 => "Detect", 61 => "Charge", 62 => "ItemSoft", 63 => "MagicSwordAttack",
        65 => "Eat", 66 => "FrogDrop", 67 => "Thievery", 68 => "DragonCrest",
        69 => "ItemPotion", 70 => "ItemEther", 71 => "ItemElixir", 72 => "ItemPhoenix",
        73 => "ItemCureStatus", 74 => "ItemGem", 75 => "DeadPepper", 76 => "Tent",
        77 => "DarkMatter", 80 => "DoubleCastSpecial", 83 => "MassSpear", 84 => "Jewel",
        85 => "Summon", 86 => "Atomos", 87 => "Odin", 88 => "Melt",
        89 => "HPSwitching", 90 => "HalfDefence", 91 => "Cannon", 92 => "ItemAdd",
        93 => "Maelstrom", 94 => "AbsorbMagic", 95 => "AbsorbStrength", 96 => "TranceFull",
        97 => "Entice", 98 => "SimpleAttackGaia", 99 => "FlareStar",
        100 => "PreciseEnemyPhysicalAttack", 101 => "EnemySteal", 102 => "EnemyMug",
        103 => "MagicApplyPositiveStatus", 104 => "TonberryKarma", 105 => "GrandCross",
        106 => "Swallow", 107 => "PreciseEnemyPhysicalAttackAndChangeRow",
        108 => "IaiStrike", 109 => "Mini",
        _ => "(no script — choreography)"
    };
}
