using Alexandria.Battle;
using Alexandria.Formats;
using Alexandria.Formats.Scripts;

namespace ff9tool;

public static class FieldCommand
{
    public static void Run(String[] args)
    {
        switch (args.Length)
        {
            case 0: Census(); break;
            case 1: Describe(args[0]); break;
            case 2: Disassemble(args[0], Int32.Parse(args[1])); break;
            default: Execute(args[0], Int32.Parse(args[1]), Int32.Parse(args[2])); break;
        }
    }

    private static void Census()
    {
        Int32 parsed = 0, failed = 0, functions = 0, complete = 0;
        var blockers = new Dictionary<Int32, Int32>();
        var reasons = new Dictionary<String, Int32>();
        var examples = new List<String>();

        foreach (var path in Directory.EnumerateFiles(FF9Data.FieldScripts, "*.eb").Order())
        {
            try
            {
                using var reader = new BinaryReader(File.OpenRead(path));
                var script = EbScript.Read(reader);
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
                parsed++;
            }
            catch (Exception ex)
            {
                failed++;
                reasons[ex.Message] = reasons.GetValueOrDefault(ex.Message) + 1;
                if (examples.Count < 5)
                    examples.Add(Path.GetFileNameWithoutExtension(path));
            }
        }
        
        Console.WriteLine($"{parsed} ok, {failed} failed of {parsed + failed}");
        Console.WriteLine($"{complete}/{functions} functions disassemble start to finish");

        if (reasons.Count > 0)
        {
            Console.WriteLine("why they failed:");
            foreach (var (reason, count) in reasons.OrderByDescending(r => r.Value).Take(8))
                Console.WriteLine($"  {count,5}  {reason}");
        }
        Console.WriteLine($"  e.g. {String.Join(", ", examples)}");
        
        Console.WriteLine("opcode wishlist (functions blocked):");
        foreach (var (opcode, count) in blockers.OrderByDescending(b => b.Value).Take(20))
            Console.WriteLine($"  0x{opcode:X2}: {count}");
    }

    private static void Describe(String name)
    {
        var path = Path.Combine(FF9Data.FieldScripts, name.EndsWith(".eb") ? name : name + ".eb");
        using var reader = new BinaryReader(File.OpenRead(path));
        var script = EbScript.Read(reader);
        
        Console.WriteLine($"{Path.GetFileNameWithoutExtension(path)}: {script.EntryCount} entries");
        foreach (var entry in script.Entries)
            Console.WriteLine($"  [{entry.Index}] type {entry.Type}, vars {entry.VarCount}: " +
                              String.Join(", ", entry.Functions.Select(f => $"{f.TagName}@{f.EntryPoint} ({f.Code.Length}B)")));
    }

    private static String ScriptPath(String name)
        => Path.Combine(FF9Data.FieldScripts, name.EndsWith(".eb") ? name : name + ".eb");

    private static void Disassemble(String name, Int32 entryIndex)
    {
        using var reader = new BinaryReader(File.OpenRead(ScriptPath(name)));
        var entry = EbScript.Read(reader).Entries[entryIndex];

        foreach (var fn in entry.Functions)
        {
            Console.WriteLine($"-- {fn.TagName} ({fn.Code.Length} bytes) --");
            var lines = EbDisassembler.Disassemble(fn, out var blocked);
            foreach (var line in lines)
                Console.WriteLine("  " + line);
            
            if (blocked is not null)
                Console.WriteLine($"  ... stops at opcode 0x{blocked.Value:X2}");
            Console.WriteLine();
        }
    }

    private static void Execute(String name, Int32 entryIndex, Int32 tag)
    {
        using var reader = new BinaryReader(File.OpenRead(ScriptPath(name)));
        var script = EbScript.Read(reader);
        var world = new WorldState();
        var vm = new EbVM(
            script.Entries[entryIndex],
            new FieldHost(world, new FieldContext { UserControl = 1 }, new Random(1)), 
            new EbState(), uid: 128);

        try
        {
            if (tag != 0)
                vm.RunFunction(0);
            vm.RunFunction((UInt16)tag);
            Console.WriteLine($"ran to completion; frogs {world.Frogs}, gil {world.Gil}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"stopped: {ex.Message}");
        }
        
        Console.WriteLine(
            $"stepped over {vm.Skipped.Count} distinct presentation opcodes: " +
            String.Join(", ", vm.Skipped.Order().Select(o => $"0x{o:X2}")));
    }
}

