using System.Text.RegularExpressions;

namespace Alexandria.Formats.Scripts;

/// <summary>Per-scene battle text table (mirror: FF9TextTool.GetBattleText +
/// EmbadedSentenseLoader.ExtractSentenseEnd). Entries split on [ENDN]; indices run
/// enemy names first, then attack names, then event-script messages - but always
/// index the whole array absolutely, as scripts reference lines beyond MesCnt.</summary>
public static class BattleText
{
    public static IReadOnlyList<String> Load(String mesPath)
    {
        var text = File.ReadAllText(mesPath);
        var split = text.Split("[ENDN]", StringSplitOptions.None).ToList();
        if (split.Count > 0 && text.EndsWith("[ENDN]"))
            split.RemoveAt(split.Count - 1);
        return [.. split.Select(Clean)];
    }

    /// <summary>Strips FF9 markup tags ([STRT=w,l], colors, speeds) for display.</summary>
    public static String Clean(String raw)
        => Regex.Replace(raw, @"\[[^\]]*\]", "").Trim();
}

