using Alexandria.Battle;
using Alexandria.Formats;
using Alexandria.Formats.Scenes.BattleScene;

namespace Alexandria.Battle.Tests;

public class BattleUnitTests
{
    [Fact]
    public void Type_C_wakes_up_ready_to_fight()
    {
        var path = Path.Combine(FF9Data.Scene("EVT_BATTLE_AC_E028F"), "dbfile0000.raw16");
        using var reader = new BinaryReader(File.OpenRead(path));
        var scene = BattleScene.Read(reader);

        var typeC = BattleUnit.FromEnemy("Type C", scene.Monsters[0]);
        Assert.Equal(623, typeC.MaxHP);
        Assert.Equal(623, typeC.CurrentHP);
        Assert.Equal(447, typeC.MaxMP);
        Assert.Equal(13, typeC.Level);
        Assert.Equal(21, typeC.Speed);
        Assert.Equal(10, typeC.PhysicalDefense);
        Assert.True(typeC.IsAlive);
        Assert.Equal(0, typeC.ATBGauge);
    }
}
