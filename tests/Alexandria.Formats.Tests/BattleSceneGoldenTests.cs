using Alexandria.Formats.Memoria;
using Alexandria.Formats.Scenes.BattleScene;

namespace Alexandria.Formats.Tests;

public class BattleSceneGoldenTests
{
    public static IEnumerable<Object[]> AllScenes()
        => Directory.EnumerateDirectories(FF9Data.Scenes, "EVT_BATTLE_*")
            .Select(dir => new Object[] { Path.GetFileName(dir) });

    /// <summary>Our reader against Memoria's own export, for every scene in the game.
    /// All 562 carry both dbfile0000.raw16 and Enemies.json, so nothing is skipped.</summary>
    [Theory]
    [MemberData(nameof(AllScenes))]
    public void Scene_matches_memoria_export(String sceneName)
    {
        var dir = FF9Data.Scene(sceneName);

        using var reader = new BinaryReader(File.OpenRead(Path.Combine(dir, "dbfile0000.raw16")));
        var scene = BattleScene.Read(reader);
        var expected = MemoriaJsonParser.ReadEnemies(Path.Combine(dir, "Enemies.json"));

        Assert.Equal(expected.Count, scene.Monsters.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var dto = expected[i];
            var monster = scene.Monsters[i];

            Assert.Equal(dto.MaxHp, monster.MaxHP);
            Assert.Equal(dto.MaxMp, monster.MaxMP);
            Assert.Equal(dto.WinGil, monster.WinGil);
            Assert.Equal(dto.WinExp, monster.WinExp);
            Assert.Equal(dto.AP, monster.AP);
            Assert.Equal(dto.Level, monster.Level);
            Assert.Equal(dto.Element.Dex, monster.MonsterStats.Speed);
            Assert.Equal(dto.Element.Str, monster.MonsterStats.Strength);
            Assert.Equal(dto.Element.Mgc, monster.MonsterStats.Magic);
            Assert.Equal(dto.Element.Wpr, monster.MonsterStats.Spirit);
            Assert.Equal(dto.Pdp, monster.PDefense);
            Assert.Equal(dto.Pav, monster.PEvade);
            Assert.Equal(dto.Mdp, monster.MDefense);
            Assert.Equal(dto.Mav, monster.MEvade);
            Assert.Equal(dto.Card, monster.WinCard);

            // Memoria JSON's -1 is our 65535 read - same bits.
            Assert.Equal(dto.DieSfx, unchecked((Int16)monster.DieSfx));
        }
    }

    [Fact]
    public void Type_C_Strike_has_its_real_numbers()
    {
        var dir = FF9Data.Scene("EVT_BATTLE_AC_E028F");
        using var reader = new BinaryReader(File.OpenRead(Path.Combine(dir, "dbfile0000.raw16")));
        var scene = BattleScene.Read(reader);

        var strike = scene.Attacks[0];
        Assert.Equal(8, strike.ScriptID);     // enemy physical
        Assert.Equal(22, strike.Power);
        Assert.Equal(100, strike.Rate);
        Assert.Equal(44, strike.Category);
        Assert.Equal(0, strike.MP);
    }
}