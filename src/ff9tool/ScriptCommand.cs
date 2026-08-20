using Alexandria.Formats;
using Alexandria.Formats.Scripts;

namespace ff9tool;

public static class ScriptCommand
{
    public static void Run(String[] args)
    {
        switch (args.Length)
        {
            case 0: Census(); break;
            case 1: Describe(args[0]); break;
            default: DisassembleEntry(args[0], Int32.Parse(args[1])); break;
        }
    }

    private static void Census()
    {
        Int32 parsed = 0, failed = 0, functions = 0, complete = 0;
        var blockers = new Dictionary<Int32, Int32>();

        foreach (var dir in Directory.EnumerateDirectories(FF9Data.Scenes))
        {
            var name = Path.GetFileName(dir);
            try
            {
                using var reader = new BinaryReader(File.OpenRead(Path.Combine(dir, name + ".eb")));
                var script = EbScript.Read(reader);
                parsed++;
                foreach (var entry in script.Entries)
                foreach (var fn in entry.Functions)
                {
                    functions++;
                    EbDisassembler.Disassemble(fn, out var blocked);
                    if (blocked is null)
                        complete++;
                    else
                        blockers[blocked.Value] = blockers.GetValueOrDefault(blocked.Value) + 1;
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"FAIL {name}: {ex.Message}");
            }
        }
        
        Console.WriteLine($"{parsed} ok, {failed} failed of {parsed + failed}");
        Console.WriteLine($"{complete}/{functions} functions disassemble start to finish");
        Console.WriteLine("opcode wishlist (functions blocked):");
        foreach (var (opcode, count) in blockers.OrderByDescending(b => b.Value).Take(15))
            Console.WriteLine($"  0x{opcode:X2}: {count}");
    }

    private static void Describe(String sceneName)
    {
        using var reader = new BinaryReader(File.OpenRead(FF9Data.SceneScript(sceneName)));
        var script = EbScript.Read(reader);
        
        Console.WriteLine($"{sceneName}: {script.EntryCount} entries");
        foreach (var entry in script.Entries)
        {
            var functions = String.Join(", ",
                entry.Functions.Select(f => $"{f.TagName}@{f.EntryPoint} ({f.Code.Length}B)"));
            Console.WriteLine($"  [{entry.Index}] type {entry.Type}, vars {entry.VarCount}: {functions}");
        }
    }

    private static void DisassembleEntry(String sceneName, Int32 entryIndex)
    {
        var sceneDir = FF9Data.Scene(sceneName);
        var attackNames = Directory.EnumerateFiles(sceneDir, "*.seq")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToDictionary(s => Int32.Parse(s[..2]), s => s[3..]);
        var battleText = FF9Data.BattleTextFor(sceneName);
        
        using var reader = new BinaryReader(
            File.OpenRead(Path.Combine(sceneDir, sceneName + ".eb")));
        var entry = EbScript.Read(reader).Entries[entryIndex];
        foreach (var fn in entry.Functions)
        {
            Console.WriteLine($"-- {fn.TagName} ({fn.Code.Length} bytes) --");
            foreach (var line in EbDisassembler.Disassemble(fn, attackNames, battleText))
                Console.WriteLine("  " + line);
            Console.WriteLine();
        }
    }
}

