using Alexandria.Formats.Scripts;

namespace Alexandria.Formats;

public static class FF9Data
{
    public static readonly String Root =
        Environment.GetEnvironmentVariable("FF9DATA") ?? @"C:\Dev\Memoria\FF9Data";

    /// <summary>Memoria's CSV rule data: Characters\, Items\, Battle\.</summary>
    public static String Data => Path.Combine(Root, "Data");

    /// <summary>One directory per encounter, each named EVT_BATTLE_*.</summary>
    public static String Scenes => Path.Combine(Root, @"BattleMap\BattleScene");

    /// <summary>Battle dialogue, one .mes file per scene id.</summary>
    public static String Text => Path.Combine(Root, @"Text\Battle");

    /// <summary>Scene name → the numeric id its .mes file is named after.</summary>
    public static String SceneCsv => Path.Combine(Root, "SceneData.csv");
    
    /// <summary>The game's own card table.</summary>
    public static String CardData => Path.Combine(Data, @"TetraMaster\MINIGAME_CARD_DATA_ADDRESS.bytes");

    /// <summary>Card categories - Memoria's.</summary>
    public static String TripleTriadCsv => Path.Combine(Data, @"TetraMaster\TripleTriad.csv");

    /// <summary>Card display names.</summary>
    public static String CardTitles => Path.Combine(Root, @"Text\US\ETC\CardTitles.strings");

    public static String Scene(String sceneName) => Path.Combine(Scenes, sceneName);

    /// <summary>A scene's script blob, which repeats the directory name: EVT_X\EVT_X.eb.</summary>
    public static String SceneScript(String sceneName) => Path.Combine(Scenes, sceneName, sceneName + ".eb");

    /// <summary>The dialogue for one scene, or null when it has none.</summary>
    public static IReadOnlyList<String>? BattleTextFor(String sceneName)
    {
        var ids = SceneIDs.Load(SceneCsv);
        var path = Path.Combine(Text, ids[sceneName["EVT_BATTLE_".Length..]] + ".mes");
        return File.Exists(path) ? BattleText.Load(path) : null;
    }

    /// <summary>Localised display names; US only for now.</summary>
    public static String ItemNames => Path.Combine(Root, @"Text\US\Items.strings");
}

