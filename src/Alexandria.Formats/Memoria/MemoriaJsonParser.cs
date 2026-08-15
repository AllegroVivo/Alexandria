using System.Text;
using System.Text.Json;

namespace Alexandria.Formats.Memoria;

public class MemoriaJsonParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static List<EnemyDto> ReadEnemies(String path)
        => JsonSerializer.Deserialize<List<EnemyDto>>(
            FixMissingCommas(File.ReadAllText(path)), Options) ?? [];

    /// <summary>Per-species usable attack indices, recovered by Memoria from the animation
    /// sequence data (each species' sequences share a base-anim offset). Montype order
    /// matches the scene's monster order.</summary>
    public static IReadOnlyDictionary<Int32, IReadOnlyList<Int32>> ReadPlayableSequences(String path)
    {
        var json = FixMissingCommas(File.ReadAllText(path));
        var dto = JsonSerializer.Deserialize<ScriptsDto>(json, Options);
        return dto.SequenceProperty.ToDictionary(
            p => p.Montype,
            p => (IReadOnlyList<Int32>)p.PlayableSequence);
    }

    private static String FixMissingCommas(String raw)
    {
        var lines = raw.Split('\n');
        var result = new StringBuilder(raw.Length + lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            result.Append(line);
            if (NeedsComma(line, NextContentLine(lines, i)))
                result.Append(',');
            result.Append('\n');
        }
        return result.ToString();
    }

    private static Boolean NeedsComma(String line, String? next)
    {
        if (line.Length == 0 || next is null || !next.StartsWith('"'))
            return false;

        var last = line[^1];
        // A property line ends in a value: string, number, bool/null, or a closing bracket.
        // Openers ('{' and '[') and already-present commas never qualify.
        return last is '"' or ']' or '}' || Char.IsDigit(last) || Char.IsLetter(last);
    }

    private static String? NextContentLine(String[] lines, Int32 index)
    {
        for (var i = index + 1; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.Length > 0)
                return trimmed;
        }
        return null;
    }

    private sealed record ScriptsDto(List<SequencePropertyDto> SequenceProperty);

    private sealed record SequencePropertyDto(Int32 Montype, List<Int32> PlayableSequence);
}

public sealed record ElementDto(Byte Dex, Byte Str, Byte Mgc, Byte Wpr);

public sealed record EnemyDto(
    String Name,
    UInt16 MaxHp,
    UInt16 MaxMp,
    UInt16 WinGil,
    UInt16 WinExp,
    UInt16 AP,
    Byte Level,
    ElementDto Element,
    Byte Pdp,
    Byte Pav,
    Byte Mdp,
    Byte Mav,
    Int32 Card,
    Int32 DieSfx,
    Int32 DefaultAttack)
{
    /// <summary>Name with the leading [STRT=w,h] tag stripped.</summary>
    public String DisplayName => Name[(Name.IndexOf(']') + 1)..];
}
