using Alexandria.Formats;
using Alexandria.Formats.Scripts;

namespace ff9tool;

public static class FieldCommand
{
    public static void Run(String[] args)
    {
        if (args.Length > 0)
            Describe(args[0]);
        else
            Census();
    }

    private static void Census()
    {
        Int32 parsed = 0, failed = 0, functions = 0, complete = 0;
        var blockers = new Dictionary<Int32, Int32>();
        var reasons = new Dictionary<String, Int32>();

        foreach (var path in Directory.EnumerateFiles(FF9Data.FieldScripts, "*.eb").Order())
        {
            try
            {
                using var reader = new BinaryReader(File.OpenRead(path));
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
                reasons[ex.Message] = reasons.GetValueOrDefault(ex.Message) + 1;
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
}

