using Alexandria.Formats.Scripts;

namespace Alexandria.Formats.Tests;

public class EbScriptTests
{
    public static TheoryData<String> AllScenes()
    {
        var data = new TheoryData<String>();
        foreach (var dir in Directory.EnumerateDirectories(FF9Data.Scenes))
            data.Add(Path.GetFileName(dir));
        return data;
    }

    private static EbScript Load(String sceneName)
    {
        using var reader = new BinaryReader(File.OpenRead(FF9Data.SceneScript(sceneName)));
        return EbScript.Read(reader);
    }

    [Theory]
    [MemberData(nameof(AllScenes))]
    public void Every_battle_script_parses_cleanly(String sceneName)
    {
        var script = Load(sceneName);

        Assert.True(script.EntryCount >= 1);
        Assert.Equal(script.EntryCount, script.Entries.Count);

        foreach (var entry in script.Entries)
        {
            if (entry.Functions.Count > 0)
                Assert.True(2 + 4 * entry.Functions.Count <= entry.Functions[0].EntryPoint,
                    $"{sceneName}[{entry.Index}]: function table overlaps first body");

            for (var i = 1; i < entry.Functions.Count; i++)
                Assert.True(entry.Functions[i].EntryPoint > entry.Functions[i - 1].EntryPoint,
                    $"{sceneName}[{entry.Index}]: function bodies out of table order");
        }
    }

    [Fact]
    public void The_invasion_fight_has_twelve_entries()
        => Assert.Equal(12, Load("EVT_BATTLE_AC_E028F").EntryCount);
}
