namespace Alexandria.Rules;

/// <summary>
/// Minimal reader for Memoria's CSV dialect: semicolon-separated,
/// '#' begins a comment (whole line or trailing field), blank lines ignored.
/// </summary>
/// Limitation: '#!' metadata directives (version + optional-column flags) are skipped, not honored.
public static class MemoriaCsv
{
    public static IEnumerable<String[]> ReadRows(String path)
    {
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var fields = line.Split(';')
                .Select(f => f.Trim())
                .TakeWhile(f => !f.StartsWith('#'))
                .ToArray();

            if (fields.Length > 0)
                yield return fields;
        }
    }

    public static Int32[] ParseIntArray(String field) =>
    [
        .. field.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Int32.Parse)
    ];
}
