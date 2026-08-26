namespace Alexandria.Formats.Scripts;

/// <summary>An attack's animation sequence (mirror: btlseq). Memoria's exporter
/// dumps these already decompiled: each line is `Opcode: Key=Value ; Key=Value`,
/// with tab-indented blocks between StartThread and EndThread.
public sealed record AnimationSequenceScript(IReadOnlyList<AnimationSequenceInstruction> Instructions)
{
    /// <summary>The stand-animation this sequence leaves its caster in, 1 for
    /// Alternate=True, 0 for False, null when it never toggles.</summary>
    public Int32? StandAnimation
    {
        get
        {
            Int32? result = null;
            foreach(var i in Instructions)
                if (i.Opcode == "ToggleStandAnimation" && i.Args.GetValueOrDefault("Char") == "Caster")
                    result = i.Args.GetValueOrDefault("Alternate") == "True" ? 1 : 0;
            return result;
        }
    }
    
    /// <summary>True when the sequence's stand-animation toggle comes AFTER its EffectPoint.</summary>
    public Boolean TogglesAfterDamage
    {
        get
        {
            var effect = Instructions.ToList().FindIndex(i => i.Opcode == "EffectPoint");
            var toggle = Instructions.ToList().FindIndex(i => i.Opcode == "ToggleStandAnimation");
            return effect >= 0 && toggle > effect;
        }
    }
    
    public static AnimationSequenceScript Read(String path)
    {
        var instructions = new List<AnimationSequenceInstruction>();
        var depth = 0;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            var colon = line.IndexOf(':');
            var opcode = colon < 0 ? line : line[..colon];
            if (opcode == "EndThread")
                depth--;
            
            var args = new Dictionary<String, String>();
            if (colon >= 0)
            {
                foreach (var pair in line[(colon+1)..].Split(';'))
                {
                    var eq = pair.IndexOf('=');
                    if (eq > 0)
                        args[pair[..eq].Trim()] = pair[(eq + 1)..].Trim();
                }
            }
            instructions.Add(new AnimationSequenceInstruction(opcode, args, depth > 0));

            if (opcode == "StartThread")
                depth++;
        }
        return new AnimationSequenceScript(instructions);
    }
}

public sealed record AnimationSequenceInstruction(
    String Opcode,
    IReadOnlyDictionary<String, String> Args,
    Boolean InThread = false);
