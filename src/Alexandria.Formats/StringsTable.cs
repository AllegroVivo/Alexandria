using System.Text.RegularExpressions;

namespace Alexandria.Formats;

/// <summary>Reader for FF9's `.strings` localisation tables (Text/US/Items.strings and its
/// siblings). Each entry reads `"$item0001" = "Name\ndescription...";`.</summary>
public static class StringsTable
{
    public static IReadOnlyDictionary<Int32, String> Load(String path)
        => Regex.Matches(File.ReadAllText(path),
                """^"\$\w*?(\d+)"\s*=\s*"([^"\r\n]*)""", RegexOptions.Multiline)
            .ToDictionary(m => Int32.Parse(m.Groups[1].Value), m => m.Groups[2].Value);
}

